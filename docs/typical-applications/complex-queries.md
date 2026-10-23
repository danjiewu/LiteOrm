# 复杂查询实战：同表多关系、动态排序、分页计数、树形方言与条件持久化

基础的 `QuerySource`、`Join` / `JoinRelated`、Lambda、`Expr`、`ExprString` 各自怎么用，已有文档都讲过了。这篇只讲**组合**：一个工单列表页加一个组织树筛选，把「同一张员工表的两种角色」「表头动态排序」「同条件取 count 与当页」「自定义 Lambda 与方言树形 SQL」「保存 / 重载查询条件」串起来，最后用 `SqlGen` 离线看 SQL。

文中 SQL 都是实际渲染结果（除特别注明外为 SQLite 方言），参数值以 `@0`、`@1` 表示。

## 基础能力去哪看

| 能力 | 文档 |
| --- | --- |
| `QuerySource<T>`、`Join` / `JoinRelated` 的链式写法 | [查询总览](../core-usage/query-overview.md) |
| Lambda 语法、多参数 Lambda、外变量参数化 | [Lambda 指南](../core-usage/lambda-guide.md) |
| `[TableJoin]`、`[ForeignType]`、`[ForeignColumn]` 的最小闭环 | [关联查询](../core-usage/associations.md) |
| `RegisterMethodHandler` / `RegisterMemberHandler` 机制 | [表达式扩展](../extensibility/expression-extension.md) |
| `Expr.Lambda`、`To<bool>()`、Lambda 与 Expr 互转 | [Lambda 与 Expr 组合](../core-usage/lambda-expr-mixing.md) |
| `ExprString` 插值、`Expr` 嵌入、`[列名]` 占位 | [ExprString 指南](../core-usage/exprstring-guide.md) |
| Expr 的 JSON 形状、操作符标记 | [Expr JSON 序列化格式](../extensibility/expr-serialization.md) |
| 前端直接构造 Expr JSON | [前端原生 Expr 查询](../extensibility/frontend-native-expr.md) |
| 方言分页原理与 `Skip` / `Take` 基础 | [自定义分页](../advanced-topics/custom-paging.md) |
| 分组聚合与投影对比 | [按需投影](./query-projection.md) |
| CTE 的 `With` / `UnionAll` | [CTE 指南](../core-usage/cte-guide.md) |
| 动态表名与 `TableArgs` | [分表与 TableArgs](../advanced-topics/sharding-and-tableargs.md) |

## 1. 需求与模型

需求（也是后文的验收点）：

- 工单列表按状态、处理人等级、复核人等级、创建时间区间、标题 / 编号关键字筛选。
- 列表可按表头点击排序：创建时间、优先级、状态、编号、处理人姓名、复核人姓名，方向可选。
- 分页第 `page` 页、每页 `pageSize` 条；**同一组条件**同时给出总数与当页数据。
- 顶部状态条按状态汇总数量（第二条 SQL）。
- 组织树：查询某节点及其所有下级节点；工单列表可以按组织树筛选。
- 用户可以把当前筛选保存为视图，下次打开时重新加载并继续叠加条件。

模型是工单加组织。处理人和复核人都来自 `Employees`，用**继承同一实体的派生类型**区分两条关系：

```csharp
public enum WorkOrderStatus { Open = 1, Processing = 2, Closed = 3 }

[Table("Employees")]
public class Employee
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
    [Column("Level")] public int Level { get; set; }
}

/// <summary>处理人：Employees 表的一种业务角色。</summary>
public class Assignee : Employee { }

/// <summary>复核人：Employees 表的另一种业务角色。</summary>
public class Reviewer : Employee { }

[Table("OrgUnits")]
public class OrgUnit
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("ParentId")] public int? ParentId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
    [Column("Level")] public int Level { get; set; }
}

[Table("WorkOrders")]
public class WorkOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("Code")] public string Code { get; set; } = "";
    [Column("Title")] public string Title { get; set; } = "";
    [Column("Status")] public int Status { get; set; }
    [Column("Priority")] public int Priority { get; set; }
    [Column("OrgUnitId")] public int OrgUnitId { get; set; }

    [Column("AssigneeId")]
    [ForeignType(typeof(Assignee), JoinType = TableJoinType.Inner)]
    public int AssigneeId { get; set; }

    [Column("ReviewerId")]
    [ForeignType(typeof(Reviewer), JoinType = TableJoinType.Left)]
    public int? ReviewerId { get; set; }

    [Column("CreatedTime")] public DateTime CreatedTime { get; set; }
    [Column("DueTime")] public DateTime DueTime { get; set; }
}

/// <summary>列表页视图：只加展示列，读写仍用 WorkOrder。</summary>
public class WorkOrderView : WorkOrder
{
    [ForeignColumn(typeof(Assignee), Property = nameof(Employee.Name))]
    public string AssigneeName { get; set; } = "";

    [ForeignColumn(typeof(Reviewer), Property = nameof(Employee.Name))]
    public string? ReviewerName { get; set; }

    [ForeignColumn(typeof(Assignee), Property = nameof(Employee.Level))]
    public int AssigneeLevel { get; set; }

    [ForeignColumn(typeof(Reviewer), Property = nameof(Employee.Level))]
    public int? ReviewerLevel { get; set; }
}

public class WorkOrderListItem
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string AssigneeName { get; set; } = "";
    public string? ReviewerName { get; set; }
    public int Priority { get; set; }
    public DateTime CreatedTime { get; set; }
}

public sealed class WorkOrderQuery
{
    public int Status { get; init; }
    public int MinAssigneeLevel { get; init; }
    public int MinReviewerLevel { get; init; }
    public int? OrgUnitId { get; init; }          // 按组织树筛选，见第 4 节
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string? Keyword { get; init; }
    public string? SortBy { get; init; }          // createdTime / priority / status / code / assigneeName / reviewerName
    public bool Desc { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
```

**为什么派生类型能区分关系**（全文只讲这一次）：

- `[Table]` 的 `Inherited` 默认为 `true`，`Assignee` / `Reviewer` 自动继承表名 `Employees` 与全部列。
- 两者的 `DefinitionType` 分别是 `Assignee`、`Reviewer`。`JoinRelated` 用 `DefinitionType.IsAssignableFrom(foreignType)` 匹配，兄弟派生类型互不匹配。
- 用基类 `Employee` 连接匹配不到（`Assignee.IsAssignableFrom(Employee) == false`），会抛「No relation ... found in metadata」。
- 同一类型加不同 `Alias` 也不行：`BuildRelatedLogic` 只按类型匹配，不看别名。
- 派生类型只用于表达关系，不加 `[ForeignColumn]`；展示列放在 `WorkOrderView` 上，避免自动 JOIN 与显式连接重复。

## 2. 同一张表的两个角色：`JoinRelated`

```csharp
var query = Expr.From<WorkOrder>()
    .JoinRelated<Assignee>(TableJoinType.Inner)   // 处理人：必填，内连接
    .JoinRelated<Reviewer>(TableJoinType.Left)    // 复核人：可空，保留没有复核人的工单
    .Where((w, assignee, reviewer) => w.Status != (int)WorkOrderStatus.Closed
                                   && assignee.Level >= 3)
    .Select((w, assignee, reviewer) => new WorkOrderListItem
    {
        Id = w.Id,
        Code = w.Code,
        Title = w.Title,
        AssigneeName = assignee.Name,
        ReviewerName = reviewer.Name,
        Priority = w.Priority,
        CreatedTime = w.CreatedTime
    });

List<WorkOrderListItem> items =
    await workOrderDAO.SearchAs<WorkOrderListItem>(query).ToListAsync(ct);
```

渲染结果（SQLite）：

```sql
SELECT "T0"."Id", "T0"."Code", "T0"."Title",
       "T1"."Name" AS "AssigneeName", "T2"."Name" AS "ReviewerName",
       "T0"."Priority", "T0"."CreatedTime"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "T1" ON "T0"."AssigneeId" = "T1"."Id"
LEFT JOIN "Employees" "T2" ON "T0"."ReviewerId" = "T2"."Id"
WHERE "T0"."Status" <> 3 AND "T1"."Level" >= 3
```

要点：

- 不写 ON，由 `[ForeignType]` 元数据补全；第二段自动回退到主表 `T0` 找关系。
- 同一物理表出现两次，靠 `T1` / `T2` 别名区分，不需要手写别名。
- 连接后元素是扁平元组，`Where` / `Select` 一表一个参数（`TupleQueryableExtensions`）。
- 要附加条件时用 `JoinRelated<T>(on, joinType)`，显式条件与自动条件按 AND 合并。
- Lambda 里的字面量 `3` 直接内联；换成外部变量（如 `minLevel`）会变成参数 `@n`。
- 查询方法在 `ObjectViewDAO<T>` 上（`ObjectDAO<T>` 只有增删改）；链式结果直接交给 `SearchAs<TResult>(IQueryable<TResult>)`，不需要把链转回 `Expr`。

## 3. 列表页：筛选、动态排序、分页与同条件 count

### 3.1 同一份筛选条件

```csharp
using static LiteOrm.Common.Expr;

LogicExpr BuildFilter(WorkOrderQuery q)
{
    LogicExpr filter = Prop(nameof(WorkOrder.Status)) == q.Status;
    filter &= Prop(nameof(WorkOrderView.AssigneeLevel)) >= q.MinAssigneeLevel;
    filter &= Prop(nameof(WorkOrder.ReviewerId)).IsNull()
            | Prop(nameof(WorkOrderView.ReviewerLevel)) >= q.MinReviewerLevel;
    filter &= Prop(nameof(WorkOrder.CreatedTime)) >= q.From;
    filter &= Prop(nameof(WorkOrder.CreatedTime)) < q.To;

    if (q.OrgUnitId is int orgUnitId)                 // 第 4 节的树形片段
        filter &= Expr.Sql("OrgSubTree", orgUnitId);

    if (!string.IsNullOrWhiteSpace(q.Keyword))
        filter &= Expr.Sql("KeywordSearch", q.Keyword.Trim());

    return filter;
}
```

`filter` 只构建一次，count 与当页共用。`Prop(nameof(WorkOrderView.AssigneeLevel))` 会落到视图关联列 `"Assignee"."Level"`。

### 3.2 表头排序：用 Expr，而不是拼 Lambda

```csharp
// 只认白名单键，绝不把用户输入直接当属性名
private static readonly Dictionary<string, string> SortableFields =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["createdTime"]  = nameof(WorkOrder.CreatedTime),
        ["priority"]     = nameof(WorkOrder.Priority),
        ["status"]       = nameof(WorkOrder.Status),
        ["code"]         = nameof(WorkOrder.Code),
        ["assigneeName"] = nameof(WorkOrderView.AssigneeName),
        ["reviewerName"] = nameof(WorkOrderView.ReviewerName),
    };

OrderByItemExpr[] BuildOrderBys(WorkOrderQuery q)
{
    var items = new List<OrderByItemExpr>(2);
    if (q.SortBy is not null && SortableFields.TryGetValue(q.SortBy, out string? prop))
        items.Add(q.Desc ? Prop(prop).Desc() : Prop(prop).Asc());

    items.Add(Prop(nameof(WorkOrder.Id)).Desc());   // 唯一键兜底，保证页边界稳定
    return items.ToArray();
}
```

如果改用 Lambda，每个字段 × 两个方向都要写一个分支：

```csharp
IOrderedQueryable<WorkOrderView> ordered = q.SortBy switch
{
    "createdTime"  => q.Desc ? src.OrderByDescending(v => v.CreatedTime) : src.OrderBy(v => v.CreatedTime),
    "priority"     => q.Desc ? src.OrderByDescending(v => v.Priority) : src.OrderBy(v => v.Priority),
    // ... 六个字段 × 两个方向 = 12 个分支
    _              => src.OrderByDescending(v => v.Id)
};
ordered = ordered.ThenByDescending(v => v.Id);
```

Expr 版是「字段名 → 表达式」一次映射，方向用 `Desc()` / `Asc()` 切换；新增可排序列只加一行白名单。

### 3.3 同一份 `filter` 取总数与当页

```csharp
SelectExpr BuildPageExpr(WorkOrderQuery q, LogicExpr filter)
{
    int skip = (q.Page - 1) * q.PageSize;
    return Expr.From(typeof(WorkOrderView))
        .Where(filter)
        .OrderBy(BuildOrderBys(q))
        .Section(skip, q.PageSize)
        .Select(
            new SelectItemExpr(Prop(nameof(WorkOrder.Id)), nameof(WorkOrderListItem.Id)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Code)), nameof(WorkOrderListItem.Code)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Title)), nameof(WorkOrderListItem.Title)),
            new SelectItemExpr(Prop(nameof(WorkOrderView.AssigneeName)), nameof(WorkOrderListItem.AssigneeName)),
            new SelectItemExpr(Prop(nameof(WorkOrderView.ReviewerName)), nameof(WorkOrderListItem.ReviewerName)),
            new SelectItemExpr(Prop(nameof(WorkOrder.Priority)), nameof(WorkOrderListItem.Priority)),
            new SelectItemExpr(Prop(nameof(WorkOrder.CreatedTime)), nameof(WorkOrderListItem.CreatedTime)));
}

LogicExpr filter = BuildFilter(q);

// 总数：不带 ORDER BY / 分页
int total = await workOrderService.CountAsync(filter, cancellationToken: ct);

// 当页：Where → OrderBy → Section → Select
List<WorkOrderListItem> page = await workOrderService
    .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);
```

count SQL（SQLite，树形片段与关键字片段用 `...` 省略）：

```sql
SELECT Count(1)
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE "T0"."Status" = @0
  AND "Assignee"."Level" >= @1
  AND ("T0"."ReviewerId" IS NULL OR "Reviewer"."Level" >= @2)
  AND "T0"."CreatedTime" >= @3 AND "T0"."CreatedTime" < @4
  AND ...树形子查询...
  AND ...关键字片段...
```

当页 SQL（`SortBy = "assigneeName"`、`Desc = true`、`Page = 2`、`PageSize = 20`）：

```sql
SELECT "T0"."Id", "T0"."Code", "T0"."Title",
       "Assignee"."Name" AS "AssigneeName", "Reviewer"."Name" AS "ReviewerName",
       "T0"."Priority", "T0"."CreatedTime"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE ...
ORDER BY "Assignee"."Name" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
```

换 `SortBy` 只影响 `ORDER BY`；`SortBy = "createdTime"`、`Desc = false`、第 1 页 10 条时是 `ORDER BY "T0"."CreatedTime", "T0"."Id" DESC` + `LIMIT 10`。

要点：

- `filter` 只构建一次，count 与当页共用；count 分支不能带 `OrderBy` / `Skip` / `Take`。
- 动态排序先查白名单；直接 `Prop(userInput)` 等于把列名交给用户，必须禁止。
- 末尾补 `Id` 兜底：排序值可能重复，缺唯一键时分页会重复或漏行。
- 两个关系都是多对一，`Count(1)` 不会放大；若换一对多连接，改 `Count(DISTINCT "T0"."Id")`。
- 视图的 JOIN 类型来自 `[ForeignType]` 元数据（处理人 Inner、复核人 Left），与第 2 节显式连接语义一致。
- `Section` 在 SQLite 里内联为 `LIMIT n OFFSET m`，不进入参数列表；MySQL 是 `LIMIT m,n`，Oracle 是 `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`（细节见[自定义分页](../advanced-topics/custom-paging.md)）。
- count 与当页是两次执行；要严格一致时放进同一事务或快照读。

## 4. 树形查询：自定义 Lambda + `GenericSqlExpr` 生成方言 SQL

目标：写 `u.InSubTreeOf(rootId)`，在 Oracle 上生成 `START WITH ... CONNECT BY`，在 MySQL / SQLite / PostgreSQL 上生成递归 CTE。

### 4.1 业务方法（只为编译，不在内存执行）

```csharp
public static class OrgUnitExtensions
{
    /// <summary>是否位于 rootId 的子树中（含自身）。</summary>
    public static bool InSubTreeOf(this OrgUnit unit, int rootId)
        => unit.Id == rootId;   // 占位实现，查询时由解析器接管
}
```

### 4.2 注册自定义 Lambda 解析：把方法展开成 `GenericSqlExpr`

```csharp
static WorkOrderQueries()
{
    // 先注册片段，再注册 Lambda 处理器；Expr.Sql 在解析时要求 key 已存在
    RegisterOrgSubTree();

    LambdaExprConverter.RegisterMethodHandler(
        typeof(OrgUnitExtensions), nameof(OrgUnitExtensions.InSubTreeOf),
        (node, converter) =>
        {
            // 扩展方法在表达式里是静态调用：node.Object == null，
            // 接收者在 Arguments[0]，根节点 Id 在 Arguments[1]。
            // 直接把参数表达式交给 GenericSqlExpr：参数化、方言、序列化都在后段完成。
            return Expr.Sql("OrgSubTree", converter.Convert(node.Arguments[1]));
        });
}
```

### 4.3 `GenericSqlExpr`：参数直接用 Expr 输出，递归 CTE 用 Expr 构造后 `ToSql`

```csharp
static void RegisterOrgSubTree()
{
    GenericSqlExpr.Register("OrgSubTree", (context, arg) =>
    {
        // Lambda 解析传入的是 Expr；保存后重载的可能是原始值
        ValueTypeExpr root = arg switch
        {
            ValueTypeExpr value => value,
            null => throw new NotSupportedException("OrgSubTree 需要一个根节点参数。"),
            _ => Expr.Value(arg)          // 渲染成参数，不内联
        };

        ISqlBuilder builder = context.SqlBuilder;

        // 同一片段既用于 OrgUnit（Id），也用于 WorkOrderView（OrgUnitId）
        string column = context.Table?.DefinitionType == typeof(OrgUnit)
            ? nameof(OrgUnit.Id)
            : nameof(WorkOrder.OrgUnitId);
        string outerColumn = context.SingleTable || context.DefaultTableAliasName is null
            ? builder.ToSqlName(column)
            : $"{builder.ToSqlName(context.DefaultTableAliasName)}.{builder.ToSqlName(column)}";

        if (builder is OracleBuilder)
        {
            // Oracle 的层次查询无法用 Expr 表达，这里手写；
            // 根节点仍由 Expr 输出参数（:0），不手工 new Param
            string table = builder.ToSqlName(
                TableInfoProvider.Instance.GetTableDefinition(typeof(OrgUnit)).Name!);
            string id = builder.ToSqlName(nameof(OrgUnit.Id));
            string parentId = builder.ToSqlName(nameof(OrgUnit.ParentId));
            string rootSql = root.ToSql(context);

            return $"{outerColumn} IN (" +
                   $"SELECT {id} FROM {table} " +
                   $"START WITH {id} = {rootSql} " +
                   $"CONNECT BY PRIOR {id} = {parentId})";
        }

        // MySQL / SQLite / PostgreSQL：递归 CTE 用 Expr 构造，ToSql 输出子查询
        using (context.BeginScope())   // CTE 渲染会改写默认别名，作用域内隔离
        {
            return $"{outerColumn} IN ({BuildOrgSubTreeQuery(root).ToSql(context)})";
        }
    });
}

/// <summary>组织子树查询：锚点 + 自引用递归，包装成 CTE。</summary>
static SelectExpr BuildOrgSubTreeQuery(ValueTypeExpr rootId)
{
    SelectExpr anchor = Expr.From(typeof(OrgUnit))
        .Where(Expr.Prop(nameof(OrgUnit.Id)) == rootId)
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

    var selfRef = new CommonTableExpr { Alias = "OrgTree" };

    SelectExpr recursive = Expr.From(typeof(OrgUnit))
        .Join(selfRef, Expr.Prop(nameof(OrgUnit.ParentId)) == Expr.Prop("OrgTree", nameof(OrgUnit.Id)))
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

    return anchor.UnionAll(recursive)
        .With("OrgTree")
        .Select(new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));
}
```

### 4.4 两种调用方式，生成同一段 SQL

```csharp
int rootId = 1001;

// 方式一：业务方法进 Lambda（解析器展开为 OrgSubTree 片段）
List<OrgUnit> nodes = await orgUnitDAO
    .Search(q => q.Where(u => u.InSubTreeOf(rootId))
                  .OrderBy(u => u.Level))
    .ToListAsync(ct);

// 方式二：直接拼 Expr（适合动态条件，也适合保存为历史条件）
List<OrgUnit> sameNodes = await orgUnitDAO
    .Search(Expr.Sql("OrgSubTree", rootId))
    .ToListAsync(ct);
```

Oracle（`OracleBuilder`；Oracle 方言会把标识符转成大写）：

```sql
SELECT "T0"."ID", "T0"."PARENTID", "T0"."NAME", "T0"."LEVEL"
FROM "ORGUNITS" "T0"
WHERE "T0"."ID" IN (
    SELECT "ID" FROM "ORGUNITS"
    START WITH "ID" = :0
    CONNECT BY PRIOR "ID" = "PARENTID"
)
ORDER BY "T0"."LEVEL"
```

SQLite / MySQL / PostgreSQL（递归 CTE 由 Expr 构造；别名为实测结果：锚点 `T1`、递归段 `T2`，CTE 自身不追加别名）：

```sql
SELECT "T0"."Id", "T0"."ParentId", "T0"."Name", "T0"."Level"
FROM "OrgUnits" "T0"
WHERE "T0"."Id" IN (
    WITH RECURSIVE "OrgTree" AS (
        SELECT "T1"."Id"
        FROM "OrgUnits" "T1"
        WHERE "T1"."Id" = @0
        UNION ALL
        SELECT "T2"."Id"
        FROM "OrgUnits" "T2"
        LEFT JOIN "OrgTree" ON "T2"."ParentId" = "OrgTree"."Id"
    )
    SELECT "Id" FROM "OrgTree"
)
ORDER BY "T0"."Level"
```

在工单列表里复用同一片段，外层列自动切换为 `OrgUnitId`：

```csharp
// 等价于第 3 节的 filter &= Expr.Sql("OrgSubTree", orgUnitId)
// 生成："T0"."OrgUnitId" IN (...树形子查询...)
List<WorkOrderListItem> page = await workOrderDAO
    .SearchAs<WorkOrderListItem>(BuildPageExpr(q, BuildFilter(q)))
    .ToListAsync(ct);
```

### 4.5 要点

- 外层列按 `context.Table` 的 `DefinitionType` 取 `Id` 或 `OrgUnitId`，并自动带上当前默认别名；单表模式（`SingleTable`）下不加前缀。
- Oracle 分支的表名走 `TableInfoProvider` + `FormatTableName`，分表 `TableArgs` 自动生效。
- 参数全部由 Expr 输出：Oracle 用 `root.ToSql(context)` 得到 `:0`；递归 CTE 里根节点作为值表达式参与 `Where`，由 `ToSql` 统一追加参数。不要手拼值或手工 `new Param`。
- CTE 用 Expr 构造（`From → Where → Select`、`UnionAll`、`With("OrgTree")`）再 `ToSql(context)`，别名、参数、`RECURSIVE` 关键字都走框架规则。
- CTE 渲染会改写 `DefaultTableAliasName`，必须放在 `context.BeginScope()` 内，避免影响外层条件。
- `Arg` 可以是 Expr（Lambda 解析传入），也可以是原始值（保存后重载）；处理器开头的 `switch` 统一成 `ValueTypeExpr`。
- `Expr.Sql(key, arg)` 在解析时要求 key 已注册；注册放在静态构造函数或启动初始化里。
- `Arg` 能否随 Expr JSON 保存，取决于它的类型：原始值可以，`ValueExpr` 不行（见 5.5）。
- 有环数据时 Oracle 加 `NOCYCLE`，MySQL / SQLite 在递归段附带 `Depth` 并限制最大深度。

### 4.6 顺带：可复用的多列关键字片段

树形片段之外的通用片段只需要参数，不需要方言，写法更短：

```csharp
GenericSqlExpr.Register("KeywordSearch", (context, arg) =>
{
    string keyword = arg as string ?? string.Empty;
    // 用 Expr 输出参数，和树形片段同一套参数规则
    string param = Expr.Value($"%{keyword}%").ToSql(context);
    string title = context.SqlBuilder.ToSqlName(nameof(WorkOrder.Title));
    string code = context.SqlBuilder.ToSqlName(nameof(WorkOrder.Code));
    return $"({title} LIKE {param} OR {code} LIKE {param})";
});
```

```sql
WHERE ("Title" LIKE @0 OR "Code" LIKE @0)
```

两列共用一个参数；区别只在树形片段需要读方言与表名。

## 5. 保存与重新加载历史查询条件：函数与参数怎么选

### 5.1 保存（只存条件，不存分页）

```csharp
public sealed class SavedView
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string FilterJson { get; set; } = "";   // 存 LogicExpr 的 JSON
    public int OwnerId { get; set; }
}

static readonly JsonSerializerOptions SaveOptions = new()
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};

// 用户点“保存视图”
string json = JsonSerializer.Serialize<Expr>(BuildFilter(q), SaveOptions);
```

生成的条件 JSON：

```json
{
  "$and": [
    { "$": "==", "Left": { "#": "Status" }, "Right": { "@": 1 } },
    { "$": "contains", "Left": { "#": "Title" }, "Right": { "@": "账单" } },
    { "$": "sql", "Key": "OrgSubTree", "Arg": 1001 }
  ]
}
```

（实际输出是紧凑单行；这里为阅读换行。`{"@": {"$datetime": "..."}}` 表示 `DateTime` 常量，`{"$": "func", "Now": []}` 表示数据库函数。）

### 5.2 重新加载并继续叠加

```csharp
// 打开已保存视图
LogicExpr? saved = JsonSerializer.Deserialize<Expr>(savedView.FilterJson)?.AsLogic()
    ?? throw new InvalidDataException("保存的查询条件无法解析。");

// 当前请求条件继续与历史条件合并
LogicExpr filter = saved & BuildFilter(q);

int total = await workOrderService.CountAsync(filter, cancellationToken: ct);
List<WorkOrderListItem> page = await workOrderService
    .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);
```

排序与分页不落库，仍由当前请求决定；历史条件只负责「筛哪些行」。

### 5.3 重载后 `DateTime.Now` 的两种结果

同一条时间条件，写法不同，保存后再加载的行为完全不同：

```csharp
// A. Lambda：解析为数据库函数，每次执行由数据库取当前时间
List<WorkOrder> byFunction = await workOrderDAO
    .Search(q => q.Where(w => w.DueTime < DateTime.Now))
    .ToListAsync(ct);

// B. Expr + Const：解析为参数，值在构建时固定
LogicExpr frozen = Prop(nameof(WorkOrder.DueTime)) < Expr.Const(DateTime.Now);

// C. Expr + Now()：解析为数据库函数，且可 JSON 往返
LogicExpr dynamicExpr = Prop(nameof(WorkOrder.DueTime)) < Expr.Now();
```

生成 SQL（SQLite）：

```sql
-- A / C
"T0"."DueTime" < datetime('now', 'localtime')

-- B
"T0"."DueTime" < @0        -- 参数值：2026-10-23T10:30:45.123
```

Oracle / MySQL：

```sql
-- A / C
"T0"."DUETIME" < sysdate          -- Oracle
`T0`.`DueTime` < NOW()            -- MySQL
```

三种写法保存成 JSON 后：

```json
// B：时间被冻结在保存那一刻
{ "$": "<", "Left": { "#": "DueTime" }, "Right": { "$datetime": "2026-10-23T10:30:45.123+08:00" } }

// C：函数被保留，重载后仍然动态
{ "$": "<", "Left": { "#": "DueTime" }, "Right": { "$": "func", "Now": [] } }
```

对比：

| 写法 | 生成形态 | 求值时机 | 保存后重载 |
| --- | --- | --- | --- |
| `w => w.DueTime < DateTime.Now` | 方言函数（`datetime('now','localtime')` / `NOW()` / `sysdate`） | 每次执行，数据库时钟 | 先 `Expr.Lambda<T>(...)` 转成 Expr 后可保存，仍是函数 |
| `Prop(...) < Expr.Const(DateTime.Now)` | 参数 `@0`（DateTime 不会被内联） | 构建时，应用时钟 | JSON 里是固定时间，重载后不再变化 |
| `Prop(...) < Expr.Now()` | 方言函数 | 每次执行，数据库时钟 | JSON 保留 `{"$":"func","Now":[]}`，重载后仍动态 |
| Lambda 外 `var now = DateTime.Now;` 再进 Lambda | 参数 | 赋值时 | 固定值（基础规则见 [Lambda 指南](../core-usage/lambda-guide.md)） |

### 5.4 历史条件重载后的适用场景

| 场景 | 选函数还是参数 | 写法 | 重载后的行为 |
| --- | --- | --- | --- |
| 最近 7 天、未逾期、待办提醒 | 函数 | `Expr.Now()` | 每次执行重新取数据库时间 |
| 报表快照、审计复现、对账 | 参数 | `Expr.Const(DateTime.Now)` | 冻结在保存那一刻 |
| 用户选择的绝对日期区间 | 参数 | `Prop(...) >= q.From`（外部变量） | 固定为用户选的值 |
| 依赖数据库时区口径 | 函数 | `Expr.Now()` | 跟随数据库时区 |
| 依赖应用时区口径 | 参数 | `Expr.Const(DateTime.Now)` | 跟随应用时区 |

一句话：需要「相对执行时间」用函数，需要「保存时的快照」用参数。

### 5.5 要点

- 保存的是 `Expr`，不是 `Expression<Func<T,bool>>`；从 Lambda 起步时先 `Expr.Lambda<T>(...)` 得到 `LogicExpr`，它才能序列化。
- 只存条件（`LogicExpr`）而不是整条 `SelectExpr`：分页与排序由当前请求决定；确实要复现整个视图时再保存 `SelectExpr`。
- `GenericSqlExpr` 只会恢复 `Key` + `Arg`；加载端必须在渲染前注册同名 key。
- **`Arg` 必须是可直接 JSON 序列化的原始值。** Lambda 解析得到的 `Arg` 是 `ValueExpr`（`Expr` 对象），无论 const 与否，`JsonSerializer.Serialize` 都会抛 `InvalidOperationException: Type 'LiteOrm.Common.ValueExpr' is not registered in ExprJsonSerializerContext.`。要把带树形条件的查询保存成历史视图，请用原始值构造 `Expr.Sql("OrgSubTree", rootId)`（第 3 节就是这种写法），JSON 为 `{"$":"sql","Key":"OrgSubTree","Arg":1001}`，重载后 `Arg` 仍是 `int`。
- 「函数」与「参数」的差别不在 SQL 长短，而在**求值位置**：函数在数据库、参数在应用。
- 时区与精度要对齐：`sysdate` / `NOW()` / `datetime('now','localtime')` 取数据库时区，`DateTime.Now` / `Expr.Const` 取应用时区；跨时区部署时要统一。
- `DateTime.Today` 同理：Lambda 里是 `CURRENT_DATE` / `curdate()` / `trunc(sysdate)`，参数写法是固定日期。
- `ExprString` / `RawSql` 不可序列化，不要放进可保存的条件。
- 已保存 JSON 建议带版本号（如外层包一层 `"v": 1`），便于以后调整结构。
- 底层格式与标记说明见 [Expr JSON 序列化格式](../extensibility/expr-serialization.md)，前端构造见[前端原生 Expr 查询](../extensibility/frontend-native-expr.md)。

## 6. Lambda / Expr / ExprString 混用：一个接口三段写法

```csharp
public async Task<WorkOrderPageResult> GetPageAsync(WorkOrderQuery q, CancellationToken ct)
{
    // 1) 固定业务基线用 Lambda 写，转成可继续拼装的 Expr
    LogicExpr filter = Expr.Lambda<WorkOrderView>(
        v => v.Status != (int)WorkOrderStatus.Closed)!;

    // 2) 动态筛选、动态排序继续用 Expr 叠加
    filter &= BuildFilter(q);
    OrderByItemExpr[] orderBys = BuildOrderBys(q);

    // 3) 同一条件：总数 + 当页
    int total = await workOrderService.CountAsync(filter, cancellationToken: ct);
    List<WorkOrderListItem> items = await workOrderService
        .SearchAsAsync<WorkOrderListItem>(BuildPageExpr(q, filter), cancellationToken: ct);

    // 4) 顶部状态条：自定义 DAO 用 ExprString 手写 GROUP BY，直接插入同一个 filter
    List<StatusSummary> summary = await workOrderDAO.QueryStatusSummaryAsync(filter, ct);

    return new WorkOrderPageResult(total, items, summary);
}
```

自定义 DAO（`ObjectViewDAO<WorkOrderView>`）：

```csharp
public class WorkOrderDAO : ObjectViewDAO<WorkOrderView>
{
    public WorkOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    public Task<List<StatusSummary>> QueryStatusSummaryAsync(LogicExpr filter, CancellationToken ct = default)
        => SearchAs<StatusSummary>(
                $"SELECT [Status], COUNT(1) AS [Total] FROM {From} WHERE {filter} GROUP BY [Status]")
            .ToListAsync(ct);
}

public sealed class StatusSummary
{
    public int Status { get; set; }
    public int Total { get; set; }
}
```

生成的第二条 SQL（SQLite）：

```sql
SELECT "Status", COUNT(1) AS "Total"
FROM "WorkOrders" "T0"
INNER JOIN "Employees" "Assignee" ON "T0"."AssigneeId" = "Assignee"."Id"
LEFT JOIN "Employees" "Reviewer" ON "T0"."ReviewerId" = "Reviewer"."Id"
WHERE "T0"."Status" = @0
  AND "Assignee"."Level" >= @1
  AND ("T0"."ReviewerId" IS NULL OR "Reviewer"."Level" >= @2)
  ...
GROUP BY "Status"
```

`{From}` 是 DAO 的预定义标记（整个 FROM 子句，含视图 JOIN），`filter` 是插值进来的 `Expr`；两者共用 DAO 的视图元数据，所以 `"Assignee"."Level"` 这类关联列别名一致。

选型：

| 场景 | 写法 | 原因 |
| --- | --- | --- |
| 固定业务基线条件 | Lambda 或 `Expr.Lambda<T>` | 可读、可重构、编译期校验 |
| 请求参数决定的筛选项 | Expr | 条件数量与组合在运行时决定 |
| 表头排序 | Expr（白名单 + `Asc` / `Desc`） | 单条映射，避免 switch 爆炸 |
| 与 Lambda 查询共存 | `expr.To<bool>()` | 动态片段嵌回强类型查询 |
| 重复出现、需要参数化的 SQL 片段 | `GenericSqlExpr` | 一次注册、统一参数化、可按方言渲染 |
| 聚合 / UNION / 方言相关的手写 SQL | `ExprString` | Expr 不适合表达的语句，仍能复用 `{From}` 与插入 `Expr` |
| 保存历史条件 | Expr JSON | Lambda / ExprString 不能直接持久化 |
| 排查最终 SQL | `SqlGen` | 离线、可换方言 |

要点：

- `Expr.Lambda<T>` 把 Lambda 变成 `LogicExpr`，之后就能和请求条件按 `&` / `|` 组合。
- `ExprString` 里插入 `Expr` 会沿用 Expr 的参数化；插入普通值也会自动命名参数；`[Status]` 由方言替换引用符。
- `{From}` 与插入的 `Expr` 共用 DAO 的建表上下文；若发现别名不一致，先检查是否混用了不同视图的 DAO。
- `ExprString` 只在 DAO 层可用，Service 不暴露；需要时自定义 DAO 子类（构造参数注入 `SessionManager`）。

## 7. 和其他 ORM 的对照

措辞留有余地，以各版本实际能力为准：

| 本场景的要求 | LiteOrm 写法 | 常见 EF Core 做法 | 常见 Dapper 做法 |
| --- | --- | --- | --- |
| 同一张表两条业务关系 | 两个派生类型 + 字段上的 `[ForeignType]`；`JoinRelated<Assignee>()` / `JoinRelated<Reviewer>()` 自动补 ON | 两个导航属性或两次 `HasOne`，查询里两次 `Join` 并手写 ON | 手写两次 JOIN 与两个别名，自行映射 |
| 视图按同一套元数据自动 JOIN | `WorkOrderView` 的 `[ForeignColumn]` 复用派生类型 | 投影 + `Include` 或手工 `Join` | 手写 JOIN + 列别名 |
| 表头点击动态排序 | 白名单 + `OrderByItemExpr[]`，一行一个字段 | switch + 各 `OrderBy` 分支，或引入 Dynamic LINQ | SQL 字符串拼接，无编译期约束 |
| 同一条件取总数与当页 | 一个 `filter` 用两次（`CountAsync` / `Where`） | 保存 `IQueryable` 分支或重复条件 | 两条 SQL，条件重复 |
| 树形查询按方言生成 | `GenericSqlExpr` + `context.SqlBuilder` 分支（`CONNECT BY` / `WITH RECURSIVE`） | 手写 SQL 或 `FromSql`，方言差异自己处理 | 直接写方言 SQL |
| 业务方法进 SQL | 一次 `RegisterMethodHandler` | 自定义方法需要能翻译，否则退回客户端求值 | 直接写 SQL 函数 |
| 保存并重载查询条件 | `Expr` JSON 往返，含 `GenericSqlExpr` 的 key / arg | 表达式树无内置序列化，通常存 DTO 再重建 | 无对应能力，需自己存 SQL |
| 离线查看任意方言 SQL | `SqlGen` 换 `SqlBuilder` | `ToQueryString()` 取决于当前 provider | SQL 本就是手写的 |

只写差异：LiteOrm 的不同点是**元数据补 ON、连接后仍一表一参数、同一条件可直接复用、查询条件可序列化、Expr 可离线渲染成任意方言**。

## 8. 用 SqlGen 调试查询

### 8.1 最小用法

```csharp
using LiteOrm;
using LiteOrm.CodeGen;
using LiteOrm.Common;

var sqlGen = new SqlGen(typeof(WorkOrderView))
{
    SqlBuilder = SQLiteBuilder.Instance          // 默认是通用 SqlBuilder，排查时显式指定方言
};

PreparedSql prepared = sqlGen.ToSql(BuildPageExpr(q, filter));

Console.WriteLine(prepared.Sql);
foreach (Param p in prepared.Params)
    Console.WriteLine($"{p.Name} = {p.Value} ({p.DbType})");
```

函数与 Lambda 处理器在首次使用 `SqlBuilder` / `LambdaExprConverter` 时自动初始化，独立控制台无需手动调用 `Initialize`。

### 8.2 调试当页 SQL

链尾是 `SelectExpr`（只有 `Select` / `Update` / `Delete` 输出完整语句）。换 `SortBy` 即可对比 `ORDER BY`：

```csharp
foreach (string sortBy in new[] { "createdTime", "priority", "assigneeName" })
{
    var probe = new WorkOrderQuery { SortBy = sortBy, Page = 2, PageSize = 20 /* ... */ };
    Console.WriteLine(sqlGen.ToSql(BuildPageExpr(probe, BuildFilter(probe))).Sql);
}
```

### 8.3 调试 count

与 `CountAsync` 的规则一致：先 `ToSource` 再包 `Count(1)`。

```csharp
Expr countExpr = new SelectExpr(
    filter.ToSource<WorkOrderView>(),
    new SelectItemExpr(Expr.Aggregate("Count", Expr.Const(1))));

PreparedSql countSql = sqlGen.ToSql(countExpr);
```

### 8.4 调试 `JoinRelated` 查询

链尾 `GetQueryExpr()`，再交给 SqlGen：

```csharp
SqlSegment joinExpr = query.GetQueryExpr();   // 第 2 节的 IQueryable 链
PreparedSql joinSql = sqlGen.ToSql(joinExpr);
```

### 8.5 调试树形片段：同一份 Expr 换方言，不用连数据库

```csharp
// 独立控制台里要先触发注册（静态构造函数或显式调用 RegisterOrgSubTree）
RegisterOrgSubTree();

var treeExpr = new SelectExpr(
    Expr.From(typeof(OrgUnit)).Where(Expr.Sql("OrgSubTree", 1001)),
    new SelectItemExpr(Expr.Prop(nameof(OrgUnit.Id)), nameof(OrgUnit.Id)));

var treeGen = new SqlGen(typeof(OrgUnit)) { SqlBuilder = OracleBuilder.Instance };
Console.WriteLine(treeGen.ToSql(treeExpr).Sql);          // START WITH ... CONNECT BY

treeGen.SqlBuilder = SQLiteBuilder.Instance;
Console.WriteLine(treeGen.ToSql(treeExpr).Sql);          // WITH RECURSIVE（Expr 构造）
```

### 8.6 调试保存 / 重载的条件

```csharp
string json = JsonSerializer.Serialize<Expr>(filter);
Expr? loaded = JsonSerializer.Deserialize<Expr>(json);
if (loaded is null) throw new InvalidDataException("反序列化失败");

Console.WriteLine(json);
Console.WriteLine(sqlGen.ToSql(loaded).Sql);   // 应与原 filter 渲染结果一致
```

### 8.7 切换方言

```csharp
sqlGen.SqlBuilder = MySqlBuilder.Instance;     // 或 PostgreSqlBuilder / OracleBuilder / SqlServerBuilder
PreparedSql mysql = sqlGen.ToSql(BuildPageExpr(q, filter));
```

同一 Expr 换 builder 后可直接对比引用符、分页语法、参数占位符；分表实体给 `sqlGen.TableArgs` 赋参数（见[分表与 TableArgs](../advanced-topics/sharding-and-tableargs.md)）。

### 8.8 与执行期 SQL 对照

```csharp
string? lastSql = SessionManager.Current?.SqlStack.LastOrDefault();
```

- `SqlStack` 只有 `CommandText`、没有参数值；参数值看离线 `PreparedSql.Params` 或日志。
- 最多 10 条，Service 调用后清空，返回后立刻读取。
- 离线与执行不一致时按顺序排查：`TableArgs`、`SqlBuilder` 方言、根表别名、服务层 `ConstFilter`、参数值与 `DbType`。

### 8.9 调试清单

- 先 `ToSql` 看结构与参数，再执行；不要用字符串拼 SQL 反推。
- 链尾必须有 `Select`，否则只得到片段。
- 分页数值内联在 SQL 里，找不到对应参数是正常的。
- 连接查询先核对 JOIN 段数与 ON，再看 WHERE；动态排序再核对 ORDER BY 是否按白名单命中。
- 自定义 Lambda / `GenericSqlExpr` 出错时，先看生成的片段是否为空（注册未生效）。
- 树形查询先确认方言分支：Oracle 应出现 `CONNECT BY`，其他方言应出现 `WITH RECURSIVE`。
- CTE 里的自引用只写别名（如 `LEFT JOIN "OrgTree" ON ...`），不会再追加 `T3` 之类的别名。
- 反序列化条件渲染失败，先检查 `GenericSqlExpr` 的 key 是否在当前进程注册，`Arg` 是否为可序列化的原始值。

## 9. 限制与相关链接

- 派生类型必须继承带 `[Table]` 的实体；用基类连接匹配不到。
- `JoinRelated` 依赖 `[ForeignType]` / `[TableJoin]` 元数据，推断不到时抛异常，此时才退回 `Join` 写显式条件。
- 多表元组连接最多 7 张表。
- 动态排序必须走白名单；禁止把请求字段直接传给 `Expr.Prop`。
- `GenericSqlExpr` 适合结构固定的片段；条件结构随业务变化时用 Expr。
- 递归 CTE 嵌在 `IN (...)` 子查询里：SQLite / MySQL / PostgreSQL 可用；SQL Server 不允许 `WITH` 嵌在子查询中，该方言应改用物化路径列（如 `Path LIKE '1/5/%'`）或把 CTE 提到语句根部。
- `SqlGen.ToSql` 只对 `Select` / `Update` / `Delete` 输出完整语句；其余返回片段。
- 相关链接：[查询总览](../core-usage/query-overview.md)、[Lambda 指南](../core-usage/lambda-guide.md)、[关联查询](../core-usage/associations.md)、[表达式扩展](../extensibility/expression-extension.md)、[Lambda 与 Expr 组合](../core-usage/lambda-expr-mixing.md)、[ExprString 指南](../core-usage/exprstring-guide.md)、[Expr JSON 序列化格式](../extensibility/expr-serialization.md)、[前端原生 Expr 查询](../extensibility/frontend-native-expr.md)、[按需投影](./query-projection.md)、[自定义分页](../advanced-topics/custom-paging.md)、[分表与 TableArgs](../advanced-topics/sharding-and-tableargs.md)、[CTE 指南](../core-usage/cte-guide.md)、[权限过滤](../advanced-topics/permission-filtering.md)。
