# 复杂查询实例

本文以一条真实的订单列表查询为例，直接给出完整实现，并逐项解释其中的关键机制：部门树过滤、最近 7 天、动态排序分页、条件持久化，以及等价的 Lambda 解析方式。基础语法、`Expr` 组合与关联查询已由其他文档分别介绍，这里不再重复。文中的 SQL 均为实际渲染结果，除特别注明外使用 SQLite 方言，参数值以 `@0`、`@1` 表示。

## 1. 需求描述与查询输入

### 1.1 需求描述

本次查询需要满足以下要求：

- 只查询指定部门及其所有下级部门的订单，不允许先把部门 Id 列表查到内存再拼成 `IN` 列表。
- 只查询最近 7 天创建的订单，其中「最近」既可以是相对当前时间的规则，也可以是固定的时间窗口。
- 支持状态、最低金额和订单号关键字这三个可选条件，条件不存在时对应的 SQL 片段不应出现在语句中。
- 列表支持按表头点击排序，可排序列包括创建时间、金额、状态和订单号，方向可选。
- 列表支持分页，并且要用同一组条件同时给出总数与当前页的数据。
- 查询条件可以保存，用户在之后重新加载时能够继续使用。

### 1.2 模型

模型只需要两张表，部门表用于展示树形结构，订单表用于筛选和排序：

```csharp
[Table("Departments")]
public class Department
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("ParentId")] public int? ParentId { get; set; }
    [Column("Name")] public string Name { get; set; } = "";
}

[Table("Orders")]
public class SaleOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)] public int Id { get; set; }
    [Column("OrderNo")] public string OrderNo { get; set; } = "";
    [Column("DeptId")] public int DeptId { get; set; }
    [Column("Status")] public int Status { get; set; }
    [Column("Amount")] public decimal Amount { get; set; }
    [Column("CreatedTime")] public DateTime CreatedTime { get; set; }
}
```

### 1.3 查询输入：一份查询 JSON

列表接口只接收一个 JSON 字符串。下面直接给出本文最终执行的完整查询：部门树、最近 7 天、三个可选条件、排序与分页都在这同一份 JSON 里。它不需要 `$table`，也不需要 `$select`，表与查询列由 Service 的实体类型补齐。

```json
{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": {
        "$and": [
          { "$": "in", "Left": { "#": "DeptId" },
            "Right": { "$": "sql", "Key": "DeptSubTree", "Arg": { "@": 1001 } } },
          { "$": ">=", "Left": { "#": "CreatedTime" },
            "Right": { "$": "func", "AddDays": [ { "$": "func", "Now": [] }, { "@": -7 } ] } },
          { "$": "==", "Left": { "#": "Status" }, "Right": { "@": 1 } },
          { "$": ">=", "Left": { "#": "Amount" }, "Right": { "@": 100 } },
          { "$": "contains", "Left": { "#": "OrderNo" }, "Right": { "@": "SO2026" } }
        ]
      }
    },
    "OrderBys": [
      { "Field": { "#": "Amount" }, "Asc": false },
      { "Field": { "#": "Id" }, "Asc": false }
    ]
  },
  "Skip": 20,
  "Take": 20
}
```

一次反序列化得到整条查询，计数取其中的条件、当页使用整条查询（`GetFilter` 的实现见 1.4）：

```csharp
Expr query = JsonSerializer.Deserialize<Expr>(queryJson)!;
LogicExpr? filter = GetFilter(query);

int total = await orderService.CountAsync(filter, cancellationToken: ct);
List<SaleOrder> items = await orderService.SearchAsync(query, cancellationToken: ct);
```

关键点：

- `$` 开头的键表示操作符或片段类型，`#` 表示列，`@` 表示参数，完整的形状见 [Expr JSON 序列化格式](../extensibility/expr-serialization.md)。
- 片段按层嵌套：`$section` 包住 `$orderby`，`$orderby` 包住 `$where`；每层的内层片段放在 `$标记` 对应的键上，自己的属性（`Skip` / `Take`、`OrderBys`、`Where`）与标记同级。
- 可选条件不存在时，`$and` 数组里就没有对应节点，生成的 SQL 自然也不会多出条件。
- 部门树使用 `$sql` 片段（见 §3），最近 7 天使用 `Now` / `AddDays` 函数（见 2.2）；它们和其它条件一样最终参数化为 `@0`、`@1`。`DeptSubTree` 处理器必须已在当前进程注册，否则片段会被渲染为空（见 5.4）。
- 排序在 `Amount` 之后追加 `Id` 作为唯一列兜底，避免翻页出现重复行或漏行。
- 两条语句共用同一次反序列化的结果：`CountAsync` 只拿 `filter`，`SearchAsync` 拿整条 `query`，排序与分页不会进入计数 SQL。
- 生产环境应先用 `ExprValidator` 校验这份 JSON，并限制前端可引用的字段与操作符，具体做法见[安全性](../advanced-topics/security.md)。

### 1.4 从查询中提取过滤条件

分页查询需要整条查询，而计算总数只需要其中的筛选条件。查询只有条件时，条件本身就是 `filter`；查询带排序或分页时，各层片段通过 `Source` 串成 `SectionExpr`、`OrderByExpr`、`WhereExpr` 的链，只要沿着 `Source` 逐层向内走，遇到第一个 `WhereExpr` 就找到了条件：

```csharp
static LogicExpr? GetFilter(Expr? query)
{
    if (query is LogicExpr logic) return logic;

    for (Expr? node = query; node is not null; node = (node as SqlSegment)?.Source)
    {
        if (node is WhereExpr where) return where.Where;
    }
    return null;
}
```

这段代码不会复制任何节点：条件查询直接返回表达式本身，带排序分页的查询返回 `Where` 节点里的同一个对象。因此计数与当页查询天然共享同一份条件，条件只有一个来源。

### 1.5 本文边界与前置知识

下面只讨论各项能力的组合方式，单项能力本身不再展开：

| 能力 | 文档 |
| --- | --- |
| Lambda 语法、闭包变量参数化、多参数 Lambda | [Lambda 指南](../core-usage/lambda-guide.md) |
| `RegisterMethodHandler` / `RegisterMemberHandler` 机制 | [表达式扩展](../extensibility/expression-extension.md) |
| `Expr.Lambda`、`AsValue()`、Lambda 与 Expr 互转 | [Lambda 与 Expr 组合](../core-usage/lambda-expr-mixing.md) |
| Expr 的 JSON 形状、操作符标记 | [Expr JSON 序列化格式](../extensibility/expr-serialization.md) |
| 前端直接构造 Expr JSON | [前端原生 Expr 查询](../extensibility/frontend-native-expr.md) |
| 方言分页原理与 `Section` / `Skip` / `Take` | [自定义分页](../advanced-topics/custom-paging.md) |
| CTE 的 `With` / `UnionAll` | [CTE 指南](../core-usage/cte-guide.md) |
| 分组聚合与投影对比 | [按需投影](./query-projection.md) |
| 自定义函数映射与 `SqlBuilder` 扩展 | [自定义 SqlBuilder / 方言扩展](../extensibility/custom-sqlbuilder.md) |

## 2. 过滤条件：本部门、最近 7 天与可选条件

1.3 的完整查询已经可以直接执行；下面解释过滤条件本身：计数与当页的分工、最近 7 天的两种写法，以及可选条件的展开方式。

### 2.1 计数与当页的分工

`CountAsync` 只接受条件，内部会把它包装成 `WHERE` 并渲染 `SELECT Count(1)`，所以计数 SQL 不会带上排序和分页；`SearchAsync` 接受任意 `Expr`，内部用 Service 的实体类型补齐表与查询列，排序和分页会原样渲染。`$and` 里可以放任意多个条件，也可以只放一条，参数值全部由 `@` 节点携带并最终进入 `context.OutputParams`，不会被拼进 SQL 文本。`contains` 节点对应 Lambda 的 `Contains`，框架会给参数补上两侧通配符并负责转义与参数化。

### 2.2 「最近 7 天」：函数还是参数

同一句「最近 7 天」在 JSON 里有两种写法，它们生成的 SQL 形态不同，保存后的含义也不同：

```json
// 函数：每次执行都以数据库当前时间重新计算
{ "$": ">=", "Left": { "#": "CreatedTime" },
  "Right": { "$": "func", "AddDays": [ { "$": "func", "Now": [] }, { "@": -7 } ] } }

// 参数：时间点在生成 JSON 时就已固定
{ "$": ">=", "Left": { "#": "CreatedTime" },
  "Right": { "$datetime": "2026-10-18T13:53:02.2826566+08:00" } }
```

SQLite 把这两种写法分别渲染为：

```sql
-- 函数：-7 作为参数参与日期运算
"T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@0 AS TEXT)||' days')
-- @0 = -7

-- 参数：时间点固定，SQL 里只有一个普通参数
"T0"."CreatedTime" >= @0
-- @0 = 2026/10/18 13:53:02
```

MySQL 与 Oracle 则分别渲染为 ``DATE_ADD(NOW(), INTERVAL -7 DAY)`` 和 `(sysdate + NUMTODSINTERVAL(-7, 'DAY'))`，函数名与日期运算语法由各方言的 SqlBuilder 负责适配。

服务端用 Lambda 或 `Expr` 构造同样的条件时，`DateTime.Now` 会被解析成 `Now()` 函数，只有显式常量才会成为参数：

```csharp
// Lambda：DateTime.Now → Now() 函数
Expr.Lambda<SaleOrder>(o => o.CreatedTime >= DateTime.Now.AddDays(-7))!;

// 参数：先把时间算好，再作为常量
Expr.Const(DateTime.Now.AddDays(-7));
```

选择原则取决于条件要表达的是规则还是事实。实时看板适合用函数，因为每次执行都以数据库当前时间为准，条件保存后也永远表示「最近 7 天」；对账、导出和审计快照适合用参数，因为时间窗口被冻结进条件，之后重跑的结果保持不变。两种写法都能保存和重载，区别见 5.2。

## 3. 部门树过滤

1.3 的查询用部门子树替换了本部门的等值条件：`"$": "in"` 的右值不再是部门 Id，而是一个子树片段，其余条件保持不变。难点在于子树如何落到 SQL 里：Oracle 使用 `CONNECT BY`，其它方言使用递归 CTE。

下面两种方式实现同一个 `DeptSubTree` 片段，任选其一即可，3.4 给出两种方式对应的 JSON。

### 3.1 方案 A：`GenericSqlExpr` + `AsValue()`

#### 3.1.1 注册值片段

`GenericSqlExpr` 本身是 `LogicExpr`，只能放在 WHERE 位置；要给「订单的 `DeptId`」当右值用于 `IN (...)`，需要用 `AsValue()` 把它包成值表达式。

`Arg` 的类型是 `Expr`，传原始值时会自动包装为 `ValueExpr`，也可以直接传属性、函数等表达式，处理器需要值时用 `AsValue()` 归一。

注册的处理器返回带括号的子查询，这样它既能放在 `IN` 里，也能放进 SELECT 列、比较运算或函数参数：

```csharp
public static class DeptSubTree
{
    public static void Register()
    {
        GenericSqlExpr.Register("DeptSubTree", (context, arg) =>
        {
            if (arg is null) throw new NotSupportedException("DeptSubTree 需要一个部门 Id。");
            ValueTypeExpr rootId = arg.AsValue();

            ISqlBuilder builder = context.SqlBuilder;
            if (builder is OracleBuilder)
            {
                string table = builder.ToSqlName(
                    TableInfoProvider.Instance.GetTableDefinition(typeof(Department)).Name!);
                string id = builder.ToSqlName(nameof(Department.Id));
                string parentId = builder.ToSqlName(nameof(Department.ParentId));
                return $"(SELECT {id} FROM {table} START WITH {id} = {rootId.ToSql(context)} " +
                       $"CONNECT BY PRIOR {id} = {parentId})";
            }

            using (context.BeginScope())
                return $"({BuildRecursiveQuery(rootId).ToSql(context)})";
        });
    }

    // 递归 CTE 直接用 Expr 构造：锚点 + UNION ALL 自引用，最后 With("DeptTree")
    // internal 是为了让 3.2 的 FunctionExpr 版本复用同一段构造逻辑
    internal static SelectExpr BuildRecursiveQuery(ValueTypeExpr rootId)
    {
        SelectExpr anchor = Expr.From(typeof(Department))
            .Where(Expr.Prop(nameof(Department.Id)) == rootId)
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));

        var selfRef = new CommonTableExpr { Alias = "DeptTree" };

        SelectExpr recursive = Expr.From(typeof(Department))
            .Join(selfRef, Expr.Prop(nameof(Department.ParentId)) == Expr.Prop("DeptTree", nameof(Department.Id)))
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));

        return anchor.UnionAll(recursive)
            .With("DeptTree")
            .Select(new SelectItemExpr(Expr.Prop(nameof(Department.Id)), nameof(Department.Id)));
    }
}
```

#### 3.1.2 用在条件里

```csharp
LogicExpr InDeptTree(int deptId)
    => Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Sql("DeptSubTree", deptId).AsValue());
```

`InDeptTree` 生成的递归 CTE 会被收集到上下文，并在最终语句的最外层拼出 `WITH`，`IN` 里只保留对 CTE 的引用：

```sql
WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
  FROM "Departments" "T1"
  WHERE "T1"."Id" = @0
  UNION ALL SELECT "T2"."Id"
    FROM "Departments" "T2"
    LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT "T0"."Id", "T0"."OrderNo", ...
FROM "Orders" "T0"
WHERE "T0"."DeptId" IN (SELECT "Id"
  FROM "DeptTree")
-- @0 = 1001
```

SQL Server 输出同样的结构，只是不带 `RECURSIVE`（`WITH "DeptTree" AS (...)`）；Oracle 则把同一个条件渲染成层级查询：

```sql
"T0"."DEPTID" IN (SELECT "ID" FROM "DEPARTMENTS" START WITH "ID" = :0 CONNECT BY PRIOR "ID" = "PARENTID")
-- :0 = 1001
```

注意 `WITH` 一定出现在语句根部而不是 `IN` 子查询内部：CTE 定义在主体渲染完成后才统一拼到最前面，因此子查询里只出现别名。也因此，CTE 里的参数编号排在主体条件之后——例如主体已有 `@0` 时，子树根 Id 就是 `@1`；参数按名称绑定，编号先后不影响执行。

#### 3.1.3 让 Lambda 也能写

业务代码里更自然的写法是 `o.InDeptSubTree(deptId)`。定义这个方法只是为了让编译通过，它永远不会在内存中执行：

```csharp
public static class OrderExtensions
{
    public static bool InDeptSubTree(this SaleOrder order, int deptId) => false;
}
```

注册解析器时要注意，扩展方法在表达式树里是静态调用，因此 `node.Object == null`，接收者位于 `Arguments[0]`，部门 Id 位于 `Arguments[1]`：

```csharp
LambdaExprConverter.RegisterMethodHandler(
    typeof(OrderExtensions), nameof(OrderExtensions.InDeptSubTree),
    (node, converter) =>
    {
        Expr deptId = converter.Convert(node.Arguments[1]);
        return Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Sql("DeptSubTree", deptId).AsValue());
    });

// 使用
int deptId = 1001;
LogicExpr filter = Expr.Lambda<SaleOrder>(o => o.InDeptSubTree(deptId))!;
```

这段 Lambda 与 3.1.2 的 `Expr` 生成完全相同的 SQL。闭包变量会被解析成 `ValueExpr`，`Arg` 直接接收即可，保存时写成 `{"@":1001}`。

#### 3.1.4 要点

处理器只负责生成值片段，`IN`、`=`、SELECT 列等位置由调用方决定，因此同一个 key 可以在不同上下文中复用。递归 CTE 由 `UnionAll` 与 `With` 拼成，`CommonTableExpr { Alias = "DeptTree" }` 只写别名，输出时不会再追加表别名；CTE 定义本身由框架收集后统一放到语句最外层的 `WITH`。`context.BeginScope()` 用于隔离内层子查询的别名与参数作用域，但参数仍会追加到同一个参数集合。Oracle 分支通过 `TableInfoProvider` 取表名、再用 `builder.ToSqlName` 转换标识符，不要手写大写表名。最后要记住 `AsValue()` 是关键：没有它，`GenericSqlExpr` 只能当条件用，不能放在 `IN` 右边。

### 3.2 方案 B：自定义 `FunctionExpr`

`FunctionExpr` 天生是值表达式，可以直接做 `IN` 的右值，因此不需要 `AsValue()`。

#### 3.2.1 默认实现 + 方言覆盖

给 `SqlBuilder.Instance` 注册一个通用实现（递归 CTE），所有方言都能使用；再在 `OracleBuilder` 等具体类型上覆盖。`BuildFunctionSql` 按「具体类型 → 基类」的顺序查找处理器，所以覆盖只影响该方言：

```csharp
public static class DeptSubTreeFunction
{
    public static void Register()
    {
        // 默认实现：所有方言共用的递归 CTE
        SqlBuilder.Instance.RegisterFunctionSqlHandler("DeptSubTree", RenderRecursive);

        // Oracle 覆盖为 CONNECT BY
        OracleBuilder.Instance.RegisterFunctionSqlHandler("DeptSubTree", RenderOracle);
    }

    static void RenderRecursive(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context)
    {
        ValueTypeExpr rootId = expr.Args[0];
        using (context.BeginScope())
            outSql.Append($"({DeptSubTree.BuildRecursiveQuery(rootId).ToSql(context)})");
    }

    static void RenderOracle(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context)
    {
        ValueTypeExpr rootId = expr.Args[0];
        ISqlBuilder builder = context.SqlBuilder;
        string table = builder.ToSqlName(
            TableInfoProvider.Instance.GetTableDefinition(typeof(Department)).Name!);
        string id = builder.ToSqlName(nameof(Department.Id));
        string parentId = builder.ToSqlName(nameof(Department.ParentId));
        outSql.Append($"(SELECT {id} FROM {table} START WITH {id} = {rootId.ToSql(context)} " +
                       $"CONNECT BY PRIOR {id} = {parentId})");
    }
}
```

先给 `SqlBuilder.Instance` 注册默认实现再按需覆盖，就不必为每个方言重复编写一段相同的递归 CTE，方言差异只保留在真正不同的地方。

#### 3.2.2 用在条件里

```csharp
LogicExpr InDeptTree(int deptId)
    => Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Func("DeptSubTree", Expr.Value(deptId)));
```

SQLite 与 Oracle 的输出与 3.1 逐字相同。Lambda 版本只需要把处理器最后返回的 `Expr` 换掉：

```csharp
// 方案 B 下，3.1.3 的处理器改成返回 FunctionExpr
LambdaExprConverter.RegisterMethodHandler(
    typeof(OrderExtensions), nameof(OrderExtensions.InDeptSubTree),
    (node, converter) =>
    {
        Expr deptId = converter.Convert(node.Arguments[1]);
        return Expr.Prop(nameof(SaleOrder.DeptId)).In(Expr.Func("DeptSubTree", deptId.AsValue()));
    });
```

### 3.3 两种方式怎么选

| 关注点 | `GenericSqlExpr` + `AsValue()` | 自定义 `FunctionExpr` |
| --- | --- | --- |
| 注册位置 | 全局 key 注册表，一处注册 | `SqlBuilder.Instance` 注册默认实现，方言按需覆盖 |
| 方言分支 | 处理器内部判断 `context.SqlBuilder` | 默认实现 + 具体类型覆盖 |
| 表达式类型 | `LogicExpr`，值位置要 `AsValue()` | 原生 `ValueTypeExpr` |
| 复用 | 任何 Expr 都能引用这个 key | 与函数体系一致，可被函数校验器识别 |
| 适合 | 片段跨方言差异不大、想集中管理 | 片段方言差异大、或已有按方言定制 SqlBuilder |

### 3.4 换成部门树的 JSON

方案 A 把右值换成一个带 `Arg` 的 `$sql` 节点，其中 `Arg` 按 `Expr` 的规则写成参数值：

```json
{ "$": "in", "Left": { "#": "DeptId" },
  "Right": { "$": "sql", "Key": "DeptSubTree", "Arg": { "@": 1001 } } }
```

方案 B 把右值换成一个函数调用节点，参数数组里同样只有一个部门 Id：

```json
{ "$": "in", "Left": { "#": "DeptId" },
  "Right": { "$": "func", "DeptSubTree": [ { "@": 1001 } ] } }
```

只替换这一个条件节点而保持其余节点不动，得到的就是「部门树 + 最近 7 天 + 可选条件」这份新的查询 JSON。

## 4. 动态排序与分页

### 4.1 排序与分页的 JSON 形状

排序和分页不是额外的请求参数，而是查询 JSON 的两个片段层。查询不需要 `$select` 和 `$table`，最外层直接是分页片段 `$section`，它的源是排序片段 `$orderby`，排序片段的源才是条件片段 `$where`；由于没有指定表，`$where` 的值写成 `null`，交给 `Search` 用实体类型补上：

```json
{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": { "$and": [ ... ] }
    },
    "OrderBys": [
      { "Field": { "#": "Amount" }, "Asc": false },
      { "Field": { "#": "Id" }, "Asc": false }
    ]
  },
  "Skip": 20,
  "Take": 20
}
```

每个片段都把内层片段放在 `$` 标记对应的键上，自己的属性与这个键同级：`Skip` 和 `Take` 与 `$section` 同级，`OrderBys` 与 `$orderby` 同级，`Where` 与 `$where` 同级。`Asc` 省略时按升序处理，写 `false` 表示降序；`Skip` 为 0 时可以省略，`Take` 为 0 表示不限制数量。

当 `OrderBys` 为 `Amount` 降序并取第 2 页（每页 20 条）时，渲染出的排序与分页部分如下：

```sql
ORDER BY "T0"."Amount" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
```

当 `OrderBys` 只有 `CreatedTime` 升序时，渲染结果为：

```sql
ORDER BY "T0"."CreatedTime", "T0"."Id" DESC
```

MySQL 的分页写作 ``LIMIT 20,20``，Oracle 写作 `OFFSET 20 ROWS FETCH NEXT 20 ROWS ONLY`，这些差异由方言的 SqlBuilder 处理，JSON 本身不需要区分方言。

排序字段直接写在 JSON 里，查询方法不需要再做一次白名单映射。如果接口对外暴露，可以统一用 `ExprValidator` 限制可引用的字段范围（见[安全性](../advanced-topics/security.md)），这样排序字段与筛选字段就在同一处校验，不必额外维护一份排序列白名单。

### 4.2 用同一份 filter 取总数与当页

`query` 由一次反序列化得到，`filter` 是从它里面提取出来的条件（执行代码见 1.3），两者分别用于计数与当页。计数 SQL 没有 `ORDER BY` 和 `LIMIT`，因为它只拿到 `filter`：

```sql
WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
  FROM "Departments" "T1"
  WHERE "T1"."Id" = @1
  UNION ALL SELECT "T2"."Id"
    FROM "Departments" "T2"
    LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT Count(1)
FROM "Orders" "T0"
WHERE "T0"."DeptId" IN (SELECT "Id"
  FROM "DeptTree") AND "T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@0 AS TEXT)||' days') AND "T0"."Status" = @1 AND "T0"."Amount" >= @2 AND "T0"."OrderNo" LIKE @3
-- @0 = -7, @1 = 1, @2 = 100, @3 = %SO2026%, @4 = 1001
```

注意参数顺序：子树根 Id 在 CTE 定义渲染时才产生，CTE 又是主体渲染完成后才拼到最前面，所以它拿到的是最后一个编号，主体条件依次占用前面的编号。参数按名称绑定，编号先后不影响执行。

当页 SQL 拿到整条 `query`，条件与计数完全一致，只额外追加排序和分页；它同样在最外层输出一份独立的 `WITH`（两条语句各自生成，不共享上下文）：

```sql
WITH RECURSIVE "DeptTree" AS (SELECT "T1"."Id"
  FROM "Departments" "T1"
  WHERE "T1"."Id" = @4
  UNION ALL SELECT "T2"."Id"
    FROM "Departments" "T2"
    LEFT JOIN "DeptTree" ON "T2"."ParentId" = "DeptTree"."Id")
SELECT "T0"."Id", "T0"."OrderNo", "T0"."DeptId", "T0"."Status", "T0"."Amount", "T0"."CreatedTime"
FROM "Orders" "T0"
WHERE "T0"."DeptId" IN (SELECT "Id"
  FROM "DeptTree") AND "T0"."CreatedTime" >= DATE(datetime('now', 'localtime'), CAST(@0 AS TEXT)||' days') AND "T0"."Status" = @1 AND "T0"."Amount" >= @2 AND "T0"."OrderNo" LIKE @3
ORDER BY "T0"."Amount" DESC, "T0"."Id" DESC
LIMIT 20 OFFSET 20
-- @0 = -7, @1 = 1, @2 = 100, @3 = %SO2026%, @4 = 1001
```

### 4.3 要点

- `filter` 与 `query` 来自同一次反序列化，计数取其中的 `Where`，当页使用整条 `query`，所以两者天然一致，修改条件只需要改 JSON 的一处。
- 排序必须额外指定一个唯一列兜底（这里是 `Id`），否则翻页时可能出现重复行或漏行。
- 表由 Service 的实体类型决定，JSON 只描述条件、排序和分页，因此计数与当页一定落在同一张表上。

## 5. 条件持久化

### 5.1 保存条件而不是整份查询

列表接口收到的 JSON 同时包含条件、排序和分页，但需要长期保存的只有条件：排序和分页属于「本次浏览」，应该跟随用户当前的界面状态，而不是被固化进历史记录。因此保存时从查询中提取 `Where` 并序列化，重载时再把它放回查询 JSON 的 `$where` 节点；如果查询本身就是条件，则直接使用这条条件：

```csharp
static Expr SetFilter(Expr query, LogicExpr? filter)
{
    if (query is LogicExpr) return filter!;

    for (SqlSegment? node = query as SqlSegment; node is not null; node = node.Source)
    {
        if (node is WhereExpr where) { where.Where = filter; return query; }
    }
    throw new ArgumentException("查询 JSON 中没有 $where 节点。");
}

// 保存：只存条件
store.Save(userId, JsonSerializer.Serialize<Expr>(GetFilter(query)));

// 重载：把条件放回当前查询
query = SetFilter(query, (LogicExpr?)JsonSerializer.Deserialize<Expr>(store.Load(userId)));
```

`GenericSqlExpr.Arg` 的类型是 `Expr`，因此按 `Expr` 的规则序列化：`ValueExpr` 写成 `{"@": 值}`，`Expr.Const(...)` 写成原生值。3.4 里的 `"Arg": { "@": 1001 }` 因此可以直接往返，不需要在保存和重载时还原原始值。条件按原样保存再重新装载后，渲染出的 SQL 与保存前逐字相同。

### 5.2 重载后「最近 7 天」的两种语义

2.2 中的两种写法在重载后的含义完全不同。函数形式（`$func` 里的 `Now` 与 `AddDays`）保存的是「最近 7 天」这个规则，今天打开是 10-18 到 10-25，下周打开就是 10-25 到 11-01，因此适合实时看板与常驻筛选器。参数形式（`$datetime`）保存的是「10-18 以来」这个事实，过多久重跑结果都一样，因此适合对账、导出与审计留痕。判断标准也很直接：如果用户在界面上明确选了日期区间，就应该用参数；如果用户选的是「最近 N 天」这种相对选项，就应该用函数。

### 5.3 重载后继续叠加条件

重载得到的条件可以继续与新的条件合并，再把合并结果放回查询中，排序和分页仍然保留在 JSON 里：

```csharp
LogicExpr saved = (LogicExpr?)JsonSerializer.Deserialize<Expr>(store.Load(userId))!;
query = SetFilter(query, saved & (Expr.Prop(nameof(SaleOrder.Amount)) >= 500));
```

### 5.4 处理器必须在当前进程注册

`DeptSubTree` 的处理器必须在当前进程注册过，例如在启动时调用 `DeptSubTree.Register()` 或 `DeptSubTreeFunction.Register()`。否则重载得到的条件在生成 SQL 时找不到对应的 key，原本应该出现的子查询就会被渲染成空片段。

## 6. 完整查询方法

把上述能力组合成一个可直接使用的查询方法。入口有两种：反序列化前端提交的查询 JSON，或用 Lambda 构造 `IQueryable<T>` 后传入解析。

### 6.1 JSON 入口：总数与当页

```csharp
public sealed record OrderPage(int Total, List<SaleOrder> Items);

public sealed class OrderListService
{
    private readonly EntityService<SaleOrder> _orders;

    public OrderListService(EntityService<SaleOrder> orders) => _orders = orders;

    public async Task<OrderPage> QueryAsync(string queryJson, CancellationToken ct = default)
    {
        // 一次反序列化得到整条查询
        Expr query = JsonSerializer.Deserialize<Expr>(queryJson)!;

        // 计数只使用 Where，当页使用整条查询
        LogicExpr? filter = GetFilter(query);
        int total = await _orders.CountAsync(filter, cancellationToken: ct);
        List<SaleOrder> items = await _orders.SearchAsync(query, cancellationToken: ct);

        return new OrderPage(total, items);
    }
}
```

`GetFilter`（1.4）与 `SetFilter`（5.1）放在同一个类里，应用启动时调用一次 `DeptSubTree.Register()` 或 `DeptSubTreeFunction.Register()`。保存与重载条件按第 5 节的方式处理即可，`QueryAsync` 的其余部分不需要修改。

### 6.2 Lambda 入口：构造 Query 对象后传入解析

条件来自服务端时，可以完全跳过 JSON，用 Lambda 直接构造 `IQueryable<SaleOrder>`，再把这个构造好的 Query 对象传给 `SearchAsync` / `Search` 的 `IQueryable<T>` 重载，由框架解析成 `Expr` 并执行。下面两个方法可以并入 6.1 的 `OrderListService`：

```csharp
public sealed record OrderCriteria(
    int DeptId,
    int? Status,
    decimal? MinAmount,
    string? Keyword,
    int Skip,
    int Take);

public IQueryable<SaleOrder> BuildQuery(OrderCriteria criteria)
{
    IQueryable<SaleOrder> query = Expr.From<SaleOrder>()
        .Where(o => o.InDeptSubTree(criteria.DeptId))
        .Where(o => o.CreatedTime >= DateTime.Now.AddDays(-7));

    // 可选条件按需追加；重复 Where 会在解析时合并为同一个条件（AND）
    if (criteria.Status.HasValue)
        query = query.Where(o => o.Status == criteria.Status.Value);
    if (criteria.MinAmount.HasValue)
        query = query.Where(o => o.Amount >= criteria.MinAmount.Value);
    if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        query = query.Where(o => o.OrderNo.Contains(criteria.Keyword));

    return query
        .OrderByDescending(o => o.Amount)
        .ThenByDescending(o => o.Id)
        .Skip(criteria.Skip)
        .Take(criteria.Take);
}

public async Task<OrderPage> QueryByLambdaAsync(OrderCriteria criteria, CancellationToken ct = default)
{
    IQueryable<SaleOrder> query = BuildQuery(criteria);

    // 把构造好的 Query 对象解析成 Expr；仅拼表达式，不需要执行器
    Expr queryExpr = query.GetQueryExpr();
    LogicExpr? filter = GetFilter(queryExpr);

    int total = await _orders.CountAsync(filter, cancellationToken: ct);
    List<SaleOrder> items = await _orders.SearchAsync(query, cancellationToken: ct);

    return new OrderPage(total, items);
}
```

`Expr.From<SaleOrder>()` 返回 `QuerySource<SaleOrder>`，它实现 `IQueryable<T>`，因此 `Where` / `OrderByDescending` / `Skip` / `Take` 都是标准 LINQ 算子。传入 `SearchAsync(IQueryable<T>)` / `Search(IQueryable<T>)` 时，框架用同一个 `LambdaExprConverter` 把表达式树解析成 `Expr`，后续 SQL 生成与参数绑定和 JSON 入口完全一致。

要点：

- **构造阶段不执行查询。** `Expr.From<T>()` 返回的查询源没有绑定执行器，只用于拼表达式；直接枚举会抛 `InvalidOperationException`。要取数就把它传给 `SearchAsync` / `Search`，或从容器取已绑定执行器的 `QuerySource<T>`，再用 `.AsSource()` 调用异步方法。
- **计数与当页仍然同源。** 可选条件通过追加 `Where` 实现，重复 `Where` 在解析时原地合并成一个条件，因此 `GetFilter(queryExpr)` 拿到的是完整条件，计数不会漏掉其中任何一层。
- **「最近 7 天」的语义由写法决定。** `DateTime.Now.AddDays(-7)` 在解析阶段变成 `Now()` 与 `AddDays` 函数，等价于 2.2 的函数形式；如果要冻结时间窗口，先在 Lambda 外把时间算好，再用闭包变量捕获，它会成为参数。
- **部门树依赖 Lambda 处理器。** `InDeptSubTree` 需要按 3.1.3 注册方法处理器；未注册时解析器无法识别这个调用。
- **动态排序仍要收口。** 排序列来自前端时，把它映射成固定的 `switch` 分支（每个分支写一条 Lambda 排序），不要让用户字符串直接进入查询；JSON 入口则继续交给 `$orderby` 与 `ExprValidator` 校验。
- **与 JSON 入口的分工。** Lambda 入口的字段名由编译期成员访问确定，不需要字段白名单，适合服务端固定条件；JSON 入口适合前端动态组装条件。两条路径最终生成同一棵 `Expr` 树，可以按调用方混用，也可以按 §5 的方式把 Lambda 产生的 `filter` 序列化保存。

## 7. 和其他 ORM 的对照

| 场景 | LiteOrm 的做法 | 常见 ORM 的额外工作 |
| --- | --- | --- |
| 查询条件传入 | 前端 Expr JSON 直接反序列化 | 为每类查询定义 DTO，再映射成查询 |
| 排序与分页传入 | 同一份 JSON 的 `$orderby` / `$section` 层 | 分别接收排序和分页参数，再拼进查询 |
| 部门树过滤 | 一个 Expr 片段生成方言 `CONNECT BY` 或递归 CTE | 先递归查 Id 列表，再拼 `IN`；或写原生 SQL |
| 同一条件 count + 当页 | 从同一个 `Expr` 提取 `Where` 用于计数 | 手写两套 SQL 或两次表达式树 |
| 表头动态排序 | `OrderBys` 直接写在查询 JSON 里 | 大量 `switch` 或字符串拼接 |
| 条件保存 | 只保存提取出的条件 JSON | 另建 DTO 转换层 |
| Lambda / Expr / ExprString | 三种写法可互相组合、共用参数 | 通常只能选一种 |

## 8. 用 SqlGen 调试查询

`SqlGen` 不连接数据库，只按指定方言把 `Expr` 渲染成 SQL，因此适合写文档和排查方言差异。

### 8.1 最小用法

```csharp
var sqlGen = new SqlGen(typeof(SaleOrder)) { SqlBuilder = SQLiteBuilder.Instance };
PreparedSql sql = sqlGen.ToSql(query);
Console.WriteLine(sql.Sql);
foreach (Param p in sql.Params) Console.WriteLine($"{p.Name} = {p.Value}");
```

`SqlGen` 默认使用通用 `SqlBuilder.Instance`（`OFFSET ... FETCH`），要验证 SQLite、MySQL 或 Oracle 的行为必须显式设置 `SqlBuilder`。

### 8.2 换方言验证树形片段

```csharp
LogicExpr treeFilter = InDeptTree(1001);

var sqlite = new SqlGen(typeof(SaleOrder)) { SqlBuilder = SQLiteBuilder.Instance };
var oracle = new SqlGen(typeof(SaleOrder)) { SqlBuilder = OracleBuilder.Instance };

Console.WriteLine(sqlite.ToSql(treeFilter).Sql);   // WITH RECURSIVE
Console.WriteLine(oracle.ToSql(treeFilter).Sql);   // START WITH ... CONNECT BY
```

换成 `Expr.Func("DeptSubTree", ...)` 的实现后，输出应当逐字相同；如果出现差异，说明两种实现的注册方式或参数处理不一致。

### 8.3 验证 count、当页与重载

```csharp
// 与 CountAsync 内部构造的计数查询等价
SelectExpr countExpr = new SelectExpr(
    GetFilter(query)!.ToSource<SaleOrder>(),
    new SelectItemExpr(Expr.Aggregate("Count", Expr.Const(1))));

Console.WriteLine(sqlGen.ToSql(countExpr).Sql);   // SELECT Count(1)，无 ORDER BY/LIMIT
Console.WriteLine(sqlGen.ToSql(query).Sql);       // ORDER BY + LIMIT/OFFSET

string saved = JsonSerializer.Serialize<Expr>(GetFilter(query));
LogicExpr reloaded = (LogicExpr?)JsonSerializer.Deserialize<Expr>(saved)!;
Console.WriteLine(sqlGen.ToSql(GetFilter(query)!).Sql == sqlGen.ToSql(reloaded).Sql);   // True
```

### 8.4 对照执行期 SQL 与调试清单

执行期的 SQL 可以从 `SessionManager.Current?.SqlStack` 取得，它最多保留最后 10 条。使用 DI 时，`ServiceInvokeInterceptor` 会在每次 Service 方法调用结束时清空它；只用核心库（不经过 Service 拦截器）时不会自动清空，需要手动调用 `SessionManager.Reset()`。`SqlGen` 只验证生成逻辑，不验证参数类型，也不验证数据库函数是否存在，因此上线前仍要用真实方言跑一次。

遇到问题时可以按下面的顺序排查：

- 树形 SQL 为空：`DeptSubTree` 的 key 没有注册，或者 `GenericSqlExpr` 没有走 `AsValue()`。
- SQLite 出现 `CONNECT BY`：`RegisterFunctionSqlHandler` 没有注册到 `OracleBuilder` 这类具体类型上。
- 重载后 SQL 变了：保存的是函数还是参数，先看 JSON 里是 `"Now"` 还是 `"$datetime"`。
- 排序或分页没有生效：确认 JSON 中存在 `$orderby` / `$section` 层，并且它们位于正确的层级。
- 重载失败：检查 `Arg` 是否按 Expr JSON 书写，数值参数应写成 `{"@": 1001}`。
- 翻页重复：排序缺少唯一列兜底。

## 9. 限制与延伸阅读

- 递归 CTE 由框架提升到语句最外层（`WITH` 不会出现在 `IN (...)` 子查询里），SQLite、MySQL 8、PostgreSQL 与 SQL Server 都可直接执行，Oracle 走 `CONNECT BY`；MySQL 5.7 不支持递归 CTE。DAO 内部手工拼接的 UPDATE / DELETE / SELECT 语句会自动把 CTE 的 `WITH` 补在语句最前面，只有自己在框架外拼接 SQL 字符串时才需要手动移动。
- `GenericSqlExpr` 的 `Arg` 是单个 `Expr`，需要多个参数或更复杂的条件结构时应改用 `FunctionExpr` 或显式构造 `Expr`。
- 接收前端 JSON 时，必须用 `ExprValidator` 限制表达式类型与字段范围，不能把任意 JSON 直接送进 SQL 生成；排序字段与筛选字段可以在这同一处限制。
- 计数只使用提取出的 `Where`，表由 Service 的实体类型决定，因此查询 JSON 不需要也不应该再指定表。
- 树形处理器只解决「向下找部门」这一种语义；向上找父链、按层聚合等需求应另写片段，不要把多个语义塞进同一个 key。
- 相关链接：[查询总览](../core-usage/query-overview.md)、[Lambda 指南](../core-usage/lambda-guide.md)、[关联查询](../core-usage/associations.md)、[表达式扩展](../extensibility/expression-extension.md)、[Lambda 与 Expr 组合](../core-usage/lambda-expr-mixing.md)、[Expr JSON 序列化格式](../extensibility/expr-serialization.md)、[前端原生 Expr 查询](../extensibility/frontend-native-expr.md)、[按需投影](./query-projection.md)、[自定义分页](../advanced-topics/custom-paging.md)、[CTE 指南](../core-usage/cte-guide.md)、[分表与 TableArgs](../advanced-topics/sharding-and-tableargs.md)、[自定义 SqlBuilder / 方言扩展](../extensibility/custom-sqlbuilder.md)。
