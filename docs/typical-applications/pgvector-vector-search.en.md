# Vector Search on PostgreSQL (pgvector)

Whether you are building a recommendation feed, a similar-image search or a semantic search, stripping away the product framing leaves the same two steps: store a vector in the database, then fetch the nearest rows by distance. On PostgreSQL, the [pgvector](https://github.com/pgvector/pgvector) extension supplies the `vector` column type, the distance operators and HNSW/IVFFlat indexes.

LiteOrm itself requires no source changes, and there are only a handful of places to hook into:

| Goal | Approach |
| --- | --- |
| Create a `vector(3)` column | Subclass `PostgreSqlBuilder` and override `GetSqlTypeDefinition` |
| Emit `<->`, `<=>` and `<#>` in SQL | Register function handlers that translate functions into operators |
| Build distance conditions and sorting with `Expr` | Add `ValueTypeExpr` extensions that return `FunctionExpr` |
| Write typed queries with Lambda | Register method handlers through `LambdaExprConverter.RegisterMethodHandler` |
| Insert and update vectors | Type the entity property as `Pgvector.Vector` and let Npgsql map it |

The sections below go through three requirements: creating the table, writing data and retrieving neighbours, and the SQL snippets shown are what the PostgreSQL dialect produces.

## Prerequisites

First, enable the extension in the database:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Then add the two required NuGet packages to the project:

```bash
dotnet add package Pgvector
dotnet add package Npgsql
```

The `Pgvector` package provides the `Pgvector.Vector` type together with the matching Npgsql mapping. One assumption is easy to miss here: LiteOrm's connection pool creates `NpgsqlConnection` instances through `Activator.CreateInstance(ProviderType)` and never uses a data source you configured yourself, so the mapping has to be registered globally at startup:

```csharp
using Npgsql;

// Must run before the first vector read or write; Npgsql 8+ marks GlobalTypeMapper as obsolete but it still works
NpgsqlConnection.GlobalTypeMapper.UseVector();
```

If the application builds its own `NpgsqlDataSource`, the registration moves to the builder, where you call `UseVector()` instead. Pick whichever of the two applies:

```csharp
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
```

> If this step is skipped, the first `Vector` read or write fails inside the driver with an unsupported-type exception.

## Requirement 1: create the table and map the entity

Start by defining the entity, with the vector property typed as `Pgvector.Vector`:

```csharp
using LiteOrm;
using LiteOrm.Common;
using PgVector = Pgvector.Vector;

[Table("Article")]
public class Article
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Title", Length = 200)]
    public string Title { get; set; } = "";

    // Custom CLR types never create a column implicitly; Length is the vector dimension here
    [Column("Embedding", Length = 3, AllowNull = true)]
    public PgVector? Embedding { get; set; }
}
```

Three things tend to trip people up here, and each one is usually remembered after the first encounter:

- LiteOrm infers `Pgvector.Vector` as `Object`, so without `[Column]` the property is treated as unmappable and skipped.
- `Length` normally means string length; here it declares the `vector(N)` dimension, which every vector written to the column must match.
- `ColumnAttribute.AllowNull` defaults to `false`, so a nullable property needs an explicit `AllowNull = true`; otherwise the generated column is `NOT NULL`.

With `SyncTable` enabled, LiteOrm creates the table through the registered dialect:

```sql
CREATE TABLE "article" (
  "id" BIGSERIAL PRIMARY KEY NOT NULL,
  "title" VARCHAR(200) NOT NULL,
  "embedding" vector(3)
)
```

`CREATE EXTENSION` and index creation, however, are better kept in an init script or a migration, since automatic table creation is convenient but does not cover them.

## Custom dialect: vector columns and distance operators

The approach is to subclass `PostgreSqlBuilder`, which does not take much: render `vector(N)` for the column type and translate function names into pgvector operators.

```csharp
using LiteOrm;
using LiteOrm.Common;
using Pgvector;

/// <summary>PostgreSQL dialect with pgvector support: vector(N) columns and distance operators.</summary>
public sealed class PgVectorPostgreSqlBuilder : PostgreSqlBuilder
{
    public static new readonly PgVectorPostgreSqlBuilder Instance = new();

    static PgVectorPostgreSqlBuilder()
    {
        RegisterVectorFunctions();
    }

    /// <summary>Render vector properties as vector(N), using Length as the dimension.</summary>
    protected override string GetSqlTypeDefinition(ColumnDefinition column)
    {
        if (column.PropertyType == typeof(Vector))
            return column.Length > 0 ? $"vector({column.Length})" : "vector";
        return base.GetSqlTypeDefinition(column);
    }

    private static void RegisterVectorFunctions()
    {
        RegisterDistance("pgvector_l2_distance", " <-> ");
        RegisterDistance("pgvector_cosine_distance", " <=> ");
        RegisterDistance("pgvector_inner_product", " <#> ");

        Instance.RegisterFunctionSqlHandler("pgvector_vector_dims", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context) =>
        {
            outSql.Append("vector_dims(");
            expr.Args[0].ToSql(ref outSql, context);
            outSql.Append(')');
        });

        Instance.RegisterFunctionSqlHandler("pgvector_l2_normalize", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context) =>
        {
            outSql.Append("l2_normalize(");
            expr.Args[0].ToSql(ref outSql, context);
            outSql.Append(')');
        });
    }

    private static void RegisterDistance(string functionName, string sqlOperator)
    {
        Instance.RegisterFunctionSqlHandler(functionName, (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context) =>
        {
            outSql.Append('(');
            expr.Args[0].ToSql(ref outSql, context);
            outSql.Append(sqlOperator);
            outSql.Append('(');
            expr.Args[1].ToSql(ref outSql, context);
            outSql.Append(")::vector");
            outSql.Append(')');
        });
    }
}
```

The right-hand operand of a distance is always written as `(...)::vector`: with a `Vector` parameter the cast is redundant but harmless, while with text or array input it does real work and removes any dependence on the driver guessing the parameter type.

Function handlers are registered against the concrete builder type and looked up along the inheritance chain, so handlers on `PgVectorPostgreSqlBuilder` are hit first while the ones built into `PostgreSqlBuilder` keep working.

## Register the dialect

There are two ways to register, through DI or by calling the factory directly:

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder(typeof(NpgsqlConnection), PgVectorPostgreSqlBuilder.Instance);
});
```

```csharp
using LiteOrm;
using Npgsql;

SqlBuilderFactory.Instance.RegisterSqlBuilder(typeof(NpgsqlConnection), PgVectorPostgreSqlBuilder.Instance);
```

Registering by data source name also works (`options.RegisterSqlBuilder("ArticleDb", PgVectorPostgreSqlBuilder.Instance)`). Registration must not be delayed: it has to happen before DAOs are created or an entity is touched for the first time.

## Expr extensions

Add a few `ValueTypeExpr` extension methods that wrap the call in a `FunctionExpr`. There is one hard rule here: the function name must match the one registered on the builder exactly.

```csharp
using LiteOrm.Common;

public static class PgVectorExprExtensions
{
    public static FunctionExpr L2Distance(this ValueTypeExpr column, ValueTypeExpr target)
        => new FunctionExpr("pgvector_l2_distance", column, target);

    public static FunctionExpr CosineDistance(this ValueTypeExpr column, ValueTypeExpr target)
        => new FunctionExpr("pgvector_cosine_distance", column, target);

    public static FunctionExpr InnerProduct(this ValueTypeExpr column, ValueTypeExpr target)
        => new FunctionExpr("pgvector_inner_product", column, target);

    public static FunctionExpr VectorDims(this ValueTypeExpr column)
        => new FunctionExpr("pgvector_vector_dims", column);

    public static FunctionExpr Normalize(this ValueTypeExpr vector)
        => new FunctionExpr("pgvector_l2_normalize", vector);
}
```

## Lambda extensions

Lambda expressions cannot call database functions directly, so the usual workaround is to define same-named CLR methods as placeholders and let method handlers replace the expression-tree call with the `FunctionExpr` built above:

```csharp
using LiteOrm.Common;
using Pgvector;

public static class PgVectorLambdaExtensions
{
    // Placeholder methods: only used inside expression trees, never invoked at runtime
    public static double L2Distance(this Vector column, Vector target)
        => throw new NotSupportedException("Only used inside LiteOrm expressions.");

    public static double CosineDistance(this Vector column, Vector target)
        => throw new NotSupportedException("Only used inside LiteOrm expressions.");

    public static double MaxInnerProduct(this Vector column, Vector target)
        => throw new NotSupportedException("Only used inside LiteOrm expressions.");

    public static int Dimensions(this Vector column)
        => throw new NotSupportedException("Only used inside LiteOrm expressions.");

    public static Vector Normalize(this Vector column)
        => throw new NotSupportedException("Only used inside LiteOrm expressions.");

    public static void Register()
    {
        LambdaExprConverter.RegisterMethodHandler(typeof(PgVectorLambdaExtensions), nameof(L2Distance), (node, converter) =>
            PgVectorExprExtensions.L2Distance(converter.Convert(node.Arguments[0]).AsValue(), converter.Convert(node.Arguments[1]).AsValue()));

        LambdaExprConverter.RegisterMethodHandler(typeof(PgVectorLambdaExtensions), nameof(CosineDistance), (node, converter) =>
            PgVectorExprExtensions.CosineDistance(converter.Convert(node.Arguments[0]).AsValue(), converter.Convert(node.Arguments[1]).AsValue()));

        LambdaExprConverter.RegisterMethodHandler(typeof(PgVectorLambdaExtensions), nameof(MaxInnerProduct), (node, converter) =>
            PgVectorExprExtensions.InnerProduct(converter.Convert(node.Arguments[0]).AsValue(), converter.Convert(node.Arguments[1]).AsValue()));

        LambdaExprConverter.RegisterMethodHandler(typeof(PgVectorLambdaExtensions), nameof(Dimensions), (node, converter) =>
            PgVectorExprExtensions.VectorDims(converter.Convert(node.Arguments[0]).AsValue()));

        LambdaExprConverter.RegisterMethodHandler(typeof(PgVectorLambdaExtensions), nameof(Normalize), (node, converter) =>
            PgVectorExprExtensions.Normalize(converter.Convert(node.Arguments[0]).AsValue()));
    }
}
```

A single call to `Register()` at startup is enough:

```csharp
PgVectorLambdaExtensions.Register();
```

The registry is static, so registration must be finished before the first Lambda conversion. If what you need to extend is member access rather than a method call, use `RegisterMemberHandler`; see [Expression Extensions](../extensibility/expression-extension.en.md).

The startup order is roughly as follows: `CREATE EXTENSION` → enable the Npgsql type mapping → register the dialect → register Lambda handlers.

## Requirement 2: insert and update

A vector property has `DbValueType.Object`, and that detail matters: LiteOrm never sets `DbParameter.DbType`, so Npgsql maps the CLR `Pgvector.Vector` to `vector` on its own, which makes the value converter unnecessary and removes the need for a `::vector` cast in Insert/Update SQL.

```csharp
using PgVector = Pgvector.Vector;
using static LiteOrm.Common.Expr;

// 1) Single insert: Id is filled from RETURNING
var article = new Article
{
    Title = "Getting started with LiteOrm",
    Embedding = new PgVector(new float[] { 0.12f, 0.34f, 0.56f })
};
await articleService.InsertAsync(article);

// 2) Batch insert
await articleService.BatchInsertAsync(new[]
{
    new Article { Title = "Vector search", Embedding = new PgVector(new float[] { 0.20f, 0.10f, 0.70f }) },
    new Article { Title = "pgvector", Embedding = new PgVector(new float[] { 0.05f, 0.90f, 0.15f }) }
});

// 3) Update the whole entity: all updatable columns are written
var loaded = await articleService.SearchOneAsync(a => a.Id == article.Id);
loaded!.Embedding = new PgVector(new float[] { 0.30f, 0.30f, 0.40f });
await articleService.UpdateAsync(loaded);

// 4) Update only the vector column (recommended)
var newVector = new PgVector(new float[] { 0.11f, 0.22f, 0.33f });
await articleService.UpdateAllAsync(
    a => new Article { Embedding = newVector },
    a => a.Id == article.Id);

// 5) Build an UpdateExpr dynamically
await articleService.UpdateAllAsync(
    Update<Article>()
        .Where(Prop("Id") == article.Id)
        .Set(("Embedding", Value(newVector))));

// 6) Batch update
var loadedList = await articleService.SearchAsync(a => a.Id > 0);
foreach (var item in loadedList)
    item.Embedding = new PgVector(new float[] { 0.01f, 0.02f, 0.03f });
await articleService.BatchUpdateAsync(loadedList);

// 7) Upsert
await articleService.UpdateOrInsertAsync(article);
await articleService.BatchUpdateOrInsertAsync(loadedList);
```

The corresponding SQL:

```sql
-- Single insert (Id is an identity column)
INSERT INTO "article" ("title","embedding") 
VALUES (@0,@1) RETURNING "id"

-- Batch insert
INSERT INTO "article" ("title","embedding") 
VALUES (@0,@1),(@2,@3)

-- Update the whole entity
UPDATE "article" SET "title" = @0,"embedding" = @1 
WHERE "id" = @2

-- Update only the vector column
UPDATE "article" SET "embedding" = @0 
WHERE "id" = @1

-- Batch update (writes every updatable column, in UpdatableColumns order)
UPDATE "article" u SET "title" = v.v0, "embedding" = v.v1
FROM (VALUES (@0, @1, @2), (@3, @4, @5)) AS v (v0, v1, k0)
WHERE u."id" = v.k0
```

Writing vectors has a few sharp edges worth watching:

- Prefer `UpdateAllAsync` or `UpdateExpr` when only the vector changes. An entity-level `UpdateAsync` or `BatchUpdateAsync` writes every updatable column and can overwrite concurrent changes.
- `Embedding = null` writes `NULL` only when the column declares `AllowNull = true`; without it the column is `NOT NULL` and the database rejects the write.
- PostgreSQL rejects a vector whose length does not match `vector(N)`, and LiteOrm does not validate dimensions.
- If the write side passes a string or `float[]` instead of `Pgvector.Vector`, the parameter becomes text or an array and the conversion is left to you; this article only covers the official mapping.

## Requirement 3: top-k retrieval

The lambda form looks like any other query, with the distance function acting as just another term in the condition or the ordering:

```csharp
var queryVector = new Pgvector.Vector(new float[] { 0.12f, 0.34f, 0.56f });

var top = await articleService.SearchAsync(q =>
    q.Where(a => a.Embedding!.CosineDistance(queryVector) < 0.2)
     .OrderBy(a => a.Embedding!.L2Distance(queryVector))
     .Take(10));
```

The `Expr` form works here as well. In the lambda form `0.2` is a constant, so write the threshold as `Const(0.2)` to keep the two consistent:

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

var target = Value(queryVector);
var list = await articleService.SearchAsync(
    Expr.From(typeof(Article))
        .Where(Prop("Embedding").CosineDistance(target) < Const(0.2))
        .OrderBy(Prop("Embedding").L2Distance(target).Asc())
        .Take(10));
```

Both forms produce exactly the same SQL:

```sql
SELECT ... FROM "article"
WHERE ("embedding" <=> (@0)::vector) < 0.2
ORDER BY ("embedding" <-> (@1)::vector)
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY
```

`<=>` and `<->` are cosine and Euclidean distance, so ascending order runs from nearest to farthest. `<#>` is the one people misread: it returns the negative inner product, which still puts the most similar rows first in ascending order, but it should not be presented as a positive similarity score. The vector itself always goes through parameter binding and should never be pasted into the SQL text, which is where injection and type-inference problems begin.

## Indexes and performance

Without an index PostgreSQL has to scan the whole table, which is acceptable for small data sets but requires an approximate index once the table grows. The operator and the index opclass must be used as a pair:

| Need | Operator | Index opclass |
| --- | --- | --- |
| Nearest by Euclidean distance | `<->` | `vector_l2_ops` |
| Nearest by cosine distance | `<=>` | `vector_cosine_ops` |
| Largest inner product | `<#>` | `vector_ip_ops` |

Examples of building either type of index:

```sql
-- HNSW
CREATE INDEX ix_article_embedding_l2 ON "article"
USING hnsw ("embedding" vector_l2_ops);

-- IVFFlat (the table should already contain data)
CREATE INDEX ix_article_embedding_cosine ON "article"
USING ivfflat ("embedding" vector_cosine_ops) WITH (lists = 100);

-- Tune recall at query time
SET hnsw.ef_search = 100;
SET ivfflat.probes = 10;
```

A few notes on indexes:

- The ordering expression must match the index opclass. Ordering with `<->` against a `vector_cosine_ops` index does not use the index.
- With a `WHERE` filter, PostgreSQL may choose to filter first and sort afterwards, which leaves the index unused; a highly selective filter can still be faster, and deciding which plan wins is a matter for `EXPLAIN ANALYZE` on real data.
- HNSW limits `vector` to 2000 dimensions, beyond which the dimension must be reduced or `halfvec` used; the pgvector version also decides which operators exist, and `<+>` (L1) requires a newer release.

## When it is not a good fit

- **High dimensions**: HNSW supports `vector` up to 2000 dimensions, beyond which you need to reduce the dimension or switch to `halfvec`.
- **Heavy filters plus vector ordering**: when several business-field filters are combined with a vector sort, the index can usually serve only one side, and pre-filtering, partitioning or a dedicated search service may be the more economical choice.
- **No extension available**: some managed PostgreSQL offerings do not provide pgvector, leaving brute-force computation or an external vector store as the fallback.
- **Tiny data sets**: under a few tens of thousands of rows a brute-force scan is already fast enough, and adding a vector index plus an extra dependency is rarely worth it.
- **One codebase across databases**: the types and operators in this article are PostgreSQL-only, so other dialects require their own `FunctionExpr` handlers.

## Related links

- [Back to contents](../README.en.md)
- [CRUD Guide](../core-usage/crud-guide.en.md)
- [Lambda Query Guide](../core-usage/lambda-guide.en.md)
- [Data Mapping and Value Conversion](../advanced-topics/data-mapping.en.md)
- [Expression Extensions](../extensibility/expression-extension.en.md)
- [Custom SqlBuilder](../extensibility/custom-sqlbuilder.en.md)
- [Database Compatibility](../reference/database-compatibility.en.md)
- [JSON Columns for Objects and Field-Level Queries](./json-column-mapping.en.md)
