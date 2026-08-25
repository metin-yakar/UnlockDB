# AxarDB .NET SDK

The official .NET SDK for AxarDB, allowing seamless integration with Web, Console, Desktop, and Mobile applications.

## Installation

Add the `AxarDB.Sdk` project to your solution or reference the DLL.

## Usage

### Initialization

```csharp
using AxarDB.Sdk;

// Initialize the client
using var client = new AxarClient("http://localhost:5000", "unlocker", "unlocker");
```

### Basic Querying

You can execute raw JavaScript queries just like in the AxarDB shell.

```csharp
// Execute a script and get the result
var users = await client.QueryAsync<List<User>>("db.users.findall()");

// Execute a script without return value
await client.ExecuteAsync("db.users.delete(u => u.isActive == false)");
```

### Parameterized Queries (Secure)

To prevent injection attacks, use the `parameters` object. The SDK handles proper encoding.

```csharp
var minAge = 18;
var name = "John";

// The script uses @paramName placeholders
var script = "db.users.findall(u => u.age >= @minAge && u.name == @name)";

var results = await client.QueryAsync<List<User>>(script, new { minAge, name });
```

### Query Builder (Fluent API)

Use the `AxarQueryBuilder` to construct queries safely without raw strings.

```csharp
// Get a builder for the 'users' collection
var builder = client.Collection<User>("users");

// Build a query: Find users older than 18, take 10
var users = await builder
    .Where("age", ">", 18)
    .Take(10)
    .ToListAsync();

// Complex where clause with parameters
var activeUsers = await builder
    .Where("status == @status && loginCount > @minLogins", new { status = "active", minLogins = 5 })
    .ToListAsync();
```

### Rate Limiting

The SDK includes a built-in client-side rate limiter. You can configure limits and check them before executing queries.

1. **Configure Limits**:
   Define the maximum number of requests allowed for a specific limit type.

   ```csharp
   // Allow 100 requests for 'ip_ratelimit'
   client.ConfigureRateLimit("ip_ratelimit", 100);
   ```

2. **Execute with Rate Limit**:
   Use `QueryWithRateLimitAsync`.

   ```csharp
   try 
   {
       // Check against 'ip_ratelimit' for IP '192.168.1.1' within a '1h' window.
       // The 'condition' parameter (e.g., "warning") is optional.
       var result = await client.QueryWithRateLimitAsync<List<User>>(
           script: "db.users.findall()", 
           parameters: null, 
           limitKey: "192.168.1.1", 
           limitDuration: "1h", 
           limitType: "ip_ratelimit",
           limitCondition: "warning"
       );
   }
   catch (Exception ex)
   {
       // Handle rate limit exception
       Console.WriteLine(ex.Message); // "Rate limit exceeded..."
   }
   ```

### Typed Helper Methods

The SDK provides helper methods for common operations to avoid writing raw JavaScript strings for simple tasks.

```csharp
// Insert
var newUser = new User { Name = "Alice", Age = 25 };
await client.InsertAsync("users", newUser);

// Find All
var allUsers = await client.FindAllAsync<User>("users");

// Find with Predicate
var adults = await client.FindAllAsync<User>("users", "u => u.age > 18"); // Note: Predicate is a string
```

### Error Handling

The SDK throws an exception if the AxarDB server returns a non-success status code (e.g., 401 Unauthorized, 500 Internal Server Error).

```csharp
try 
{
    await client.QueryAsync<object>("invalid script");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Examples

### Console Application

```csharp
using AxarDB.Sdk;

class Program
{
    static async Task Main(string[] args)
    {
        using var client = new AxarClient("http://localhost:5000", "unlocker", "unlocker");
        
        var serverTime = await client.QueryAsync<DateTime>("new Date()");
        Console.WriteLine($"Server Time: {serverTime}");
    }
}
```

### ASP.NET Core Web API (Service Injection)

1. Register the client in `Program.cs`:

```csharp
builder.Services.AddScoped<AxarClient>(sp => 
    new AxarClient("http://localhost:5000", "unlocker", "unlocker"));
```

2. Inject and use in Controller:

```csharp
[ApiController]
[Route("[controller]")]
public class UsersController : ControllerBase
{
    private readonly AxarClient _db;

    public UsersController(AxarClient db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var users = await _db.FindAllAsync<User>("users");
        return Ok(users);
    }
}
```

### MAUI / Xamarin

In your `MauiProgram.cs` or `App.xaml.cs`:

```csharp
// Register as singleton or transient
builder.Services.AddSingleton<AxarClient>(new AxarClient("http://10.0.2.2:5000", "unlocker", "unlocker"));
// Note: Use 10.0.2.2 for Android Emulator to access localhost
```

## License

MIT
