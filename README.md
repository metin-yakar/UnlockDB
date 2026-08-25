![AxarDB Logo](./AxarDBLogo.png)

[![License: Custom](https://img.shields.io/badge/License-Metin_YAKAR-blue.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker&logoColor=white)](Dockerfile)
[![Built With .NET 8](https://img.shields.io/badge/Built_With-.NET_8.0-512BD4.svg?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![JavaScript Engine](https://img.shields.io/badge/Engine-Jint-f7df1e.svg?logo=javascript&logoColor=black)](https://github.com/sebastienros/jint)

> **AxarDB** is a high-performance, in-memory NoSQL database server that allows you to write database queries directly in **JavaScript**. Built on ASP.NET Core 8.0, it combines the flexibility of a document store with the power of a full JavaScript runtime.

---

## 🌍 Languages

| [English](README.md) | [Türkçe](Docs/README.tr.md) | [Русский](Docs/README.ru.md) | [中文](Docs/README.zh.md) | [Deutsch](Docs/README.de.md) | [日本語](Docs/README.ja.md) | [العربية](Docs/README.ar.md) | [Nederlands](Docs/README.nl.md) | [Български](Docs/README.bg.md) | [Italiano](Docs/README.it.md) | [Español](Docs/README.es.md) |
|---|---|---|---|---|---|---|---|---|---|---|

---

![AxarDB Web UI](./image.png)

## 🚀 Key Features

| Feature | Description |
|:---|:---|
| **📜 JavaScript Querying** | Use full JS structure: `db.users.findall(x => x.active)`. Supports prototype extensions like `count()`, `distinct()`, `orderBy()`, `orderByDesc()`, `max()`, and `min()`. |
| **🆔 UUID v7 Support** | Native RFC 9562 UUID v7 is the default `_id` scheme. Generates sortable IDs and supports `guidv7()`, `guidv7(datetime)` and `guidv7CreatedAt(id)`. |
| **⚡ High Performance** | In-memory storage with `ConcurrentDictionary`, lazy evaluation using PLINQ, and strictly-capped dynamic 40% Memory Cache expiration. |
| **🧠 Memory Store** | `memory.sessions.insert({...})` with TTL support for sessions, caches, and short-lived data. |
| **💾 Bulk Store** | High-performance JSONL static storage with LRU caching and memory-bounded chunked queries for large files. |
| **📄 CSV Engine** | Bidirectional robust CSV support. Convert text to Collections or Collections to CSV via `csv(input)`. |
| **🔍 Smart Indexing** | Create ASC/DESC indexes on any field. Supports optimized range queries. |
| **🔗 Joins** | Perform complex joins with easy aliases: `db.join(alias(u, "user"), alias(o, "order"))`. |
| **🛢️ MySQL/MariaDB** | Native support for external SQL queries: `mysqlRead(conn, query)` and `mysqlExec(conn, query)`. |
| **🐘 PostgreSQL** | Native support for PostgreSQL queries: `pgsqlRead(conn, query)` and `pgsqlExec(conn, query)`. |
| **⏳ Task Queue** | Background job processing with `queue("script", params, { priority: 1 })`. Tracks completion with a `completedAt` timestamp. Direct insertion to `db.sysqueue` is restricted. |
| **👁️ Views** | Stored server-side queries with `@access public/private` metadata and `@param` parameter injection. |
| **⚡ Triggers** | Automatic event handlers on data changes with `@target` filtering. |
| **🔐 Vaults** | Secure key-value storage for API keys using `$KEY` syntax. Direct insertion to `db.sysvaults` is restricted; use `addVault()`. |
| **🌐 Webhooks & HTTP** | HTTP POST with `webhook()` and HTTP GET with `httpGet()`, both with custom headers. |
| **🛡️ Secure** | Basic Authentication (supports SHA256 hashing), **Injection Prevention** via `@placeholder` replacement, and **Sys-prefix Protection** blocking custom `sys*` collection names. |
| **🛡️ Data Recovery** | Automatic reverse query generation in `backup_queries` for state-changing operations to prevent accidental data loss (Fail-safe). |
| **🐳 Docker Ready** | Runs anywhere with a single `docker run` command. |
| **🖥️ Management Console** | Web UI with Monaco Editor, **Tab System**, **Query History**, **Smart View Click** with `@param` detection, Resizable Grid, and Dark Mode. |
| **📚 Documentation** | Built-in docs page (`/docs`) with sidebar navigation and search. |

---

## 🏎️ Performance Benchmarks

Unlike traditional databases that require complex protocols, AxarDB executes your logic on the server.

### Multi-Engine Benchmark Results (1000 records)

> 📊 **See the full interactive report: [output.html](output.html)** — includes Chart.js visualizations and a feature comparison matrix.

![AxarDB Web UI](./axarchart.png)

| Operation | AxarDB (memory) | AxarDB (db) | AxarDB (bulk) | PostgreSQL | MariaDB | MongoDB |
|:---|---:|---:|---:|---:|---:|---:|
| Setup (DDL) | 0 ms | 0 ms | 0 ms | 15.81 ms | 41.61 ms | 0.29 ms |
| Single Insert | 1.00 ms | 2.00 ms | 1.00 ms | 0.22 ms | 2.99 ms | 0.44 ms |
| Bulk Insert | 17.00 ms | 948.00 ms | 8.00 ms | 199.14 ms | 13.11 ms | 6.06 ms |
| Index Creation | — | 12.00 ms | — | 6.39 ms | 50.19 ms | 26.58 ms |
| Count (COUNT) | 0.05 ms | 4.82 ms | 0.02 ms | 0.45 ms | 0.34 ms | 0.81 ms |
| Filter Query | 0.08 ms | 0.22 ms | 0.06 ms | 0.37 ms | 0.47 ms | 0.78 ms |
| Range Query | 0.08 ms | 5.28 ms | 0.06 ms | 0.92 ms | 4.49 ms | 1.96 ms |
| Aggregation (avg age) | 0.05 ms | 0 ms | 0 ms | 0.36 ms | 0.29 ms | 1.39 ms |
| Update | — | 175.00 ms | — | 1.79 ms | 3.25 ms | 2.05 ms |
| Delete | 3.00 ms | 147.00 ms | — | 2.43 ms | 4.45 ms | 7.23 ms |
| **Total** | **21.26 ms** | **1294.32 ms** | **9.14 ms** | **227.89 ms** | **121.20 ms** | **47.59 ms** |
| **How many times faster is AxarDB (memory)?** | — | 60.88x slower | 0.43x faster | 10.72x slower | 5.70x slower | 2.24x slower |

> **Methodology:** Each engine received an identical workload. AxarDB times exclude HTTP overhead via server-side `Stopwatch` through `sysqueue`. PostgreSQL/MariaDB/MongoDB were measured with native drivers. Filter and Range queries used an indexed `age` column. The `memory` and `bulk` stores lack update/index APIs (shown as —). Results vary by hardware and load. Run `python compare.py` to regenerate with current data.

### Feature Comparison Matrix

| Feature | AxarDB | PostgreSQL | MariaDB | MongoDB |
|:---|:---:|:---:|:---:|:---:|
| JavaScript-based server-side query language | ✅ | ❌ | ❌ | Limited |
| Built-in task queue (queue / sysqueue) | ✅ | ❌ | ❌ | ❌ |
| Multiple stores in one system (db / memory / bulk) | ✅ | ❌ | ❌ | ❌ |
| Native HTTP REST API | ✅ | ❌ | ❌ | ❌ |
| Embedded mode | ✅ | ❌ | ❌ | ❌ |
| In-memory store (TTL-based) | ✅ | ❌ | ❌ | ❌ |
| Bulk (JSONL chunk) store | ✅ | ❌ | ❌ | ❌ |
| Schemaless document model | ✅ | ❌ | ❌ | ✅ |
| Built-in authentication (Basic Auth) | ✅ | ❌ | ❌ | ❌ |
| SQL compatibility | ❌ | ✅ | ✅ | ❌ |

*Benchmark run on standard workstation. Actual performance depends on hardware.*


---

## 🐳 Quick Start with Docker

Get up and running in seconds:

```bash
# Default (Port 5000)
docker run -d -p 5000:5000 -v $(pwd)/data:/app/data --name AxarDB AxarDB:latest

# Custom Port and CORS (e.g., 5001, allowing local 3000)
docker run -d -p 5001:5001 -v $(pwd)/data:/app/data --name AxarDB AxarDB:latest -- -p 5001 --cors "http://localhost:3000"
```

Or using `dotnet run` for development:
```bash
dotnet run -- -p 5001 --cors "*"
```

Or using `docker-compose`:
```yaml
services:
  AxarDB:
    build: .
    ports: ["5000:5000"]
    volumes: ["./data:/app/data"]
```

---

## ⚙️ Configuration

AxarDB configuration settings are stored inside the database in a dedicated system collection named `sysconfig`. During the initial database setup, this collection is populated automatically with default configuration values. 

To modify settings, authorized users can update the document in the `sysconfig` collection. The new settings will take effect after restarting the database application. Direct insert operations on the `sysconfig` collection are not allowed.

### System Collection Protection

All collection names starting with `sys` are **reserved** for internal infrastructure. Only pre-defined system collections are allowed: `sysusers`, `sysqueue`, `sysvaults`, `sysconfig`, and `syslogs`. Attempting to create or access any other `sys`-prefixed collection (e.g., `db.sysnew`) will throw an `InvalidOperationException`. This protection is enforced at multiple layers (Bridge, Engine, and Collection).

### Available Settings

| Property | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| `memoryLimitPercentage` | `double` | `0.4` | Caps database memory usage (e.g., `0.3` for 30%). |
| `bulkStoreMaxCacheBytes` | `long` | `52428800` (50MB) | Maximum size of bulk cache in bytes. |
| `maxRecursionDepth` | `int` | `100` | Limits recursion depth of script executions. |
| `queryTimeoutMinutes` | `int` | `10` | Caps runtime query execution time in minutes. |
| `queuePollIntervalSeconds` | `double` | `1.0` | Controls polling frequency of the background queue. |

### Configuration Example
To change settings via query console (requires server restart to apply):
```javascript
db.sysconfig.update(x => true, { queryTimeoutMinutes: 15 });
```

---

## 🛡️ Data Recovery (Fail-safe Backup)

AxarDB includes a fail-safe data recovery mechanism. Every state-changing operation (`insert`, `update`, `delete`, `drop`, `saveView`, `deleteTrigger`, etc.) across the `db`, `bulk`, and `sys` collections automatically generates a reverse query. For example, an `insert` operation will generate a corresponding `delete()` query to easily undo the addition.

These recovery queries are appended sequentially into a daily file (`backup_queries/YYYY-MM-DD.txt`).
If an accidental deletion or modification occurs, you can execute these queries to restore your data to its previous state. The feature operates in a silent fail-safe wrapper, ensuring that any I/O errors during backup logging never interrupt the main database transaction.

---

## 💾 Bulk Store (JSONL for Large Static Datasets)

The `bulk` object manages high-speed read/write on static datasets stored as JSON Lines (JSONL) files in the `Bulk/` folder. Designed for zip codes, countries, log exports, and other large but stable data.

**Key Features:**
- **LRU Memory Cache:** Caches records up to the configurable `bulkStoreMaxCacheBytes` limit (default: 50 MB), evicting least recently used collections.
- **Memory-Bounded Chunked Queries:** Filtered queries process large JSONL files line-by-line in chunks. A temporary lightweight index is built per chunk using only the predicate-referenced fields, keeping memory safely bounded.
- **Auto-Reload:** FileSystemWatcher detects disk changes and refreshes the cache automatically.

```javascript
// Bulk insert an array of objects
bulk.countries.insert([
  { name: "Turkey", code: "TR", population: 85000000 },
  { name: "Germany", code: "DE", population: 83000000 }
]);

// Find a record (uses LRU Cache)
var tr = bulk.countries.find(c => c.code == "TR");

// Query with filtering
var large = bulk.countries.findall(c => c.population > 1000000);

// Manually reload cache
bulk.reload("countries");
```

---

## ⚙️ Built-in Functions

AxarDB provides powerful utility functions that can be used directly within your scripts and views.

### 🧩 Object and Array Operations

**Object.prototype.includes(arr)**
This function checks if an item exists within the specified array, similar to C#'s LINQ `Contains` or `SequenceEqual` logic.
- If the item being compared is a primitive value (e.g., int, string), it acts like `Contains`.
- If the item being compared is an array itself, it acts like `SequenceEqual`, performing a strict equality check on both arrays.

```javascript
// 1. Single Value Check (Contains Logic)
// Is the rowNumber one of the specified values?
var targetCats = [66, 69, 74];
var myCats = db.categories.findall(x => x.rowNumber != null && x.rowNumber.includes(targetCats));

// 2. Array Comparison (SequenceEqual Logic)
// Find users where the tags array exactly matches ["developer", "senior"]
var targetTags = ["developer", "senior"];
var matches = db.users.findall(x => x.tags.includes(targetTags));
```

---

## 🆔 UUID v7 Support

AxarDB implements native **RFC 9562** compliant UUID v7 as its default primary key (`_id`) generation scheme. This provides sequentially ordered, time-sortable IDs that perform exceptionally well with indexing and database partitioning, while maintaining full backward compatibility with older UUID v4 keys.

### Global Functions:
- `guidv7()`: Generates a new UUID v7 using the current UTC time.
- `guidv7(datetime)`: Generates a new UUID v7 with the specified date/time string.
- `guidv7CreatedAt(guid)`: Extracts the creation date of a v7 GUID as a UTC DateTime.
- `guid()`: Generates a standard UUID v4 (backward compatibility).

### Usage Example:
```javascript
// Check creation date of a document directly in queries
var product = db.products.find(p => p.name == "Super Phone 0");
if (product) {
    var creationDate = guidv7CreatedAt(product._id);
    console.log("Product created at: " + creationDate);
}
```

---

## 🛠️ CLI Tool

AxarDB comes with a powerful CLI tool (`AxarDB.Cli`) for managing your database from the command line.

### Basic Usage

```bash
dotnet run --project SDKs/cli/AxarDB.Cli -- --host http://localhost:5000 --user admin --pass admin
```

### Commands

| Command | Description | Example |
| :--- | :--- | :--- |
| `--show-collections` | Lists all collections in the database. | `--show-collections` |
| `--insert <col> <json>` | Inserts a JSON document into a collection. | `--insert users "{\"name\":\"Alice\"}"` |
| `--select <col> <sel>` | Projects data using a selector expression. | `--select users "x => x.name"` |
| `--script <js>` | Executes a raw JavaScript query. | `--script "db.users.count()"` |
| `--file <path>` | Executes a JavaScript file. | `--file ./query.js` |

---

## 📦 Client SDKs

Official SDKs are available for C# and Python, offering fully typed async support.

### C# SDK Features
- `InsertAsync<T>`: Asynchronously insert strongly-typed objects.
- `ShowCollectionsAsync`: Get a list of all collections.
- `SelectAsync<TResult>`: Project and retrieve data asynchronously.
- `RandomStringAsync(int len)`: Generate random strings using server-side function.

### Python SDK Features
- `insert_async`: Thread-safe async insertion.
- `show_collections_async`: Async collection listing.
- `select_async`: Async projection.
- `random_string_async(len)`: Async random string helper.

### Using SDKs & Models
To ensure data consistency and unique IDs, your data models should inherit from `AxarBaseModel`.

**C# Example:**
```csharp
public class MyUser : AxarBaseModel
{
    public string Name { get; set; }
}
// ID is automatically generated
await client.InsertAsync("users", new MyUser { Name = "Alice" });
```

**Python Example:**
```python
from axardb import AxarBaseModel

class MyUser(AxarBaseModel):
    def __init__(self, name):
        super().__init__()
        self.name = name

# ID is automatically generated
await client.insert_async("users", MyUser("Alice"))
```


### Parameterized View Call

**C# Example:**
```csharp
// Create a view
await client.CreateViewAsync("myview", "db.users.findall(x => x.age > @minAge)");

// Call the view with parameters
var users = await client.CallViewAsync<User[]>("myview", new { minAge = 18 });
```

**Python Example:**
```python
# Create a view
client.create_view("myview", "db.users.findall(x => x.age > @minAge)")

# Call the view with parameters
users = client.call_view("myview", { "minAge": 18 })
```

---

## 👨‍💻 Developer

**Metin YAKAR**  
*Software Developer & .NET Expert*  
Istanbul, Turkey 🇹🇷

Experience **since 2011** in C# and software architecture. Metin specializes in building high-performance systems and innovative developer tools.

[![LinkedIn](https://img.shields.io/badge/LinkedIn-Connect-0077B5?style=for-the-badge&logo=linkedin)](https://www.linkedin.com/in/metin-yakar/)

---

## 🤝 Support & Contribution

We are looking for contributors to help build the future of AxarDB!
**Areas we need help with:**
- [ ] Real-time Synchronization
- [ ] Cluster Monitoring Dashboard
- [ ] Client SDKs (Node.js, Python, Go)
- [ ] Data Replication & Sharding

### 💖 Support the Project

If you love this project, consider supporting its development!

| **Buy Me a Coffee** | **Ethereum** |
|:---:|:---:|
| <a href="https://buymeacoffee.com/metinyakar"><img src="./buymecoffie.png" style="width:100px;height:100px;"/></a> | ![QR](https://api.qrserver.com/v1/create-qr-code/?size=100x100&data=0x1245CEB6c9A5485286975eEC1B2F9D496016761C) |

---

## 📄 License
**Open Source (Restricted)** - You can use, modify, and learn from AxarDB. However, you are **not allowed** to clone the repository to release a competing standalone database product. See [LICENSE](LICENSE) for details.
