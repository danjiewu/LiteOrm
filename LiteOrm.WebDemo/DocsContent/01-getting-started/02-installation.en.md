# Installation and Environment Requirements

This document covers the runtime environment, database support, and installation methods for LiteOrm.

> **Beginner tip**: If you just want to quickly try out LiteOrm, we recommend using SQLite—it requires no database server installation and works out of the box. See the SQLite quick-start steps at the end of this document.

## Environment Requirements

- `.NET 8.0+`
- `.NET Standard 2.0` (compatible with .NET Framework 4.6.1+)
- Dependencies: `Autofac.Extensions.DependencyInjection`, `Autofac.Extras.DynamicProxy`, `Castle.Core`

> **How to check your .NET version?** Run `dotnet --version` in a terminal. Make sure the output is `8.0.x` or higher. If not installed, visit [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).

## Supported Databases

- SQL Server 2012+
- MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

> For older database versions where default pagination syntax is incompatible, refer to [Custom Paging](../03-advanced-topics/05-custom-paging.md) and [Custom SqlBuilder / Dialect Extension](../04-extensibility/03-custom-sqlbuilder.md).

### Database Provider Reference Table

> When configuring `appsettings.json`, the `Provider` field requires the fully qualified type name of the database driver. Here are the common configurations:

| Database | NuGet Package | Provider Value |
|--------|----------|----------------|
| SQL Server | `Microsoft.Data.SqlClient` | `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient` |
| SQL Server (legacy) | `System.Data.SqlClient` | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| MySQL | `MySqlConnector` | `MySqlConnector.MySqlConnection, MySqlConnector` |
| MySQL (legacy) | `MySql.Data` | `MySql.Data.MySqlClient.MySqlConnection, MySql.Data` |
| PostgreSQL | `Npgsql` | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

> **Note**: In addition to the `LiteOrm` package, you also need to install the corresponding NuGet driver package for your database (as shown in the first column above).

## Install from NuGet

```bash
dotnet add package LiteOrm
```

### Complete Installation Commands by Database

**SQL Server project:**
```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.SqlClient
```

**MySQL project:**
```bash
dotnet add package LiteOrm
dotnet add package MySqlConnector
```

**PostgreSQL project:**
```bash
dotnet add package LiteOrm
dotnet add package Npgsql
```

**SQLite project (recommended for beginners):**
```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite
```

## Creating a New Project from Scratch

> Here are the complete commandsoodo create an ASP.NET Core project with LiteOrm from scratch:

```bash
# 1. Create a Web API project
dotnet new webapi -n MyLiteOrmApp
cd MyLiteOrmApp

# 2. Install LiteOrm (using SQLite as an example)
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite

# 3. Run the project to verify the environment
dotnet run
```

> If you use Visual Studio, you can search for `LiteOrm` in "Manage NuGet Packages" to install it.

## Next Steps After Installation

1. Prepare connection strings and data source configuration.
2. Call `RegisterLiteOrm()` during host startup.
3. Define entities, services, or DAOs.
4. Use `SearchAsync`, `InsertAsync`, and other APIs to complete the first example.

> **SQLite quick start**: If you want to try SQLite quickly, the connection string is simply `Data Source=myapp.db`—no database server needed. See the [First End-to-End Example](./04-first-example.en.md) for a complete walkthrough.

## Common Installation Issues

### Build error after installation: `RegisterLiteOrm` method not found

Make sure you installed the `LiteOrm` package (not `LiteOrm.Common`), and add `using LiteOrm;` at the top of your code file.

### Runtime error: database driver not found

Check that you installed the corresponding NuGet driver package (e.g., `Microsoft.Data.SqlClient`, `MySqlConnector`, etc.) and that the `Provider` value in `appsettings.json` matches the installed package.

### Can I use LiteOrm in a .NET Framework project?

Yes. LiteOrm supports `.NET Standard 2.0`, which is compatible with .NET Framework 4.6.1 and above. However, .NET 8.0+ is recommended for the best experience.

### Will the project size increase significantly after installation?

No. LiteOrm itself is very lightweight—the core package is only a few hundred KB. With necessary dependencies (Autofac, Castle.Core), the total increase is about 2-3 MB.

## Related Links

- [Back to docs hub](../README.md)
- [Configuration and Registration](./03-configuration-and-registration.en.md)
- [First End-to-End Example](./04-first-example.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)