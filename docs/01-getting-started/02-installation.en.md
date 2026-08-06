# Installation and Environment Requirements

This document covers the runtime environment, database support, and two installation methods: base-only (`LiteOrm`) and host integration (`LiteOrm.DependencyInjection`).

> **Beginner tip**: If you just want to quickly try out LiteOrm, we recommend using SQLite—it requires no database server installation and works out of the box. Bring in `LiteOrm.DependencyInjection` only when you need host-level integration (Autofac, AOP).

## Environment Requirements

- `.NET 8.0+`
- `.NET Standard 2.0` (compatible with .NET Framework 4.6.1+)
- Database driver package: install the corresponding NuGet driver for your chosen database

> **How to check your .NET version?** Run `dotnet --version` in a terminal. Make sure the output is `8.0.x` or higher. If not installed, visit [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).

## Supported Databases

**Mainstream databases:**

- SQL Server 2012+
- MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

**Domestic / compatible databases:**

- Dameng DM (Oracle compatible)
- KingbaseES (PostgreSQL compatible)
- Huawei GaussDB / openGauss (PostgreSQL compatible)
- OceanBase (MySQL compatible)
- TiDB (MySQL compatible)
- GreatDB (MySQL compatible)

> Domestic databases inherit behavior from their mainstream counterparts and are auto-detected with priority. See [Database Compatibility Notes](../05-reference/07-database-compatibility.en.md) for details.

> For older database versions where default pagination syntax is incompatible, refer to [Custom Paging](../03-advanced-topics/05-custom-paging.en.md) and [Custom SqlBuilder / Dialect Extension](../04-extensibility/03-custom-sqlbuilder.en.md).

### Database NuGet Driver Packages

> Regardless of installation method, you need to install the corresponding NuGet driver package for your database:

| Database | NuGet Package |
|----------|---------------|
| SQL Server | `Microsoft.Data.SqlClient` |
| SQL Server (legacy) | `System.Data.SqlClient` |
| MySQL | `MySqlConnector` |
| MySQL (legacy) | `MySql.Data` |
| PostgreSQL | `Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` |
| SQLite | `Microsoft.Data.Sqlite` |

> When configuring `appsettings.json`, the `Provider` field requires the fully qualified type name of the database driver. See [Database Compatibility Notes](../05-reference/07-database-compatibility.en.md).

## Option 1: Base Library Only (`LiteOrm`)

For scenarios that only need core capabilities (entity mapping, queries, DAO) and manage connections and lifecycle yourself. This method **introduces no DI framework**—no Autofac or Castle dynamic proxy.

```bash
dotnet add package LiteOrm
dotnet add package Microsoft.Data.Sqlite   # choose based on your database
```

- The base library consists of `LiteOrm` and `LiteOrm.Common`; `LiteOrm` automatically brings in `LiteOrm.Common`.
- No `RegisterLiteOrm()` is provided, and no AOP interception (transactions/permissions/logging).
- Data access is done through DAO types such as `ObjectDAO` / `DataDAO`.

## Option 2: Host Integration Package (`LiteOrm.DependencyInjection`)

For ASP.NET Core projects. `LiteOrm.DependencyInjection` brings in the Autofac container and Castle dynamic proxy, registers everything via `builder.Host.RegisterLiteOrm()`, and enables AOP transactions/permissions/logging.

```bash
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection    # required for DI registration (RegisterLiteOrm)
dotnet add package Microsoft.Data.Sqlite   # choose based on your database
```

## Creating a New Project from Scratch

> Here are the complete commands to create an ASP.NET Core project with LiteOrm.DependencyInjection from scratch:

```bash
# 1. Create a Web API project
dotnet new webapi -n MyLiteOrmApp
cd MyLiteOrmApp

# 2. Install LiteOrm (using SQLite as an example)
dotnet add package LiteOrm
dotnet add package LiteOrm.DependencyInjection
dotnet add package Microsoft.Data.Sqlite

# 3. Run the project to verify the environment
dotnet run
```

> If you use Visual Studio, you can search for `LiteOrm` in "Manage NuGet Packages" to install it.

## Next Steps After Installation

- **Base only**: use DAO directly for data access; see [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md) and [Query Overview](../02-core-usage/04-query-overview.en.md).
- **LiteOrm.DependencyInjection integration**: call `RegisterLiteOrm()` during host startup; see [Configuration Reference](../05-reference/01-configuration-reference.en.md) and [First End-to-End Example](./05-first-example-di.en.md).

> **SQLite quick start**: If you want to try SQLite quickly, the connection string is simply `Data Source=myapp.db`—no database server needed. See the [First End-to-End Example](./05-first-example-di.en.md) for a complete walkthrough.

## Common Installation Issues

### Build error after installation: `RegisterLiteOrm` method not found

Make sure you installed the `LiteOrm.DependencyInjection` package (`RegisterLiteOrm()` is defined there; installing only `LiteOrm` or `LiteOrm.Common` won't provide it), and add `using LiteOrm.DependencyInjection;` at the top of your code file.

### Runtime error: database driver not found

Check that you installed the corresponding NuGet driver package (e.g., `Microsoft.Data.SqlClient`, `MySqlConnector`, etc.) and that the `Provider` value in `appsettings.json` matches the installed package.

### Can I use LiteOrm in a .NET Framework project?

Yes. LiteOrm supports `.NET Standard 2.0`, which is compatible with .NET Framework 4.6.1 and above. However, .NET 8.0+ is recommended for the best experience.

### Will the project size increase significantly after installation?

No. LiteOrm itself is very lightweight—the base package is only a few hundred KB. With necessary dependencies (Autofac, Castle.Core), the total increase is about 2-3 MB. Base-only usage pulls in no Autofac or Castle, so the footprint is even smaller.

## Related Links

- [Back to docs hub](../README.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
- [First End-to-End Example](./05-first-example-di.en.md)
