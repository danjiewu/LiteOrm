# Installation and Requirements

This page covers the minimum runtime requirements, supported databases, and the usual next steps after installing LiteOrm.

## 1. Runtime requirements

- .NET `8.0+`
- .NET Standard `2.0`
- .NET Framework `4.6.1+`

Make sure your application also references the database provider package that matches your target connection type.

## 2. Supported databases

LiteOrm is designed around provider-based access and `SqlBuilder` dialects. The repository documentation and samples mainly cover:

- SQL Server
- MySQL
- PostgreSQL
- Oracle

If an older database version needs different paging or function SQL, use a custom `SqlBuilder`.

## 3. Install from NuGet

Install LiteOrm and any required supporting packages for your project:

```powershell
Install-Package LiteOrm
Install-Package Castle.Core
```

Also install the ADO.NET provider for your database, such as `MySqlConnector`, `Npgsql`, `System.Data.SqlClient`, or the Oracle provider you use internally.

## 4. What to do next

1. Configure the `LiteOrm` section in `appsettings.json`
2. Call `RegisterLiteOrm()` during startup
3. Define entities with `[Table]` and `[Column]`
4. Run the [first example](./04-first-example.en.md)

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [Configuration and Registration](./03-configuration-and-registration.en.md)
- [First End-to-End Example](./04-first-example.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)
