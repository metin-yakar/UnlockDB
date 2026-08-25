# AxarDB CLI Tool

A cross-platform command-line interface for interacting with AxarDB.

## Installation

You can run this tool without installation using `dotnet run` from the source, or publish it as a standalone executable.

### Build & Publish

```bash
# Windows
dotnet publish -c Release -r win-x64 -o ./publish/win --self-contained

# Linux
dotnet publish -c Release -r linux-x64 -o ./publish/linux --self-contained

# macOS
dotnet publish -c Release -r osx-x64 -o ./publish/osx --self-contained
```

## Usage

### Interactive Mode
If you run the tool without providing credentials, it will prompt you:

```bash
./AxarDB.Cli -s "db.users.count()"
# Prompts for Host, User, Password...
```

### Run Inline Script
```bash
./AxarDB.Cli -h http://localhost:1111 -u admin -p admin -s "db.users.findall()"
```

### Run Script from File (Interactive Auth)
You can provide just the script file, and the CLI will prompt for connection details. This is useful for running saved queries securely without typing passwords in the command line.

```bash
./AxarDB.Cli -f query.js
# Prompts for Host, User, Password...
```

### Run Script from File (Automated)
```bash
# query.js: db.users.where(u => u.active)
./AxarDB.Cli -h http://localhost:1111 -u admin -p admin -f query.js
```

### Save Output to File
```bash
./AxarDB.Cli -h http://localhost:1111 -u admin -p admin -s "db.users.count()" -o result.json
```

## Arguments
- `-h, --host`: Server URL
- `-u, --user`: Username
- `-p, --pass`: Password
- `-f, --file`: Path to script file
- `-s, --script`: Inline script string
- `-o, --out`: Output file path
