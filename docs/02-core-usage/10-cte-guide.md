# CTE 指南

LiteOrm 支持使用 `SelectExpr.With(name)` 构建公共表表达式（CTE，`WITH` 子句）。这一章单独说明 CTE 的适用场景、构建方式，以及与 `ExprString` 的边界。

## 1. 什么时候使用 CTE

CTE 适合以下场景：

- 需要在主查询中多次引用同一段子查询结果
- 希望把复杂查询拆成更清晰的多个步骤
- 想保留 `Expr` / `SelectExpr` 的结构化构建能力，而不是完全手写 SQL

如果只是一次性子查询、简单过滤或简单分页，通常直接使用普通 `Expr` / `SelectExpr` 就够了。

## 2. 基础写法

先定义一个 `SelectExpr`，再用 `.With(name)` 包装为 CTE：

```csharp
using static LiteOrm.Common.Expr;
var cteDef = new SelectExpr(
    From(typeof(User)),
    Prop("Id").As("Id"),
    Prop("UserName").As("Name"),
    Prop("Age").As("Age")
);

var query = cteDef.With("ActiveUsers")
    .Where(Prop("Age") >= 18)
    .OrderBy(Prop("Name").Asc())
    .Select(Prop("Name"), Prop("Age"));
```

生成的 SQL 形态：

```sql
WITH [ActiveUsers] AS (
    SELECT [Id] AS [Id], [UserName] AS [Name], [Age] AS [Age]
    FROM [Users]
)
SELECT [Name], [Age]
FROM [ActiveUsers]
WHERE [Age] >= 18
ORDER BY [Name]
```

## 3. 聚合 CTE

CTE 很适合先聚合、再过滤：

```csharp
using static LiteOrm.Common.Expr;
var cteDef = From<User>()
    .Where(Prop("Age") >= 25)
    .GroupBy(Prop("DeptId"))
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount"),
        Prop("Age").Avg().As("AvgAge")
    );

var query = cteDef.With("DeptAdultStats")
    .Where(Prop("UserCount") >= 2)
    .OrderBy(Prop("UserCount").Desc())
    .Select(Prop("DeptId"), Prop("UserCount"), Prop("AvgAge"));
```

## 4. 在 UNION 中复用同一个 CTE 表达式

除了在单个主查询里引用一次，CTE 也可以在 `UNION` / `UNION ALL` 两侧复用：

```csharp
using static LiteOrm.Common.Expr;
var adultUsers = From<User>()
    .Where(Prop("Age") >= 18)
    .Select(
        Prop("UserName").As("Name"),
        Prop("Age").As("Age"))
    .With("AdultUsers");

var query = adultUsers
    .Where(Prop("Age") < 30)
    .Select(Prop("Name"), Prop("Age"), Const("18-29").As("AgeGroup"))
    .UnionAll(
        adultUsers
            .Where(Prop("Age") >= 30)
            .Select(Prop("Name"), Prop("Age"), Const("30+").As("AgeGroup")));
```

这种写法的重点是：

- 先把 `With("AdultUsers")` 的结果保存下来
- 后面在多个分支里继续基于同一个 `CommonTableExpr` 构建查询
- 生成 SQL 时仍只会保留一份 `WITH AdultUsers AS (...)` 定义

## 5. 同别名 CTE 的校验规则

LiteOrm 现在会先收集整棵表达式树里的 CTE，再按别名做校验：

- 同别名且定义**相等**：自动去重，只保留第一份定义写入 `WITH`
- 同别名但定义**不相等**：抛出 `InvalidOperationException`
- 只引用别名、但前面没有先出现完整定义：抛出异常

这意味着你可以在复杂查询里复用同一个 CTE 表达式；或者使用同一个 CTE 别名，但必须保证其定义一致。

## 5.1 递归 CTE

递归 CTE 用于处理树形 / 层次结构数据（如组织架构、分类层级、路径查找等）。LiteOrm 根据数据库的 `ExplicitRecursive` 属性决定是否输出 `RECURSIVE` 关键字。

### 编写递归 CTE

编写递归 CTE 时，需要在 CTE 的定义中引用自身的别名。可以通过创建一个只包含别名的 `CommonTableExpr` 来实现自引用：

```csharp
using static LiteOrm.Common.Expr;

// 锚点部分：查询根节点
var anchor = From<Category>()
    .Where(Prop("ParentId") == 0)
    .Select(
        Prop("Id").As("Id"),
        Prop("ParentId").As("ParentId"),
        Prop("Name").As("Name"),
        Const(1).As("Level"));

// 递归部分：通过自引用别名关联子节点
var selfRef = new CommonTableExpr { Alias = "CategoryTree" };
var recursive = From<Category>()
    .Join(selfRef, Prop("ParentId") == Prop("CategoryTree", "Id"))
    .Select(
        Prop("Id").As("Id"),
        Prop("ParentId").As("ParentId"),
        Prop("Name").As("Name"),
        Prop("CategoryTree", "Level") + 1);

// 合并锚点和递归部分，包装为 CTE
var cteDef = anchor.UnionAll(recursive);
var query = cteDef.With("CategoryTree")
    .Select(Prop("Id"), Prop("Name"), Prop("Level"));
```

### 生成 SQL 的行为

是否在 `WITH` 后追加 `RECURSIVE` 关键字，仅取决于目标数据库的 `SqlBuilder.ExplicitRecursive` 属性：

- `ExplicitRecursive` 为 `true`：所有 CTE 都使用 `WITH RECURSIVE`（无论是否递归）
- `ExplicitRecursive` 为 `false`：所有 CTE 都使用 `WITH`（不带 `RECURSIVE`）

各数据库的 `ExplicitRecursive` 默认值：

| 数据库 | `ExplicitRecursive` | 说明 |
|--------|---------------------|------|
| MySQL / TiDB / OceanBase / GreatDB | `true` | 使用 `WITH RECURSIVE` |
| PostgreSQL / KingbaseES / GaussDB | `true` | 使用 `WITH RECURSIVE` |
| SQLite | `true` | 使用 `WITH RECURSIVE` |
| SQL Server | `false` | 不需要 `RECURSIVE` 关键字 |
| Oracle / 达梦（DM） | `false` | 不需要 `RECURSIVE` 关键字 |

对于 `ExplicitRecursive` 为 `true` 的数据库，生成 SQL 形如：

```sql
WITH RECURSIVE [CategoryTree] AS (
    SELECT [Id], [ParentId], [Name], 1 AS [Level]
    FROM [Categories]
    WHERE [ParentId] = 0
    UNION ALL
    SELECT ... FROM [Categories] JOIN [CategoryTree] ON ...
)
SELECT [Id], [Name], [Level]
FROM [CategoryTree]
```

对于 `ExplicitRecursive` 为 `false` 的数据库（如 SQL Server），只生成 `WITH`（不带 `RECURSIVE`）。

## 6. CTE 序列化规则

当 `Expr` / `SelectExpr` 被序列化为 JSON 时：

- 第一个同别名 CTE 会完整序列化
- 后续等价引用只会序列化别名

例如后续引用会被压缩成：

```json
{"$cte":"ActiveUsers"}
```

反序列化时，LiteOrm 会自动把它还原回首个定义对应的 CTE。

## 7. `ExprString` 与 CTE 的边界

`ExprString` **不支持把 CTE 结构当作 Expr 片段自动展开**。也就是说：

- `SelectExpr.With(name)` / `CommonTableExpr` 是 **Expr / SelectExpr 查询模型**
- `ExprString` 只适合插入普通 `Expr` 条件片段或手写 SQL 片段
- 如果你要通过 `ExprString` 使用 `WITH`，必须**手动写 WITH 部分**

### 7.1 不支持的方式

下面这种思路并不成立：先构造 CTE Expr，再希望它作为 `ExprString` 片段自动嵌入。

```csharp
var cteQuery = cteDef.With("ActiveUsers");
// 不支持把 cteQuery 当成 ExprString 片段自动展开成 WITH SQL
```

### 7.2 正确方式：手动生成 WITH 片段

如果场景必须走 `ExprString` / DAO 原生 SQL，请手动写 `WITH` 部分：

```csharp
int minAge = 18;

var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        SELECT Id, UserName, Age
        FROM Users
        WHERE Age >= {minAge}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

这里的 `WITH ...` 是你手写的 SQL，LiteOrm 只负责继续处理插值参数。

也可通过插入 `SelectExpr` 方式构造：

```csharp
using static LiteOrm.Expr;

Expr cteDef = From(typeof(User))
    .Select(
    Prop("Id"),
    Prop("UserName"),
    Prop("Age")
    ).Where(Prop("Age") >= 18);

var result = await dataViewDAO.Search(
    $"""
    WITH ActiveUsers AS (
        {cteDef}
    )
    SELECT Id, UserName, Age
    FROM ActiveUsers
    """,
    isFull: true
).GetResultAsync();
```

## 8. 相关阅读

- [查询总览](./04-query-overview.md)
- [Lambda 查询指南](./05-lambda-guide.md)
- [Expr 使用指南](./06-expr-guide.md)
- [ExprString 使用指南](./07-exprstring-guide.md)
- [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)
- [AI 指南](../05-reference/05-ai-guide.md)
- [返回文档中心](../README.md)

