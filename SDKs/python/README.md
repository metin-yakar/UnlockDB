# AxarDB Python SDK

The official Python SDK for AxarDB, a high-performance in-memory NoSQL database.

## Installation

```bash
pip install axardb-sdk
```

## Quick Start

```python
from axardb import AxarClient

# Initialize Client
client = AxarClient("http://localhost:1111", "admin", "admin")

# Insert Data
client.insert("users", {"name": "Alice", "age": 25, "active": True})

# Query Data
users = client.collection("users").where("age", ">", 20).to_list()
print(users)

# Execute Raw Query
client.execute("db.users.findall(x => x.active).delete()")
```

## Features

### Fluent Query Builder

```python
count = client.collection("users") \
    .where("active", "==", True) \
    .count()

first_user = client.collection("users") \
    .where("name", "==", "Alice") \
    .first()

# Select Projection
names = client.collection("users") \
    .where("age", ">", 18) \
    .select("x => x.name") \
    .to_list()

# Update
client.collection("users") \
    .where("status", "==", "old") \
    .update({"status": "archived"})

# Delete
client.collection("users") \
    .where("status", "==", "deleted") \
    .delete()
```

### Rate Limiting

Configure client-side rate limiting to prevent overwhelming the server.

```python
# Limit 'ip' based requests to 100 per hour
client.configure_rate_limit("ip", 100)

try:
    # Check limit before query
    client.query_with_rate_limit(
        "db.users.count()", 
        parameters=None, 
        limit_key="192.168.1.1", 
        limit_duration="1h", 
        limit_type="ip"
    )
except Exception as e:
    print("Rate limit exceeded:", e)
```

### Advanced Management

```python
# Create View
client.create_view("ActiveUsers", "return db.users.findall(u => u.active)")

# Call View
active_users = client.call_view("ActiveUsers")

# Create Trigger
client.create_trigger("LogUser", "users", "console.log('User changed: ' + event.documentId)")

# Secure Vault
client.add_vault("API_SECRET", "xyz-123")

# Indexing
client.create_index("users", "u => u.email")

# User Management
client.create_user("new_user", "password123")

# Joins
result = client.join("users", "orders", "x => x.userId == x.customerId")
```
