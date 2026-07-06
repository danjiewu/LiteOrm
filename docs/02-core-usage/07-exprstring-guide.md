# ExprString 使用指南

`ExprString` 是 LiteOrm 提供的 C# 插值字符串处理器（`[InterpolatedStringHandler]`），专门用于在 DAO 层手写 SQL 片段或完整 SQL。它可以在一条插值字符串里同时混排「`Expr` 对象」和「普通值」，由框架统一完成 SQL 拼接与参数化。

本文是 `ExprString` 的独立说明文档。如果你关心的是 Lambda / `Expr` / `ExprString` 的整体选型，请先阅读[查询总览](./04-query-overview.md)；Lambda 写法见 [Lambda 查询指南](./05-lambda-guide.md)，构造 `Expr` 对象本身见 [Expr 使用指南](./06-expr-guide.md)。

## 1. 定位与适用范围

| 维度 | 说明 |
|------|------|
| 所在类型 | `LiteOrm.Common.ExprString`（`ref struct`，编译期插值字符串处理器）|
| 入口层 | 仅 **DAO 层** 暴露 `ExprString` 重载，Service 层不提供 |
| 典型场景 | 在自定义 DAO 里补 `Search` 的 `WHERE/ORDER BY` 片段、传完整 SQL、做 `DataTable` 查询、投影到任意类型 |
| 类型安全 | 运行时，无编译期列名/类型校验 |
| SQL 注入防护 | 普通值自动转为命名参数；`Expr` 对象走 `Expr` 自身的参数化通道 |

`ExprString` 不会替代结构化的 `Expr` / `SelectExpr`，而是作为「已经确定要手写一段 SQL」时的低门槛入口——既不用脱离 LiteOrm 的参数化机制，又能直接复用已构造好的 `Expr` 条件。

## 2. 基本工作原理

`ExprString` 是一个 `ref struct`，构造时会从当前 DAO（`IExprStringBuildContext`）取得 `SqlBuilder` 和 `SqlBuildContext`，并在内部维护一个 SQL 文本缓冲区与一个参数列表。编译器把插值字符串拆成两类调用：

- `AppendLiteral(string)`：处理字面量片段，原样追加到缓冲区。
- `AppendFormatted<T>(T value)`：处理每个插值项。
  - 当 `value` 是 `Expr` 时，调用 `expr.ToSql(...)` 把表达式翻译成 SQL 片段并拼入，附带其内部产生的参数；
  - 当 `value` 是 `RawSql` 时（见 [第 8 节 插入原始 SQL](#8-插入原始-sql-rawsql)），其内容直接原样拼入缓冲区，不进行参数化或语法处理；
  - 否则把该值包装成一个命名参数（`@0`、`@1`……，按出现顺序命名，并通过 `ISqlBuilder.ToSqlParam` 转成当前方言的参数前缀），值进入参数列表。

最终通过 `GetSql()` / `GetParams()` / `GetResult()` 取出 SQL 文本与参数，组装成 `PreparedSql` 交给 DAO 执行。

> 因此一条插值字符串里可以同时出现：`{Expr 对象}`、`{普通变量}`、`{字面 SQL 文本}`，三者按出现顺序被消费。

## 3. 基本用法

### 3.1 仅作为 Search 的条件片段

最常见用法：把 `WHERE ... ORDER BY ...` 片段传给 `Search`，DAO 会自动补上 `SELECT {AllFields} FROM {From}`。

```csharp
using static LiteOrm.Common.Expr;

public async Task<List<UserView>> GetActiveAdultsAsync(CancellationToken ct = default)
{
    var condition = Prop("Age") >= 18;
    return await userViewDAO
        .Search($"WHERE {condition} ORDER BY CreateTime DESC")
        .ToListAsync(ct);
}
```

### 3.2 混合 Expr 与普通值

`Expr` 直接展开成 SQL 片段；普通值（`int`、`string` 等）自动参数化。

```csharp
using static LiteOrm.Common.Expr;

int minAge = 18;
string keyword = "admin";

var users = await userViewDAO.Search(
    $"WHERE {Prop("UserName").Contains(keyword)} AND {Prop("Age")} >= {minAge}"
).ToListAsync();
```

建议优先把条件构造成 `Expr`（例如上面的 `Prop("UserName").Contains(keyword)`），而不是把列名/值都写死在字符串里——这样列名仍走 `Expr` 的引用符规则，值仍走参数化。

### 3.3 完整 SQL（isFull: true）

需要写完整 `SELECT ... FROM ...` 时，传 `isFull: true`，DAO 不会再自动拼接 `SELECT {AllFields} FROM {From}`。

```csharp
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

### 3.4 多行插值字符串

复杂 SQL 推荐用 C# 的原始字符串插值（`"""..."""`）保持可读性：

```csharp
var result = await dataViewDAO.Search(
    $"""
    SELECT [Id], [UserName], [Age]
    FROM [Users]
    WHERE [Age] >= {minAge}
    ORDER BY [Age] DESC, [UserName] ASC
    """,
    isFull: true
).GetResultAsync();
```

## 4. 参数化与安全性

- **普通值**：插入 `int`、`string`、`DateTime` 等非 `Expr` 对象时，`ExprString` 自动生成命名参数（默认 `@0`、`@1`……，Oracle 等方言会被 `ISqlBuilder.ToSqlParam` 转成 `:0` 等），值放入参数列表，杜绝 SQL 注入。
- **`Expr` 对象**：`Expr` 自身的 `ToSql` 会决定是内联还是参数化。例如 `Prop("Age") >= 18` 中 `18` 由 `Expr` 处理（按 `Expr.Value` 语义参数化），而 `Prop("Age")` 自身则展开为引用符包裹的列名。
- **手写标识符**：见下一节「引用符占位」。

> 不要用字符串拼接把用户输入直接拼进 SQL，始终通过 `{普通变量}` 或 `{Expr.Value(...)}` 让框架参数化。

## 5. 引用符占位 `[ ]`

不同数据库的标识符引用符不同（SQL Server 用 `[ ]`、MySQL 用 `` ` ` ``、PostgreSQL/Oracle 用 `" "`）。`ExprString` 中手写标识符时，统一用 `[` 和 `]` 作为通用占位符，DAO 在执行前会通过 `ISqlBuilder.ReplaceSqlName` 按当前方言替换成真实引用符。

```csharp
// 在 SQL Server 上变成 [Users]，在 MySQL 上变成 `Users`，在 PostgreSQL 上变成 "Users"
var result = await dataViewDAO.Search(
    $"SELECT [Id], [UserName] FROM [Users] WHERE [Age] >= {minAge}",
    isFull: true
).GetResultAsync();
```

如果用 `Expr.Prop(...)` 构造列引用，则无需关心引用符——`Expr` 会通过 `SqlBuilder.ToSqlName` 自动处理。

## 6. 排序与分页

`ExprString` 中的排序直接写在 `ORDER BY` 子句中。

**直接写列名：**

```csharp
var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} ORDER BY DeptId ASC, CreateTime DESC"
).ToListAsync();
```

**嵌入 `OrderByExpr`：**

```csharp
using static LiteOrm.Common.Expr;

var orderBy = new OrderByExpr(
    null,
    Prop("DeptId").Asc(),
    Prop("CreateTime").Desc()
);

var users = await userViewDAO.Search(
    $"WHERE {Prop("Age")} >= {minAge} {orderBy}"
).ToListAsync();
```

**分页**通常交给 DAO 的分页方法（如 `Search().ToPageListAsync(...)`），或在完整 SQL 里写方言相关的 `LIMIT/OFFSET`/`ROW_NUMBER`。

## 7. 占位标记 `{Table}` / `{From}` / `{AllFields}`

`DAOBase` 预定义了三个 SQL 标记，子类在 `GetReplacements()` 中提供替换值，执行命令前由 `MultiReplacer` 统一替换：

| 标记 | 含义 | 默认替换值 |
|------|------|------------|
| `{Table}` | 实际表名（含分表参数格式化后的结果） | `FactTableName` |
| `{From}` | 查询时使用的多表 JOIN 段（含表别名、JOIN） | `Table.ToSql(...)` |
| `{AllFields}` | 当前视图/实体查询需要的全部列 SQL | 按 `SelectColumns` 拼接 |

这些标记在完整 SQL（`isFull: true`）里也生效，方便你写自定义投影时复用主表定义：

```csharp
var result = await dataViewDAO.Search(
    $"SELECT {AllFields} FROM {From} WHERE {Prop("Age")} >= {minAge}",
    isFull: true
).GetResultAsync();
```

> 自定义 DAO 可重写 `GetReplacements()` 添加更多标记，但不要破坏上述三个默认项。

## 8. 插入原始 SQL（RawSql）

### 8.1 什么时候用

`ExprString` 默认会保证安全：普通值被参数化、`Expr` 走结构化转换、手写标识符可统一用 `[ ]` 占位。但有时你确实需要把一段**数据库特定语法**原样拼进去，比如：

- 方言特有的查询提示（如 SQL Server 的 `WITH (NOLOCK)`、MySQL 的 `FORCE INDEX(...)`）
- 尚未在 `SqlBuilder` 中注册的函数调用
- 复杂的 `CASE WHEN` 片段或原生 SQL 表达式

这些文本无法用 `Expr` 表达，又不能走「普通值」路径（会被参数化成 `@N`）。此时使用 `RawSql` 标记类型显式声明「这一段是受信任的原始 SQL 文本」。

### 8.2 类型定义

`RawSql` 是一个独立的 `readonly struct`，**不**继承 `Expr`，仅作为 `ExprString` 的辅助入口存在：

```csharp
namespace LiteOrm.Common;

public readonly struct RawSql
{
    public string Sql { get; }
    public RawSql(string sql);
    public static RawSql From(string sql);
    public override string ToString();
}
```

`ExprString` 对 `RawSql` 提供专门的 `AppendFormatted(RawSql value)` 重载：直接把 `Sql` 原样追加到 SQL 缓冲区，**不**生成参数、**不**做语法处理、**不**替换 `[ ]` 引用符占位。

### 8.3 使用示例

```csharp
using LiteOrm.Common;

// 1. 用构造函数
var result = await dataViewDAO.Search(
    $"SELECT {new RawSql("TOP 10 *")} FROM {From} WHERE {new RawSql("Status = 1")} AND {Expr.Prop("Age")} >= {minAge}",
    isFull: true
).GetResultAsync();

// 2. 用工厂方法
var result2 = await dataViewDAO.Search(
    $"SELECT {RawSql.From("COUNT(*)")} FROM {From} WHERE {Expr.Prop("Name")} LIKE {"%test%"}",
    isFull: true
).GetResultAsync();
```

上面三处插值分别走三条路径：
- `new RawSql("TOP 10 *")` → 原样拼入 `TOP 10 *`
- `Expr.Prop("Age")` → 走 `Expr.ToSql`，列名按方言包裹引用符
- `{minAge}` → 自动参数化为 `@0`

### 8.4 安全约束

`RawSql` 绕过 LiteOrm 的参数化机制，**调用方必须自行保证** `Sql` 文本中不包含任何用户可控的输入，否则可能引发 SQL 注入。请遵守以下规则：

| 规则 | 说明 |
|------|------|
| 仅用于静态文本 | `RawSql` 内容必须是写死在代码里的常量片段，不接受运行时拼接用户输入 |
| 不参与 ExprValidator 验证 | `RawSql` 不是 `Expr`，不会被 `ExprValidator.CreateQueryOnly()` 等验证器扫描——它只在受信任的服务端 DAO 代码中使用 |
| 不支持 JSON 序列化往返 | `RawSql` 不是 `Expr`，无法通过 `ExprJsonConverter` 序列化/反序列化，前端 Expr JSON 中不能携带原始 SQL |
| 优先用 Expr | 凡是能用 `Expr.Prop`/`Expr.Func`/`Expr.Sql`（预注册的 `GenericSqlExpr`）表达的，不要用 `RawSql` |

> 如果需要在自定义 SQL 中安全地传递运行时值，请用 `GenericSqlExpr.Register` 注册回调，在回调内部使用 `outputParams` 参数化，详见[安全性](../03-advanced-topics/08-security.md)。

### 8.5 与 GenericSqlExpr 的区别

| 维度 | `RawSql` | `GenericSqlExpr` |
|------|----------|------------------|
| 是否为 `Expr` | 否（独立 struct） | 是（继承 `LogicExpr`） |
| 注册要求 | 无，直接构造 | 必须先 `Register` 注册回调 |
| 参数化支持 | 不支持，纯静态文本 | 支持，回调内可用 `outputParams` |
| 适用场景 | 一次性、静态、方言特定的 SQL 片段 | 可复用、需要运行时参数的动态 SQL 片段 |
| 验证器管控 | 不被扫描 | `ExprValidator.CreateQueryOnly()` 默认放行 |

简言之：**静态写死的小片段用 `RawSql`，需要参数化的可复用片段用 `GenericSqlExpr`**。

## 9. 可用入口一览

| DAO | 方法 | 说明 |
|----|------|------|
| `ObjectViewDAO<T>` / `IObjectViewDAO` | `Search(ref ExprString sqlBody, bool isFull = false)` | 默认补 `SELECT {AllFields} FROM {From}`，返回实体/视图列表 |
| `ObjectViewDAO<T>` / `IObjectViewDAO` | `SearchAs<TResult>(ref ExprString sqlBody)` | 完整 SQL 投影到任意 `TResult` |
| `DataViewDAO<T>` | `Search(ref ExprString sqlBody, bool isFull = false)` | 返回 `DataTable` |
| `DAOBase` | `GetValue<T>(ref ExprString sqlBody)` | 返回标量值 |
| `DAOBase` | `Execute(ref ExprString sqlBody)` | 执行非查询，返回受影响行数 |
| `DAOBase` | `Query<TResult>(ref ExprString sqlBody, ...)` | 自定义 Reader 的查询 |

这些方法都使用 `[InterpolatedStringHandlerArgument("")]` 把当前 DAO 作为上下文传给 `ExprString`，所以调用时直接传插值字符串即可，不需要手动 `new ExprString(...)`。

## 10. 边界与注意事项

- **顺序敏感**：`ExprString` 按插值项出现顺序消费 `Expr`，参数编号也是按顺序递增。这跟完整 `SelectExpr` 的「按结构遍历」不同，复杂查询中表别名可能无法按作用域自动匹配。
- **主表别名**：`ExprString` 创建时已在上下文注册主表及别名 `T0`。直接在插值项里再插入主表 `FromExpr` 可能造成主表别名重复分配错误——可给该 `FromExpr` 预先设定别名 `T0`，或改用 `{From}` 占位符。
- **SelectExpr 早于 FromExpr**：`ExprString` 中若 `SelectExpr` 出现在 `FromExpr` 之前，未显式指定表别名的列可能不能正确绑定默认表（主查询已通过预建默认主表上下文规避，但子查询仍需注意）。建议在多表复杂查询中预先给 `FromExpr` / `PropertyExpr` 设好别名。
- **不支持自动展开 `CommonTableExpr`**：`ExprString` 不会把 `CommonTableExpr` 翻译成 `WITH` 子句。需要 CTE 时直接写完整 `WITH ... SELECT ...`，或用 `SelectExpr.With(name)` 构造 CTE 块再走结构化通道。

```csharp
// CTE 在 ExprString 里要手写完整 SQL
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

## 11. 常见错误示范

下面列出使用 `ExprString` 时容易踩的坑，每条给出「❌ 错误」与「✅ 正确」对照。

### 11.1 LIKE 模式包住插值项

`{keyword}` 会被参数化成 `@0`，但包在 `'%...%'` 字面量里会生成 `WHERE UserName LIKE '%@0%'`，占位符落在字面量内部不会被识别。

❌ `WHERE UserName LIKE '%{keyword}%'`
✅ `WHERE {Prop("UserName").Contains(keyword)}`

### 11.2 列名当普通值插入

普通值会被参数化成 `@0`，列名直接插入会变成字符串常量参数，而不是列引用。

❌ `WHERE {ageField} >= {minAge}`（`ageField` 是 `string`）
✅ `WHERE {Prop(ageField)} >= {minAge}`

### 11.3 引用符写死成单一方言

直接写 `` ` `` 或 `"` 不会被替换，跨数据库时失效。

❌ `` SELECT `Id` FROM `Users` ``
✅ `SELECT [Id] FROM [Users]`（框架按方言替换 `[ ]`）

### 11.4 完整 SQL 忘记 `isFull: true`

不传 `isFull: true` 时 DAO 会自动补 `SELECT {AllFields} FROM {From}`，导致重复 `SELECT`。

❌ 写完整 `SELECT ... FROM ...` 但不传 `isFull: true`
✅ 完整 SQL 显式声明 `isFull: true`

### 11.5 重复插入主表 FromExpr

`ExprString` 创建时已注册主表别名 `T0`，再插入主表 `FromExpr` 会触发别名重复分配。

❌ `WHERE {mainFrom} {Prop("Age")} >= {minAge}`（`mainFrom` 是主表）
✅ 直接写条件 `WHERE {Prop("Age")} >= {minAge}`，主表由上下文托管

### 11.6 期望 CommonTableExpr 自动展开

`ExprString` 不会把 `CommonTableExpr` 翻译成 `WITH` 子句。

❌ `SELECT * FROM {cte}`（`cte` 是 `CommonTableExpr`）
✅ 手写完整 `WITH ... SELECT ...`，或用 `SelectExpr.With(name)` 走结构化通道

### 11.7 在 Service 层调用 ExprString

`ExprString` 重载只在 DAO 层暴露，Service 层没有对应入口，编译期就会报找不到重载。

❌ `userViewService.SearchAsync($"WHERE ...")`
✅ 用 Service 的 `Expr`/Lambda 重载，或下沉到自定义 DAO

### 11.8 子查询 SelectExpr 早于 FromExpr 未指定别名

`ExprString` 按顺序消费 `Expr`，子查询里 `SelectExpr` 在 `FromExpr` 之前时，未指定表别名的列无法绑定默认表。

❌ 子查询先插 `SelectExpr`（列未指定别名）再插 `FromExpr`
✅ 给 `FromExpr` 和 `PropertyExpr` 都显式指定表别名

### 11.9 把用户输入塞进 RawSql

`RawSql` 内容会原样拼入 SQL，不经过参数化。把用户可控的字符串塞进去会直接导致 SQL 注入。

❌ `$"WHERE {new RawSql($"Name = '{userInput}'")}"`（`userInput` 来自前端）
✅ 用 `Expr.Prop("Name") == userInput` 或 `$"WHERE {Expr.Prop("Name")} = {userInput}"`，让框架参数化

## 12. 推荐写法小结

1. 能用 `Expr`/Lambda 表达的条件，优先构造 `Expr` 再插入 `ExprString`，避免在字符串里写死列名与值。
2. 普通值通过 `{变量}` 插入，交给框架参数化；不要用字符串拼接引入用户输入。
3. 手写标识符统一用 `[ ]` 占位，由框架按方言替换。
4. 复杂多表查询预先给 `FromExpr`/`PropertyExpr` 设好表别名，避免依赖上下文自动分配。
5. 完整 SQL 用 `isFull: true`，并配合 `{Table}`/`{From}`/`{AllFields}` 复用 DAO 的表定义。
6. CTE 直接写完整 `WITH ... SELECT ...`，或改用 `SelectExpr.With(name)`。
7. 仅在静态、受信任的方言片段场景下使用 `RawSql`；任何运行时值都必须走 `Expr` 或参数化路径。

## 13. 相关链接

- [查询总览](./04-query-overview.md)
- [Lambda 查询指南](./05-lambda-guide.md)
- [Expr 使用指南](./06-expr-guide.md)
- [增删改查](./03-crud-guide.md)
- [关联查询](./08-associations.md)
- [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)
- [CTE 指南](./10-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
