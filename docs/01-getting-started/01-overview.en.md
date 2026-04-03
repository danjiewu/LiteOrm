# Overview and Fit

LiteOrm is a lightweight .NET ORM that stays close to SQL while still giving you structured mapping, reusable services, and expression-based queries.

## 1. When LiteOrm is a good fit

- You want more SQL control than a convention-heavy ORM usually provides.
- You need both day-to-day Lambda queries and dynamic query builders based on `Expr`.
- Your project uses multiple data sources, read/write splitting, or sharded tables.
- You want automatic relationship projection without giving up direct access to DAO-level APIs.

## 2. Core ideas

- Three main query styles: Lambda, `Expr`, and `ExprString`
- Two common access layers: `EntityService` for business workflows, `ObjectDAO` / `ObjectViewDAO` for lower-level data access
- Relationship metadata through `ForeignType`, `TableJoin`, `ForeignColumn`, and `AutoExpand`
- Dialect and SQL customization through `SqlBuilder` and `FunctionSqlHandler`
- Sharding support through `IArged` and `TableArgs`

## 3. Positioning vs other approaches

| Option | Usually strongest at |
|------|-----------------------|
| EF Core | Full ecosystem, migrations, conventions |
| Dapper | Minimal abstraction and handwritten SQL |
| LiteOrm | Flexible SQL control, expression extensibility, relationship mapping, and high-throughput service/DAO patterns |

## 4. Recommended reading order

1. [Installation](./02-installation.en.md)
2. [Configuration and Registration](./03-configuration-and-registration.en.md)
3. [First End-to-End Example](./04-first-example.en.md)
4. [Entity Mapping and Data Sources](../02-core-usage/01-entity-mapping.en.md)
5. [Query Guide](../02-core-usage/03-query-guide.en.md)

## Related Links

- [Back to English docs hub](../SUMMARY.en.md)
- [API Index](../05-reference/02-api-index.en.md)
- [Demo Project](../../LiteOrm.Demo/)
