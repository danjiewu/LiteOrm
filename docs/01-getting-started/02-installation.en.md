# Installation and Environment Requirements

This document covers the runtime environment, database support, and installation methods for LiteOrm.

## Environment Requirements

- `.NET 8.0+`
- `.NET Standard 2.0` (compatible with .NET Framework 4.6.1+)
- Dependencies: `Microsoft.Extensions.DependencyInjection`, `Castle.Core`

## Supported Databases

- SQL Server 2012+
- MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

> For older database versions where default pagination syntax is incompatible, refer to [Custom Paging](../03-advanced-topics/05-custom-paging.md) and [Custom SqlBuilder / Dialect Extension](../04-extensibility/03-custom-sqlbuilder.md).

## Install from NuGet

```bash
dotnet add package LiteOrm
```

## Next Steps After Installation

1. Prepare connection strings and data source configuration.
2. Call `RegisterLiteOrm()` during host startup.
3. Define entities, services, or DAOs.
4. Use `SearchAsync`, `InsertAsync`, and other APIs to complete the first example.

## Related Links

- [Back to docs hub](../README.md)
- [Configuration and Registration](./03-configuration-and-registration.en.md)
- [First End-to-End Example](./04-first-example.en.md)
- [Configuration Reference](../05-reference/01-configuration-reference.en.md)