# AxarDB Documentation for AI Models

This file teaches AI models how to use AxarDB correctly. AxarDB is an **in-memory NoSQL database** that runs on ASP.NET Core. It uses **JavaScript** for all queries, powered by the Jint engine.

> **CRITICAL**: Read each section carefully. Follow the exact syntax shown.

## 1. Core Concept

*   **Structure**: Database -> Collections (like tables) -> Documents (JSON objects).
*   **Language**: All queries are JavaScript code executed server-side.
*   **Root Object**: `db` is the main database object.
    *   `db.users` refers to the "users" collection.
    *   `db.orders` refers to the "orders" collection.
    *   Collections are created automatically on first write.

## 2. Data Operations (CRUD)

### A. Insert Data
Use `insert(object)`. Returns the inserted document with auto-generated `_id`.
```javascript
// Insert a single document
db.users.insert({ 
    name: "John", 
    age: 25, 
    isAdmin: false,
    tags: ["developer", "senior"]
});

// The _id field is generated automatically
db.products.insert({ name: "Laptop", price: 999.99, inStock: true });
```

### B. Find Data (Reading)

#### `findall()` — Returns a ResultSet (fully chainable and iterable)
> **NOTE**: `findall()` returns a `ResultSet`, which represents the query result. While it is fully chainable and can be iterated directly in JS loops, you can optionally call `.toList()` or `.ToList()` to convert it to a standard JavaScript array if needed. Both casing variants work.

```javascript
// ✅ CORRECT — Get all users as array
var list = db.users.findall();

// ✅ CORRECT — Filter with predicate
var adults = db.users.findall(u => u.age > 18);

// ✅ CORRECT — Case variants both work
var items = db.orders.findall();

// ✅ VALID — Returns ResultSet
var resultSet = db.users.findall();
// resultSet supports chaining (.take(5), .skip(5), .select(x => x.name), .delete(), .update())
// and is fully iterable in JS (e.g. for (var x of resultSet))
```

#### `find()` — Returns one item
```javascript
// Find first user matching condition
var admin = db.users.find(u => u.isAdmin == true);

// Returns null if not found — always check
var user = db.users.find(u => u.email == "john@test.com");
if (user) {
    console.log(user.name);
}
```

#### Boolean Filtering
Booleans are compared with `== true` or `== false`:
```javascript
var premiums = db.users.findall(u => u.isPremium == true);
var freeUsers = db.users.findall(u => u.isPremium == false);
```

### C. Update Data
Two ways to update:
```javascript
// 1. Direct update by condition (preferred for single field updates)
db.users.update(u => u._id == "abc123", { status: "active", updatedAt: new Date() });

// 2. Update via ResultSet chain
db.users.findall(u => u.inactive == true).update({ status: "archived" });
```

### D. Delete Data
```javascript
// Delete by filter
db.users.findall(u => u.age < 18).delete();

// Delete specific record
db.orders.findall(u => u._id == "order_123").delete();
```

## 3. ResultSet Chain Methods

After `findall()`, you can chain these methods:

| Method | Description | Example |
|:---|:---|:---|
| `.toList()` / `.ToList()` | **Optional** — Convert ResultSet to array | `findall().toList()` |
| `.take(n)` | Limit results to first N items | `findall().take(5)` |
| `.skip(n)` | Skip the first N items | `findall().skip(10)` |
| `.select(fn)` | Project/transform each document | `findall().select(u => u.name)` |
| `.orderBy(fn)` | Order results ascending | `findall().orderBy(u => u.age)` |
| `.orderByDesc(fn)` | Order results descending | `findall().orderByDesc(u => u.age)` |
| `.max(selector)` | Get maximum value of a field | `findall().max(u => u.age)` |
| `.min(selector)` | Get minimum value of a field | `findall().min(u => u.age)` |
| `.count(predicate?)` | Get total count or conditionally count matches | `findall().count(x => x.age > 18)` |
| `.distinct(selector?)`| Get a list of unique values or objects | `findall().distinct(x => x.role)` |
| `.first()` | Get first matching item | `findall().first()` |
| `.foreach(fn)` | Execute callback for each item | `findall().foreach(u => console.log(u.name))` |
| `.update(obj)` | Update all matching records | `findall(u => u.old == true).update({old: false})` |
| `.delete()` | Delete all matching records | `findall(u => u.expired == true).delete()` |

### Chain Examples
```javascript
// Get names of top 5 expensive products
var top5 = db.products.findall(p => p.price > 1000)
                      .take(5)
                      .select(p => p.name)
                      ;

// Count active users
var count = db.users.findall(u => u.active == true).count();

// Get first admin
var firstAdmin = db.users.findall(u => u.isAdmin == true).first();

// Iterate and log each user
db.users.findall().foreach(u => {
    console.log(u.name + ": " + u.email);
});
```

### Case-Insensitive Search

AxarDB provides **two distinct** mechanisms for case-insensitive operations. They serve different purposes and must not be confused:

#### 1. Collection-Level `db.collection.contains(predicate)` — Exact Match
Uses a `CaseInsensitiveDocumentWrapper` so that **property access** on documents is case-insensitive. Performs exact equality comparisons:
```javascript
var devs = db.users.contains(x => x.title == "developer");
// Matches "Developer", "DEVELOPER", "developer" — exact equality, case-insensitive property access
```

#### 2. String Prototype `.contains(str)` — Substring Search (Case-Insensitive)
AxarDB injects a custom `String.prototype.contains()` method into every script execution. This performs **case-insensitive substring** matching on any string field:
```javascript
// Substring search inside a predicate
var results = bulk.postalcodes.findall(x => x.placeName.contains("esen"));
// Matches "Esenler", "ESENYURT", "Büyükçekmece" — any placeName containing "esen" (case-insensitive)

// Also works in any JavaScript string context
var found = "İstanbul".contains("istan"); // true
```

> **CRITICAL**: Standard JavaScript does **not** have `String.prototype.contains()`. AxarDB injects this method for convenience. It wraps `String.prototype.includes()` with case-insensitive behavior and Turkish character normalization. For case-sensitive substring search, use the standard `.includes()` method.

### Built-in JavaScript Prototype Extensions

AxarDB injects several methods into JavaScript's built-in prototypes on **every script execution**. These are available in all predicates, views, triggers, and any JavaScript context within AxarDB. They are **not** standard JavaScript — they are AxarDB-specific additions.

#### String Prototype Extensions

| Method | Description | Example |
|:---|:---|:---|
| `.contains(str)` | **Case-insensitive** substring search. Handles Turkish characters correctly. Returns `boolean`. | `"İstanbul".contains("istan")` → `true` |
| `.startsWith(str)` | **Case-insensitive** prefix check. Handles Turkish characters correctly. Returns `boolean`. | `"İstanbul".startsWith("ist")` → `true` |
| `.toLowerCase()` | **Turkish-aware** lowercase. Normalizes `İ→i`, `I→i`, `Ö→ö`, `Ü→ü`, `Ç→ç`, `Ş→ş`, `Ğ→ğ`. Overrides the standard `.toLowerCase()`. | `"İZMİR".toLowerCase()` → `"izmir"` |

> **IMPORTANT**: AxarDB's `.contains()` and `.startsWith()` override the standard JavaScript behavior. The standard `.includes()` is case-sensitive; AxarDB's `.contains()` is case-insensitive. The standard `.startsWith()` is case-sensitive; AxarDB's override is not. The standard `.toLowerCase()` does not handle Turkish characters correctly; AxarDB's override does.

**Usage in predicates:**
```javascript
// Substring search inside a findall predicate
var gmailUsers = db.users.findall(u => u.email.contains("gmail"));

// Bulk store substring search
var matches = bulk.products.findall(p => p.name.contains("phone"));

// Prefix check (case-insensitive)
var adminEmails = db.users.findall(u => u.email.startsWith("admin"));

// Turkish character handling
var city = "İZMİR".toLowerCase(); // "izmir" (not "İzmİr" as standard JS would produce)
var found = city.contains("iz");  // true
```

#### UUID v7 Functions

AxarDB uses UUID v7 as its default `_id` generation scheme for all collections (Standard, Memory, and Bulk). It also exposes query utility functions to generate or extract metadata from UUID v7.

| Function | Description | Example |
|:---|:---|:---|
| `guidv7()` | Generates a new UUID v7 using the current UTC time. Returns `string`. | `guidv7()` → `"019853ab-1c2d-7e4f-..."` |
| `guidv7(datetime)` | Generates a new UUID v7 using the specified ISO 8601 datetime string. Returns `string`. | `guidv7("2024-01-15T10:30:00Z")` |
| `guidv7CreatedAt(guidStr)` | Extracts the UTC creation timestamp from a UUID v7 string. Returns `Date` (or `null` if invalid/not v7). | `guidv7CreatedAt("019853ab-...")` |
| `guid()` | Generates a standard UUID v4. (Maintained for backward compatibility). Returns `string`. | `guid()` |

**Usage examples:**
```javascript
// Insert with explicit UUID v7 using a custom date
db.history.insert({
    _id: guidv7("2023-05-10T14:20:00Z"),
    event: "Legacy Import"
});

// Extract creation date from a document's ID
var doc = db.users.find(u => u.name == "John");
if (doc && doc._id) {
    var createdTime = guidv7CreatedAt(doc._id);
    console.log("Document was created at: " + createdTime);
}
```

#### Array Prototype Extensions

These methods are available on **any JavaScript array**:

| Method | Description | Example |
|:---|:---|:---|
| `.count(predicate?)` | Count elements. Without argument returns `array.length`. With predicate, counts matching elements. | `arr.count(x => x.age > 18)` |
| `.distinct(selector?)` | Return array of unique values. Optional selector transforms before deduplication. | `arr.distinct(x => x.role)` |
| `.toList()` | Identity method, returns the array itself (for API consistency with ResultSet). | `[1,2].toList()` → `[1,2]` |

#### Object Prototype Extension

| Method | Description |
|:---|:---|
| `.toList()` | Converts iterable objects (ResultSets, .NET enumerables) to plain JavaScript arrays. |
| `.includes(arr)` | Checks if the object is contained in the specified array. If the object itself is an array, it performs a strict equality check (like C# LINQ `SequenceEqual`). Example: `x.rowNumber.includes([66,69,74])` |

**Usage examples for `.includes(arr)`:**
```javascript
// 1. Single Value Check (Similar to LINQ Contains)
// Check if a document's rowNumber is one of [66, 69, 74]
var targetCats = [66, 69, 74];
var myCats = db.categories.findall(x => x.rowNumber != null && x.rowNumber.includes(targetCats));

// 2. Array Comparison (Similar to LINQ SequenceEqual)
// Find users where tags exactly match ["developer", "senior"]
var targetTags = ["developer", "senior"];
var matches = db.users.findall(x => x.tags.includes(targetTags));
```

## 4. Join Operations

AxarDB supports powerful multi-joins between collections. By default, results use `j1`, `j2`, `j3`... indexing, but the `alias()` function provides a more readable "named" approach.

### A. Named Joins (Recommended)
Use `alias(source, name)` to give each join source a meaningful name.
```javascript
var result = db.join(
    alias(db.users, "user"), 
    alias(db.orders, "order"),
    alias(db.products, "product")
)
.where(x => 
    x.user._id == x.order.userId &&
    x.order.productId == x.product._id
)
.select(x => ({
    customer: x.user.name,
    item: x.product.name,
    date: x.order.createdAt
}))
;
```

### B. Default Indexed Joins
If aliases are not provided, sources are indexed as `j1`, `j2`, `j3`, etc., based on their position in `db.join`.
```javascript
// j1 = users, j2 = orders
var result = db.join(db.users, db.orders)
    .where(x => x.j1._id == x.j2.userId)
    ;
```

### C. Joining Arrays/Parameters
You can join literal arrays or objects passed as parameters:
```javascript
// order.items is an array inside the view
return db.join(alias(order.items, "item"), alias(db.products, "prod"))
    .where(x => x.item.productId == x.prod._id)
    ;
```

## 5. Index Creation

Create indexes for faster queries:
```javascript
// ASC index (default)
db.users.index(x => x.email);

// DESC index
db.orders.index(x => x.createdAt, "DESC");

// Check existing indexes
getIndexes("users");
```

## 6. Views (Stored Queries)

Views are server-side stored JavaScript scripts saved as `.js` files in the `Views/` folder.

### Access Control
Every view **must** declare its access level on the first line as a comment:
- `// @access public` — Accessible via HTTP without authentication
- `// @access private` — Requires Basic Auth to access via HTTP

### Parameters in Views
Views use `@paramName` syntax for parameters. These are replaced with values from the HTTP query string.

```javascript
// Create a public view WITH parameters
db.saveView("getUsersByAge", `
// @access public
var minAge = @minAge;
var maxAge = @maxAge;
return db.users.findall(u => u.age >= minAge && u.age <= maxAge);
`);

// Create a public view WITHOUT parameters
db.saveView("activeUsers", `
// @access public
return db.users.findall(u => u.active == true);
`);

// Create a private view
db.saveView("internalReport", `
// @access private
return db.orders.findall();
`);
```

### Using Views in JavaScript
```javascript
// Execute a view (no parameters)
var result = db.view("activeUsers");

// Execute a view with parameters
var result = db.view("getUsersByAge", { minAge: 18, maxAge: 65 });

// Read a view's source code
var code = db.getView("activeUsers");

// Delete a view
db.deleteView("oldView");
```

### Calling Views via HTTP
```bash
# Public view — NO authentication needed
curl "http://localhost:5000/views/activeUsers"

# Public view with parameters — values replace @param placeholders
curl "http://localhost:5000/views/getUsersByAge?minAge=18&maxAge=65"

# Private view — requires Basic Auth
curl -u "unlocker:unlocker" "http://localhost:5000/views/internalReport"
```

### View with Vault Variables and Encryption
Views can use vault variables (`$KEY_NAME`) and utility functions:
```javascript
db.saveView("login", `
// @access public
var email = @email;
var password = sha256(@password);
var deviceid = @deviceid;
var raw = email + "|" + password + "|" + deviceid;
var existing = db.users.find(u => u.email == email && u.password == password && u.deviceid == deviceid);
if (existing)
    return {status: true, token: encrypt(raw, $LOGIN_SALT)};
return {status: false, token: null};
`);
```
Call this view:
```bash
curl "http://localhost:5000/views/login?email=test@test.com&password=mypass&deviceid=DEV001"
```

## 7. Triggers (Event Handlers)

Triggers run automatically when data in a collection changes.

```javascript
// Create a trigger
db.saveTrigger("userNotifier", "users", `
// @target users
console.log("User changed: " + event.documentId);
webhook("https://api.example.com/notify", 
    { id: event.documentId, type: event.type },
    { "Authorization": "Bearer " + $API_TOKEN }
);
`);
```

### Event Object Properties
```javascript
event.type        // "created" | "changed" | "deleted"
event.collection  // Collection name that was modified
event.documentId  // The _id of the affected document
event.timestamp   // Server timestamp (ISO format)
```

### Manage Triggers
```javascript
db.deleteTrigger("oldTrigger");
```

## 8. Vaults (Secure Key-Value Storage)

Store API keys, secrets, and configuration values:
```javascript
// Add vault entries
addVault("API_KEY", "sk-xxxx...");
addVault("SLACK_WEBHOOK", "https://hooks.slack.com/...");
addVault("LOGIN_SALT", "mysecretkey123");

// Use in scripts with $KEY_NAME — replaced at runtime
webhook($SLACK_WEBHOOK, { text: "Alert!" }, {});
var token = encrypt("data", $LOGIN_SALT);
```

## 9. HTTP Functions

### webhook (POST)
Send HTTP POST requests to external services:
```javascript
// webhook(url, data, headers)
webhook("https://api.example.com/notify", 
    { userId: 123, action: "update" },
    { 
        "Authorization": "Bearer " + $API_TOKEN,
        "Content-Type": "application/json"
    }
);
// Returns: { success: true/false, status: 200, data: {...} }
```

### httpGet (GET)
Send HTTP GET requests:
```javascript
// httpGet(url, headers)
var response = httpGet("https://api.example.com/data", {
    "Authorization": "Bearer " + $API_TOKEN
});
// Returns: { success: true/false, status: 200, data: {...} }

// Without headers
var result = httpGet("https://api.example.com/public");
```

## 10. External Database Access (MySQL/MariaDB)

AxarDB can connect to external MySQL or MariaDB databases directly from scripts.

### Functions

#### `mysqlRead(connectionString, query, parameters)`
Executes a `SELECT` statement and returns a list of objects.

```javascript
var conn = "Server=127.0.0.1;Database=test;Uid=root;Pwd=pass;";
var users = mysqlRead(conn, "SELECT id, name FROM users WHERE age > @age", { age: 18 });
// Returns: [{ id: 1, name: "Alice" }, { id: 2, name: "Bob" }]
```

#### `mysqlExec(connectionString, query, parameters)`
Executes `INSERT`, `UPDATE`, or `DELETE` statements and returns the number of affected rows.

```javascript
var conn = "Server=127.0.0.1;Database=test;Uid=root;Pwd=pass;";
var count = mysqlExec(conn, "DELETE FROM logs WHERE created_at < @date", { date: "2023-01-01" });
// Returns: integer (e.g., 5)
```

### PostgreSQL Functions

#### `pgsqlRead(connectionString, query, parameters)`
Executes a `SELECT` statement against a PostgreSQL database.

```javascript
var conn = "Host=localhost;Database=testdb;Username=postgres;Password=secret";
var data = pgsqlRead(conn, "SELECT id, json_data FROM reports WHERE created_at > @date", { date: "2023-01-01" });
```

#### `pgsqlExec(connectionString, query, parameters)`
Executes `INSERT`, `UPDATE`, or `DELETE` statements against a PostgreSQL database.

```javascript
var conn = "Host=localhost;Database=testdb;Username=postgres;Password=secret";
var affected = pgsqlExec(conn, "UPDATE reports SET status = 'archived' WHERE id = @id", { id: 100 });
```

### Logging
- All queries are logged to server-side request logs.
- Errors are logged to error logs.
- View execution context is preserved in logs.

- View execution context is preserved in logs.

## 11. Memory Store (Temporary In-Memory Storage)

The `memory` object works exactly like `db`, but stores data **only in server RAM** — nothing is written to disk. Each record has a TTL (Time-To-Live) and is automatically removed when it expires.

> **NOTE**: `memory` is a top-level object, just like `db`. Use it directly — not as `db.memory`.

### Insert with TTL
```javascript
// Insert with default TTL (1 hour)
memory.sessions.insert({ userId: "abc", token: "xyz" });

// Insert with custom TTL (2.5 hours)
memory.sessions.insert({ userId: "abc", token: "xyz" }, 2.5);

// Insert with 0.5 hours (30 minutes)
memory.cache.insert({ key: "homepage", html: "<h1>Hi</h1>" }, 0.5);
```

### Find Data
```javascript
// Get all entries from memory collection
var sessions = memory.sessions.findall();

// Filter with a predicate
var active = memory.sessions.findall(s => s.active == true);

// Find a single entry
var session = memory.sessions.find(s => s.userId == "abc");
if (session) {
    console.log(session.token);
}
```

### Delete Data
```javascript
// Delete all entries
memory.sessions.findall().delete();

// Delete by filter
memory.sessions.findall(s => s.userId == "expired_user").delete();
```

### Key Differences from `db`
| Feature | `db` | `memory` |
|:---|:---|:---|
| Persistence | ✅ Disk (survives restart) | ❌ RAM only (lost on restart) |
| TTL support | ❌ No expiry | ✅ Auto-expires (default 1h) |
| Use case | Permanent data | Sessions, caches, temp data |

## 12. Bulk Store (JSONL Storage)

The `bulk` object manages collections of data serialized in the JSONL (JSON Lines) format inside the `Bulk/` folder. It is designed to handle large, static lookup tables (such as countries, postal codes, product catalogs) efficiently without causing performance locks from individual JSON file reads on disk.

> **NOTE**: `bulk` is a top-level object, just like `db` and `memory`. Use it directly — not as `db.bulk`.

### Bulk Operations
- **Bulk Insert / Initialize**: `bulk.countries.insert([...ArrayOfObjects])`
- **Bulk Retrieve**: `bulk.countries.findall()`
- **Bulk Find**: `bulk.countries.find(c => c.code == "TR")`
- **Bulk Delete**: `bulk.countries.findall(c => c.code == "US").delete()`
- **Bulk Count**: `bulk.countries.count()`
- **Manual Cache Reload**: `bulk.reload("countries")` or `bulk.reload()` (all collections)

```javascript
// Bulk initialize
bulk.countries.insert([
  { name: "Turkey", code: "TR", population: 85000000 },
  { name: "Germany", code: "DE", population: 83000000 }
]);

// Querying bulk
var european = bulk.countries.findall(c => c.population > 80000000);

// Substring search (case-insensitive via String.prototype.contains)
var esenDistricts = bulk.postalcodes.findall(x => x.placeName.contains("esen"));

// Prefix check (case-insensitive via String.prototype.startsWith)
var istanbulCodes = bulk.postalcodes.findall(x => x.placeName.startsWith("istan"));
```

## 13. Queue Operations (Background Jobs)

Use `queue()` to schedule scripts for background execution. Direct insertions into the `db.sysqueue` collection (e.g. via `db.sysqueue.insert()`) are restricted and will throw an error. You must use the `queue()` function to add background jobs.

```javascript
// generic queue usage
var jobId = queue("db.logs.insert({ msg: @msg })", { msg: "Hello" }, { priority: 1 });
```

### Parameters
*   **Template**: The JavaScript code to execute. Use `@param` for binding.
*   **Parameters**: Object containing values for `@param` placeholders.
*   **Options**: Object with settings (e.g., `{ priority: 1 }`). Higher priority runs first.

### Queue Record Schema (`db.sysqueue`)
Queued jobs are recorded in the `db.sysqueue` collection with the following fields:
*   `_id`: Job identifier (string).
*   `queryTemplate`: Script template to run (string).
*   `parameters`: Query parameters (object).
*   `options`: Execution options (object).
*   `createdAt`: UTC creation time (DateTime).
*   `executionTime`: UTC start time, `null` if pending (DateTime).
*   `completedAt`: UTC completion time, `null` if pending/running (DateTime).
*   `priority`: Execution priority (int).
*   `duration`: Execution duration in milliseconds (long).
*   `successResult`: Execution result object, `null` on failure (object).
*   `errorMessage`: Failure error message, `null` on success (string).

### Logging
Execution logs are stored in `queue_logs/` directory.

## 14. Utility Functions Reference

### Cryptographic & Encoding
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `md5(str)` | `string -> string` | MD5 hash | `md5("hello")` → `"5d41402abc4b..."` |
| `sha256(str)` | `string -> string` | SHA256 hash | `sha256("hello")` → `"2cf24dba5fb..."` |
| `encrypt(text, salt)` | `string, string -> string` | AES encrypt (Base64 output) | `encrypt("secret", "mykey")` |
| `decrypt(text, salt)` | `string, string -> string` | AES decrypt | `decrypt("enc...", "mykey")` |
| `toBase64(str)` | `string -> string` | Base64 encode | `toBase64("hello")` → `"aGVsbG8="` |
| `fromBase64(str)` | `string -> string` | Base64 decode | `fromBase64("aGVsbG8=")` → `"hello"` |

### Random & ID Generation
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `guid()` | `-> string` | Generate UUID | `guid()` → `"a1b2c3d4-..."` |
| `randomString(len)` | `int -> string` | Random alphanumeric | `randomString(10)` → `"kA9xPm3qZ2"` |
| `randomNumber(min, max)` | `int, int -> int` | Random integer in range | `randomNumber(1, 100)` → `42` |
| `randomDecimal(min, max)` | `string, string -> decimal` | Random decimal in range | `randomDecimal("0.01", "99.99")` |

### AI / LLM Functions
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `openai(url, token)` | `string, string -> LlmClient` | Create LLM client | `var llm = openai("https://api...", "sk-...");` |
| `llm.addSysMsg(msg)` | `string -> void` | Add system message | `llm.addSysMsg("You are a bot");` |
| `llm.msg(user, data, model)` | `string, object, object -> object/string` | Send message | `var res = llm.msg("Hello", {}, null);` |

### String & Conversion
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `toString(obj)` | `object -> string` | Convert any value to string | `toString(42)` → `"42"` |
| `split(text, sep)` | `string, string -> string[]` | Split string by separator | `split("a,b,c", ",")` → `["a","b","c"]` |
| `toDecimal(str)` | `string -> decimal` | Parse string to decimal | `toDecimal("3.14")` → `3.14` |
| `toJson(obj)` | `object -> string` | Serialize object to JSON | `toJson({a:1})` → `'{"a":1}'` |
| `deepcopy(obj)` | `object -> object` | Deep clone an object | `var copy = deepcopy(original)` |
| `csv(input)` | `string <-> object[]` | Bidirectional CSV parser/generator | `var objs = csv("a,b\n1,2")` |

### Date Functions
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `addMinutes(date, n)` | `date, double -> DateTime` | Add N minutes | `addMinutes(new Date(), 30)` |
| `addHours(date, n)` | `date, double -> DateTime` | Add N hours | `addHours(new Date(), 2)` |
| `addDays(date, n)` | `date, double -> DateTime` | Add N days | `addDays(new Date(), 7)` |

### System Functions
| Function | Signature | Description | Example |
|:---|:---|:---|:---|
| `showCollections()` | `-> string[]` | List all collection names | `showCollections()` |
| `queue(script, params, opts)` | `string, object, object -> string` | Schedule background job | `queue("...", {}, {})` |
| `getIndexes(name)` | `string -> object[]` | List indexes for collection | `getIndexes("users")` |
| `console.log(msg)` | `object -> void` | Print to server console | `console.log("debug: " + x)` |
| `alias(source, name)` | `object, string -> object` | Assign alias for joins | `alias(db.users, "u")` |

## 11. Security

### System Collection Protections
To prevent unauthorized modification of core database operations, direct insertions via standard insert queries (e.g. `db.collection.insert(...)`) are strictly prohibited on the following collections:
*   `db.sysqueue`: Insertion must go through the global `queue()` function.
*   `db.sysvaults`: Insertion/updates must go through the `addVault()` / `db.addVault()` function.
*   `db.syslogs`: Read-only virtual collection for querying request, error, and debug logs. Direct insertion or modification is prohibited.

Attempting a direct insert or update on these system collections will throw an `InvalidOperationException`.

### System Log Querying (`db.syslogs` for AI Models & Agents)

> 🤖 **FOR AI MODELS & AGENTS**: When you need to investigate server execution history, diagnose runtime errors, audit past queries, track deleted/modified records, or inspect system performance, **ALWAYS query `db.syslogs`**. Do not search log files on disk manually when a database script query can answer it instantly.

`db.syslogs` is a virtual, read-only collection that aggregates system request logs, error logs, and debug logs. It parses raw log files on demand into structured JSON documents with UUID v7 IDs (`_id`), so queries return results ordered with the newest logs first.

#### Record Schema
Each log record in `db.syslogs` contains the following fields:
* `_id`: String (UUID v7, time-ordered)
* `timestamp`: String (Format: `YYYY-MM-DD HH:mm:ss.fff`)
* `type`: String (`"request"`, `"error"`, or `"debug"`)
* `ip`: String (Client IP address)
* `user`: String (Authenticated username)
* `query`: String (Executed script payload, error message, or debug log)
* `durationMs`: Number (Execution time in milliseconds)
* `status`: String (`"Success"`, `"Failed: <message>"`, `"Error"`, or `"Debug"`)

#### Practical AI Query Examples

```javascript
// 1. Get recent 50 system logs (newest first)
db.syslogs.take(50);

// 2. Search for queries referencing a specific collection or keyword (e.g. "sysvaults" or "delete")
db.syslogs.findall(l => l.query.contains("sysvaults")).take(20);

// 3. Find all failed request executions or error logs
db.syslogs.findall(l => l.type == "error" || l.status.startsWith("Failed"));

// 4. Inspect slow queries taking longer than 100ms
db.syslogs.findall(l => l.durationMs > 100).take(20);

// 5. Audit queries executed by a specific user
db.syslogs.findall(l => l.user == "unlocker" && l.type == "request").take(20);

// 6. Count total logged request entries
db.syslogs.count(l => l.type == "request");
```

> **NOTE**: `db.syslogs` is strictly read-only. Calling `.insert()`, `.update()`, or `.delete()` will throw an `InvalidOperationException`.

### Authentication
*   **Method**: HTTP Basic Auth
*   **Default User**: `unlocker` / `unlocker`
*   **Password Hashing**: Supports SHA256 hashed passwords in the `sysusers` collection
*   **Required for**: `POST /query`, `GET /collections`, private views

### Adding Database Users
```javascript
// Plain text password
db.sysusers.insert({ username: "admin", password: "admin123" });

// SHA256 hashed password (recommended)
db.sysusers.insert({ username: "admin", password: sha256("admin123") });
```

### Query Parameter Safety
Use `@placeholder` parameters to prevent injection:

**❌ UNSAFE — Never concatenate user input:**
```csharp
string script = "db.users.find(u => u.name == '" + userInput + "')"; 
```

**✅ SAFE — Use @param placeholders:**
```bash
# HTTP: POST /query?userName=John
# Body:
db.users.find(u => u.name == @userName);
```

The server replaces `@userName` with the JSON-serialized value of the query parameter, preventing injection.

### Input Validation
AxarDB blocks dangerous patterns: `eval()`, `Function()`, `<script>` tags, and other common injection vectors are automatically rejected.

## 12. API Endpoints

| Endpoint | Method | Description | Auth Required |
|:---|:---|:---|:---|
| `/query` | POST | Execute JavaScript script | ✅ Basic Auth |
| `/collections` | GET | List all collections | ✅ Basic Auth |
| `/views/{name}` | GET | Execute a view | 🔓 Public = No, 🔒 Private = Yes |
| `/docs` | GET | Documentation page | ❌ No |

### Curl Examples
```bash
# Execute a query
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d "db.users.findall()"

# Query with safe parameters
curl -X POST "http://localhost:5000/query?ageLimit=20" \
     -u "unlocker:unlocker" \
     -d "db.users.findall(u => u.age > @ageLimit)"

# Insert data
curl -X POST "http://localhost:5000/query" \
     -u "unlocker:unlocker" \
     -d 'db.users.insert({ name: "Alice", age: 28 })'

# Access public view (no auth)
curl "http://localhost:5000/views/activeUsers"

# Access public view with parameters
curl "http://localhost:5000/views/login?email=test@test.com&password=mypass&deviceid=DEV001"

# Access private view (with auth)
curl -u "unlocker:unlocker" "http://localhost:5000/views/myPrivateView"
```

## 13. Web Management Console

AxarDB includes a built-in web console at `http://localhost:5000`:

- **Monaco Editor**: Write and execute JavaScript queries with syntax highlighting
- **Tab System**: Open multiple query tabs, each with its own editor and results
- **Sidebar**: Browse collections, views, and triggers; click to interact
- **Results Grid**: Sortable, filterable, column-resizable data table with JSON/CSV export
- **Query History**: Access previous queries with search/filter, stored in localStorage
- **Context Menu**: Right-click on sidebar items for Edit/Delete, or on result rows for Update/Delete
- **HTML Rendering**: Non-array results (strings, objects, HTML) render in an iframe

## 14. .NET SDK (C#)

```csharp
using AxarDB.Sdk;

// Initialize
using var client = new AxarClient("http://localhost:5000", "unlocker", "unlocker");

// Insert
await client.InsertAsync("users", new { Name = "John", Age = 30 });

// Query with parameters
var script = "db.users.findall(u => u.name == @name)";
var users = await client.QueryAsync<List<User>>(script, new { name = "John" });

// Update
await client.UpdateAsync("users", "u => u.name == 'John'", new { Age = 31 });

// Builder pattern
var count = await client.Collection<User>("users").Where("age", ">", 18).CountAsync();
var first = await client.Collection<User>("users").FirstAsync();

// View management
await client.CreateViewAsync("ActiveUsers", "return db.users.findall(u => u.active)");
var list = await client.CallViewAsync<List<User>>("ActiveUsers");
var filtered = await client.CallViewAsync<List<User>>("myview", new { minAge = 18 });

// Vault
await client.AddVaultAsync("MY_SECRET", "12345");
```

## 15. Python SDK

```python
from axardb import AxarClient

client = AxarClient("http://localhost:5000", "unlocker", "unlocker")

# Insert
client.insert("users", {"name": "John", "age": 30})

# Query
users = client.collection("users").where("age", ">", 20).to_list()

# Count & Delete
count = client.collection("users").count()
client.collection("users").where("age", "<", 18).delete()

# View management
client.create_view("MyView", "return db.users.take(10)")
res = client.call_view("MyView")
res = client.call_view("MyView", {"minAge": 18})
```

## 16. CLI Tool

```bash
# Interactive login
./AxarDB.Cli -s "db.users.count()"

# With file input
./AxarDB.Cli -f query.js

# Fully automated (CI/CD)
./AxarDB.Cli -u admin -p pass -f query.js -o result.json

# CORS Configuration
# By default, AxarDB allows all origins (*). 
# You can restrict it using the --cors parameter.
dotnet run -- --cors "http://localhost:3000,http://example.com"

# Configuration Parameters
# Database configuration settings are stored in the sysconfig system collection.
# They are initialized with default values during database creation.
# Authorized users can update the settings via: db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
# Settings require a server restart to take effect.
# Direct inserts into sysconfig are blocked.
#
# Properties in sysconfig:
# - memoryLimitPercentage    : Memory limit percentage (default: 0.4)
# - bulkStoreMaxCacheBytes   : Bulk store max cache in bytes (default: 52428800)
# - maxRecursionDepth        : Max script recursion depth (default: 100)
# - queryTimeoutMinutes      : Max query timeout in minutes (default: 10)
# - queuePollIntervalSeconds : Background queue poll interval in seconds (default: 1.0)
#
# Sys-Prefix Protection:
# Collection names starting with "sys" are reserved for system use.
# Only sysusers, sysqueue, sysvaults, sysconfig, and syslogs are allowed.
# Creating db.sysnew or similar will throw InvalidOperationException.
# This protection is enforced at Bridge, Engine, and Collection layers.

# Bootstrap Refactoring (Clean Code)
# Program.cs is kept extremely simple. The entire application setup and configuration
# orchestrations are managed by AxarDB.Bootstrap.AppBootstrap.Run(args).
# Custom middlewares (exception handling, request logging, auth) are extracted as C# classes.

# Show collections
./AxarDB.Cli --show-collections

# Insert
./AxarDB.Cli --insert users "{\"name\":\"Alice\"}"
```

## 17. Multi-Engine Benchmark Tool

AxarDB ships with a Python-based benchmark tool (`compare.py`) that measures AxarDB (db/memory/bulk) against PostgreSQL, MariaDB, and MongoDB. It generates an interactive HTML report (`output.html`) with Chart.js visualizations.

### Running the Benchmark
```bash
python compare.py
```

### What It Tests
- Setup (DDL), Single Insert, Bulk Insert, Index Creation
- Count (COUNT), Filter Query, Range Query, Aggregation (avg age), Update, Delete

### Key Details
- AxarDB times are measured server-side via `sysqueue` Stopwatch, excluding HTTP overhead.
- Filter and Range queries use the indexed `age` column across all engines.
- The report includes a feature comparison table highlighting AxarDB-unique capabilities.
- On startup, the benchmark automatically raises `queryTimeoutMinutes` in `sysconfig` to 30 minutes to prevent script cancellation during long-running workloads.
- The speedup row label in both the HTML table and chart is **"How many times faster is AxarDB (memory)?"**, comparing total operation times across all engines against AxarDB (memory) as the baseline.
- The benchmark awaits queue job completion with a 300-second timeout per operation.

### Report Sections
- **Engine Status**: Lists each engine with OK or error status.
- **Operation Times (ms)**: Per-operation timing table.
- **How many times faster is AxarDB (memory)?**: Speedup comparison chart.
- **Advanced Features Comparison**: Feature matrix showing AxarDB-unique capabilities.
- **Test Configuration**: Connection details and methodology notes.


## 18. Common Patterns & Troubleshooting

### Frequent Mistakes

| Mistake | Fix |
|:---|:---|
| `db.users.findall()` | Returns a ResultSet (iterable directly) |
| `db.view("name", { param: "value" })` for parameterless view | Omit the second argument: `db.view("name")` |
| Forgetting `// @access public` in view | Always add access comment as the first line |
| Using single `=` instead of `==` in predicates | Use `==` for comparison: `u => u.age == 25` |
| Assuming `.contains()` doesn't exist on strings | AxarDB injects `String.prototype.contains()` — it works for case-insensitive substring search |
| Using `.contains()` for exact match | Use `==` for exact match; `.contains()` is for substring search |

### Common Query Patterns
```javascript
// Pagination — skip + take pattern
var page1 = db.products.findall().take(10);          // Page 1 (records 1-10)
var page2 = db.products.findall().skip(10).take(10); // Page 2 (records 11-20)
var page3 = db.products.findall().skip(20).take(10); // Page 3 (records 21-30)

// Aggregation by iterating
var total = 0;
db.orders.findall(o => o.status == "completed").foreach(o => {
    total += o.amount;
});
return { totalRevenue: total };

// Check if collection has any data
var hasUsers = db.users.findall().count() > 0;

// Search by ID
var user = db.users.find(u => u._id == "some-id");

// Multi-field update
db.users.update(u => u._id == "abc", { 
    name: "Updated Name", 
    age: 30, 
    updatedAt: new Date() 
});

// Create a token
var token = encrypt(guid(), $MY_SALT);

// Hash a password
var hashed = sha256("plaintext_password");

// Case-insensitive substring search (AxarDB String.prototype.contains)
var devUsers = db.users.findall(u => u.email.contains("dev"));

// Case-insensitive prefix check (AxarDB String.prototype.startsWith)
var adminMails = db.users.findall(u => u.email.startsWith("admin@"));

// Bulk substring search
var esenPlaces = bulk.postalcodes.findall(x => x.placeName.contains("esen"));
```

### Troubleshooting
*   **401 Unauthorized**: Check credentials. Default is `unlocker:unlocker`.
*   **SyntaxError**: Missing closing parenthesis `)`, mismatched braces `{}`, or unclosed quotes.
*   **Variable not found**: Check if you misspelled a property name in the predicate.
*   **Case Sensitivity**: Use `.contains()` on string fields for case-insensitive substring search (AxarDB custom extension), or `db.collection.contains(predicate)` for case-insensitive exact match. Standard `.includes()` is case-sensitive.
*   **View 404**: Verify view name exactly matches. Check spelling.
*   **View returns error with parameters**: Ensure HTTP query string keys match `@param` names in the view script exactly.

### Backup
Copy the `Data/` folder to a safe location. All collections and their documents are stored there as files.
