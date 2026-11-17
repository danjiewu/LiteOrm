# PostgreSQL 向量检索（pgvector）

不管是做文章推荐、找相似图片还是语义搜索，剥掉业务外壳后，剩下的事情其实一样：把向量存进数据库，再按距离取出最接近的几条。PostgreSQL 这边有 [pgvector](https://github.com/pgvector/pgvector) 扩展，`vector` 列类型、距离运算符以及 HNSW/IVFFlat 索引都不缺。

LiteOrm 本身一行源码都不用改，需要动的地方只有下面几处：

| 目标 | 做法 |
| --- | --- |
| 建出 `vector(3)` 列 | 继承 `PostgreSqlBuilder`，覆盖 `GetSqlTypeDefinition` |
| 在 SQL 里写 `<->`、`<=>`、`<#>` | 给自定义 Builder 注册函数处理器，把函数翻译成运算符 |
| 用 `Expr` 拼距离条件与排序 | 定义 `ValueTypeExpr` 扩展，返回 `FunctionExpr` |
| 用 Lambda 写强类型查询 | 通过 `LambdaExprConverter.RegisterMethodHandler` 注册方法处理器 |
| 插入、更新向量 | 实体属性用 `Pgvector.Vector`，由 Npgsql 类型映射读写 |

接下来按建表、写入、检索三部分展开，文中给出的 SQL 都是在 PostgreSQL 方言下生成的结果。

## 前置准备

首先在数据库中启用扩展：

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

项目中还需要安装两个 NuGet 包：

```bash
dotnet add package Pgvector
dotnet add package Npgsql
```

`Pgvector` 包提供了 `Pgvector.Vector` 类型以及配套的 Npgsql 映射。这里有一个容易被忽略的前提：LiteOrm 的连接池是通过 `Activator.CreateInstance(ProviderType)` 直接创建 `NpgsqlConnection` 的，并不会使用你配置的数据源，因此映射只能在启动时全局注册：

```csharp
using Npgsql;

// 必须在第一次读写向量之前执行；Npgsql 8+ 会把 GlobalTypeMapper 标记为过时，但依然可用
NpgsqlConnection.GlobalTypeMapper.UseVector();
```

如果项目自己构建 `NpgsqlDataSource`，注册位置就换到构建器上，调用 `UseVector()` 即可。两种方式任选其一：

```csharp
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
```

> 如果漏掉这一步，第一次读写 `Vector` 时就会在驱动层抛出“不支持该类型”的异常。

## 需求一：建表与实体映射

先定义实体，向量属性直接使用 `Pgvector.Vector` 类型：

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

    // 自定义 CLR 类型不会自动生成列，必须显式声明；Length 在这里表示向量维度
    [Column("Embedding", Length = 3, AllowNull = true)]
    public PgVector? Embedding { get; set; }
}
```

这里有三个容易出错的地方，踩过一次之后通常就很难忘记：

- `Pgvector.Vector` 在 LiteOrm 里会被推断成 `Object`，如果不写 `[Column]`，这个属性就会被当作不可映射成员跳过。
- `Length` 原本用于表示字符串长度，这里借用来声明 `vector(N)` 的维度，因此写入的向量长度必须与它保持一致。
- `ColumnAttribute.AllowNull` 默认为 `false`，属性可空时需要显式写 `AllowNull = true`，否则生成的列会是 `NOT NULL`。

启用 `SyncTable` 后，LiteOrm 会使用注册的方言自动建表，生成的 DDL 如下：

```sql
CREATE TABLE "article" (
  "id" BIGSERIAL PRIMARY KEY NOT NULL,
  "title" VARCHAR(200) NOT NULL,
  "embedding" vector(3)
)
```

不过 `CREATE EXTENSION` 和索引我还是建议放在初始化脚本或迁移中处理，自动建表虽然省事，却管不了这些内容。

## 自定义方言：vector 列与距离运算符

具体做法是继承 `PostgreSqlBuilder`，需要完成的工作并不多：列类型按维度输出 `vector(N)`，函数名则翻译成 pgvector 的运算符。

```csharp
using LiteOrm;
using LiteOrm.Common;
using Pgvector;

/// <summary>支持 pgvector 的 PostgreSQL 方言：vector(N) 列类型与向量距离运算符。</summary>
public sealed class PgVectorPostgreSqlBuilder : PostgreSqlBuilder
{
    public static new readonly PgVectorPostgreSqlBuilder Instance = new();

    static PgVectorPostgreSqlBuilder()
    {
        RegisterVectorFunctions();
    }

    /// <summary>vector 属性的列类型按维度输出 vector(N)。</summary>
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

        Instance.RegisterFunctionSqlHandler("pgvector_vector_dims", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, int priority) =>
        {
            outSql.Append("vector_dims(");
            expr.Args[0].ToSql(ref outSql, context);
            outSql.Append(')');
        });

        Instance.RegisterFunctionSqlHandler("pgvector_l2_normalize", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, int priority) =>
        {
            outSql.Append("l2_normalize(");
            expr.Args[0].ToSql(ref outSql, context);
            outSql.Append(')');
        });
    }

    private static void RegisterDistance(string functionName, string sqlOperator)
    {
        Instance.RegisterFunctionSqlHandler(functionName, (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, int priority) =>
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

距离运算的右操作数一律写成 `(...)::vector`：传入 `Vector` 参数时这层转换其实是多余的，但保留也不会有什么坏处；传入文本或数组时它才真正发挥作用，可以避免依赖驱动去猜测参数类型。

函数处理器注册在具体构建器类型上，查找时会沿继承链向上进行，因此 `PgVectorPostgreSqlBuilder` 上注册的处理器会优先命中，而 `PostgreSqlBuilder` 自带的处理器依然可用。

## 注册方言

注册有两种方式，可以通过 DI 完成，也可以手动调用：

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

也可以按数据源名称注册（`options.RegisterSqlBuilder("ArticleDb", PgVectorPostgreSqlBuilder.Instance)`）。注册时机不能太晚：必须在创建 DAO 或首次访问实体之前完成。

## Expr 扩展

给 `ValueTypeExpr` 增加几个扩展方法，把调用包装成 `FunctionExpr`。这里有一个必须遵守的约定：函数名要与 Builder 中注册的名称完全一致。

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

## Lambda 扩展

Lambda 表达式中无法直接调用数据库函数，常见的做法是先定义同名的 CLR 方法作为占位，再通过方法处理器把表达式树中的调用替换成上一步构造的 `FunctionExpr`：

```csharp
using LiteOrm.Common;
using Pgvector;

public static class PgVectorLambdaExtensions
{
    // 占位方法：只在表达式树中使用，运行时不会被调用
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

在启动时调用一次 `Register()` 即可：

```csharp
PgVectorLambdaExtensions.Register();
```

处理器注册表是静态的，因此必须在第一次转换 Lambda 之前完成注册。另外，如果需要扩展的是成员访问而不是方法调用，可以使用 `RegisterMemberHandler`，具体写法见[表达式扩展](../extensibility/expression-extension.md)。

启动顺序大致如下：`CREATE EXTENSION` → 启用 Npgsql 类型映射 → 注册方言 → 注册 Lambda 处理器。

## 需求二：写入与更新

向量属性的 `DbValueType` 是 `Object`，这个细节很关键：LiteOrm 不会设置 `DbParameter.DbType`，Npgsql 会按 CLR 类型把 `Pgvector.Vector` 映射成 `vector`，因此值转换器可以省略，Insert/Update 的 SQL 中也不需要再追加 `::vector`。

```csharp
using PgVector = Pgvector.Vector;
using static LiteOrm.Common.Expr;

// 1) 单条插入：Id 由 RETURNING 回填
var article = new Article
{
    Title = "LiteOrm 入门",
    Embedding = new PgVector(new float[] { 0.12f, 0.34f, 0.56f })
};
await articleService.InsertAsync(article);

// 2) 批量插入
await articleService.BatchInsertAsync(new[]
{
    new Article { Title = "向量检索", Embedding = new PgVector(new float[] { 0.20f, 0.10f, 0.70f }) },
    new Article { Title = "pgvector", Embedding = new PgVector(new float[] { 0.05f, 0.90f, 0.15f }) }
});

// 3) 按实体更新：会写入所有可更新列
var loaded = await articleService.SearchOneAsync(a => a.Id == article.Id);
loaded!.Embedding = new PgVector(new float[] { 0.30f, 0.30f, 0.40f });
await articleService.UpdateAsync(loaded);

// 4) 只更新向量列（推荐）
var newVector = new PgVector(new float[] { 0.11f, 0.22f, 0.33f });
await articleService.UpdateAllAsync(
    a => new Article { Embedding = newVector },
    a => a.Id == article.Id);

// 5) 动态装配 UpdateExpr
await articleService.UpdateAllAsync(
    Update<Article>()
        .Where(Prop("Id") == article.Id)
        .Set(("Embedding", Value(newVector))));

// 6) 批量更新
var loadedList = await articleService.SearchAsync(a => a.Id > 0);
foreach (var item in loadedList)
    item.Embedding = new PgVector(new float[] { 0.01f, 0.02f, 0.03f });
await articleService.BatchUpdateAsync(loadedList);

// 7) Upsert
await articleService.UpdateOrInsertAsync(article);
await articleService.BatchUpdateOrInsertAsync(loadedList);
```

对应的 SQL：

```sql
-- 单条插入（Id 为自增列）
INSERT INTO "article" ("title","embedding") 
VALUES (@0,@1) RETURNING "id"

-- 批量插入
INSERT INTO "article" ("title","embedding") 
VALUES (@0,@1),(@2,@3)

-- 按实体更新
UPDATE "article" SET "title" = @0,"embedding" = @1 
WHERE "id" = @2

-- 只更新向量列
UPDATE "article" SET "embedding" = @0 
WHERE "id" = @1

-- 批量更新（按 UpdatableColumns 顺序写入全部可更新列）
UPDATE "article" u SET "title" = v.v0, "embedding" = v.v1
FROM (VALUES (@0, @1, @2), (@3, @4, @5)) AS v (v0, v1, k0)
WHERE u."id" = v.k0
```

写入时有几个容易出问题的地方：

- 只改向量列时优先用 `UpdateAllAsync` 或 `UpdateExpr`。按实体 `UpdateAsync` / `BatchUpdateAsync` 会把所有可更新列一起写回，容易覆盖其它并发修改。
- `Embedding = null` 会写入 `NULL`，前提是列声明了 `AllowNull = true`；不声明时列是 `NOT NULL`，写空值会被数据库拒绝。
- 向量长度与 `vector(N)` 不一致时，由 PostgreSQL 报错，LiteOrm 不做维度校验。
- 如果写入端提供的不是 `Pgvector.Vector`，而是字符串或 `float[]`，参数类型会变成文本或数组，需要自行处理类型转换；本文只覆盖官方类型映射这一种情况。

## 需求三：top-k 检索

Lambda 写法与普通查询没有区别，距离函数只是条件或排序的一部分：

```csharp
var queryVector = new Pgvector.Vector(new float[] { 0.12f, 0.34f, 0.56f });

var top = await articleService.SearchAsync(q =>
    q.Where(a => a.Embedding!.CosineDistance(queryVector) < 0.2)
     .OrderBy(a => a.Embedding!.L2Distance(queryVector))
     .Take(10));
```

同样也可用 `Expr` 写法。Lambda 写法的 0.2 是常数，为保持一致阈值写成 `Const(0.2)`：

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

两种写法生成的 SQL 完全相同：

```sql
SELECT ... FROM "article"
WHERE ("embedding" <=> (@0)::vector) < 0.2
ORDER BY ("embedding" <-> (@1)::vector)
OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY
```

`<=>` 和 `<->` 分别对应余弦距离和欧氏距离，按升序排列就是由近到远。另外还有一个容易误解的 `<#>`，它返回的是负内积，升序排列同样是最相似的排在前面，但不要把它当作正的相似度分数展示。向量本身始终走参数绑定，不要拼接进 SQL 文本，注入风险和类型推断问题往往都由此而来。

## 索引与性能

如果不建索引，PostgreSQL 只能进行全表扫描，数据量小时还可以接受，数据量增长后就必须建立近似索引。运算符和索引 opclass 必须成对使用：

| 需求 | 运算符 | 索引 opclass |
| --- | --- | --- |
| 欧氏距离最近 | `<->` | `vector_l2_ops` |
| 余弦距离最近 | `<=>` | `vector_cosine_ops` |
| 内积最大 | `<#>` | `vector_ip_ops` |

两种索引的创建示例如下：

```sql
-- HNSW
CREATE INDEX ix_article_embedding_l2 ON "article"
USING hnsw ("embedding" vector_l2_ops);

-- IVFFlat（需要表中已有数据）
CREATE INDEX ix_article_embedding_cosine ON "article"
USING ivfflat ("embedding" vector_cosine_ops) WITH (lists = 100);

-- 查询时调整召回参数
SET hnsw.ef_search = 100;
SET ivfflat.probes = 10;
```

与索引相关的几点说明：

- 排序表达式必须与索引 opclass 对应，用 `<->` 排序却建了 `vector_cosine_ops`，索引不会被用上。
- 带有 `WHERE` 过滤条件时，PostgreSQL 可能选择先过滤再排序，从而放弃使用索引；如果过滤选择性很高，这种做法反而更快，具体采用哪种执行方式，需要结合真实数据用 `EXPLAIN ANALYZE` 判断。
- HNSW 对 `vector` 的维度上限是 2000，超过时考虑 `halfvec` 或降低维度；pgvector 的版本也影响可用运算符，`<+>`（L1）需要较新版本。

## 何时不适合

- **维度很高**：HNSW 对 `vector` 的支持上限是 2000 维，超过后需要降维，或者改用 `halfvec`。
- **强过滤 + 强排序**：既要按多个业务字段过滤，又要按向量排序时，索引通常只能照顾其中一边，这时预过滤、分区甚至专门的搜索服务可能都更划算。
- **装不了扩展**：部分托管 PostgreSQL 不提供 pgvector，只能退回暴力计算，或者改用外部向量库。
- **数据量很小**：几万条以内暴力扫描已经足够快，为此引入向量索引和额外依赖并不划算。
- **需要跨库统一**：文中的类型和运算符只适用于 PostgreSQL，换成其它方言时需要各自实现 `FunctionExpr` 处理器。

## 相关链接

- [返回目录](../README.md)
- [CRUD 指南](../core-usage/crud-guide.md)
- [Lambda 查询指南](../core-usage/lambda-guide.md)
- [数据映射与值转换](../advanced-topics/data-mapping.md)
- [表达式扩展](../extensibility/expression-extension.md)
- [自定义 SqlBuilder](../extensibility/custom-sqlbuilder.md)
- [数据库兼容性](../reference/database-compatibility.md)
- [对象的 JSON 列存储与字段查询](./json-column-mapping.md)
