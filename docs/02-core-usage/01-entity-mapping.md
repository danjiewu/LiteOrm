# 实体映射与数据源

实体类是 LiteOrm 与数据库表之间的映射基础。本文介绍实体定义、表列映射、多数据源和分表参数等核心规则。

## 基本实体结构

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("DeptId")]
    public int? DeptId { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
}
```

> `ObjectBase` 是可选基类，不继承也可以正常使用 LiteOrm。

## `[Table]` 特性

```csharp
[Table("Users")]
[Table("Logs_{0}", DataSource = "LogDB")]
[Table("Logs", SyncTable = SyncTableMode.Always)]
```

| 参数 | 说明 |
| --- | --- |
| `Name` | 数据库表名，支持占位符分表。 |
| `DataSource` | 指定当前实体所属数据源。 |
| `SyncTable` | 实体级表结构同步模式，枚举 `SyncTableMode`（`Default` / `Never` / `Always`），默认 `Default`。设为 `Never` 或 `Always` 时将覆盖数据源级别的 `SyncTable` 配置。 |

## `[Column]` 特性

```csharp
[Column("Id", IsPrimaryKey = true, IsIdentity = true)]
[Column("Age", DbType = DbValueType.Int32)]
[Column("Tags", DbType = DbValueType.Array)]      // PostgreSQL text[]
[Column("Meta", DbType = DbValueType.Jsonb)]      // PostgreSQL jsonb
```

> `DbType` 的类型为 `DbValueType` 枚举，默认 `DbValueType.Default`（表示未显式指定、按属性类型自动推断）。

| 参数 | 说明 |
| --- | --- |
| `ColumnName` | 数据库列名（构造函数位置参数）。 |
| `IsPrimaryKey` | 是否主键。 |
| `IsIdentity` | 是否自增列。 |
| `IdentityStart` | 自增列起始值，默认 `1`。仅在支持起始值的数据库（SQL Server、达梦、Oracle）生效；MySQL 通过表级 `AUTO_INCREMENT = n` 选项设置；SQLite 不支持自定义。 |
| `IdentityIncreasement` | 自增列增量值，默认 `1`。仅在支持增量的数据库（SQL Server、达梦、Oracle）生效；MySQL 需通过会话变量 `auto_increment_increment` 设置；SQLite 不支持自定义。 |
| `DbType` | 数据库列类型（`DbValueType` 枚举），默认 `DbValueType.Default`（按属性类型自动推断）。`Json`/`Jsonb` 表示 JSON/JSONB 列，`Array` 表示数组列。 |
| `Expression` | 计算列表达式（非实际列），用 `{属性名}` 引用同一实体的其他属性，或直接书写数据库方言 SQL 片段。 |
| `ColumnMode` | 列操作模式（`ColumnMode` 枚举），默认 `Full`。计算列设为 `ColumnMode.Computed`。 |

> **复杂类型需显式 `[Column]`**：数组/集合、以及自定义类（映射为 `Object`）等复杂类型的属性，若未标注 `[Column]` **不再被自动识别为表列**，必须显式 `[Column]`（并按其场景指定 `DbType = Array` 等）才会持久化。已知标量（数值、`string`/`char`、`byte[]`→`Binary`、`Guid`、日期、枚举→`Int32`）以及 `Json`/`Jsonb` 映射类型仍会自动映射为列。该规则在运行时（`AttributeTableInfoProvider`）与 AOT 源生成器（`TableInfoGenerator`）中保持行为一致。

### 数组列（PostgreSQL）

集合类型属性（`int[]`、`string[]`、`List<T>` 等）未显式指定 `DbType` 时自动推断为 `DbValueType.Array`。PostgreSQL 等原生数组方言据此生成数组列类型（如 `integer[]`、`text[]`），其余方言回退为文本 JSON 存储：

```csharp
[Table("Products")]
public class Product
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Tags")]
    public string[]? Tags { get; set; }   // 自动推断为 DbValueType.Array → PostgreSQL text[]

    [Column("Scores")]
    public int[]? Scores { get; set; }    // → PostgreSQL integer[]
}
```

### JSON / JSONB 列

```csharp
[Table("Products")]
public class Product
{
    [Column("Meta", DbType = DbValueType.Json)]     // 文本 JSON（各数据库兼容）
    public string? Meta { get; set; }

    [Column("Attributes", DbType = DbValueType.Jsonb)]  // PostgreSQL jsonb
    public string? Attributes { get; set; }
}
```

> JSON 列存储时复杂对象会被序列化为 JSON 字符串，读取时反序列化回属性类型。

`System.Text.Json.Nodes.JsonNode` 类型的属性已内置支持：无需显式指定 `DbType`，会自动映射为 `DbValueType.Json` 列，写入时序列化为 JSON 字符串、读取时还原为 `JsonNode`。查询时还可直接用索引器或 `GetValue<T>()` 走 JSON 路径（对应 `JsonExtract` / `JsonValue` SQL 函数）：

```csharp
[Table("Products")]
public class Product
{
    [Column("Meta")]                 // JsonNode 自动映射为 DbValueType.Json
    public JsonNode? Meta { get; set; }
}

// 查询：Meta['price'] 提取 JSON 属性
Expr.Lambda<Product>(p => p.Meta!["price"] > 10);
// 查询：Meta['name'].GetValue<string>() 提取标量
Expr.Lambda<Product>(p => p.Meta!["name"].GetValue<string>() == "Lite");
```

### 计算列（非实际列）

计算列不生成物理数据库列、不参与插入/更新；查询时按表达式返回结果，查询条件中引用该属性时同样按表达式生成。计算列表达式支持两种形式：

- **字符串形式**（`ColumnAttribute.Expression`）：用 `{属性名}` 引用同一实体的其他属性，占位符会按列名（含必要的引号与表限定）渲染；也可以直接书写数据库方言的原始 SQL 片段。
- **Expr 树形式**（`ColumnDefinition.ExpressionExpr`）：使用 `Expr.Prop("Price") * Expr.Prop("Quantity")` 等 Expr 树构建，运行时动态设置；仅允许不生成参数的固定 SQL 表达式（属性引用、常量、函数、算术运算），若渲染时产生参数化值将抛出 `NotSupportedException`。

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("FirstName")]
    public string? FirstName { get; set; }

    [Column("LastName")]
    public string? LastName { get; set; }

    // 计算列（字符串形式）：不生成物理列，SELECT 返回 (FirstName || ' ' || LastName)，WHERE 中也按表达式生成
    [Column("FullName", Expression = "{FirstName} || ' ' || {LastName}", ColumnMode = ColumnMode.Computed)]
    public string? FullName { get; set; }
}
```

Expr 树形式需在运行时动态设置（`ColumnAttribute` 不支持 Expr 树）：

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Price")]
    public decimal Price { get; set; }

    [Column("Quantity")]
    public int Quantity { get; set; }

    // 计算列（Expr 树形式）：声明为 Computed，表达式动态设置
    [Column("Total", ColumnMode = ColumnMode.Computed)]
    public decimal Total { get; set; }
}

// 运行时动态设置 ExpressionExpr
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
table.Columns.First(c => c.Name == "Total").ExpressionExpr = Expr.Prop("Price") * Expr.Prop("Quantity");
// 之后查询时 SELECT 渲染为 (T0."Price" * T0."Quantity") AS "Total"
```

- **不生成物理列**：`CREATE TABLE` / `ALTER TABLE ADD COLUMN` 均跳过该列，插入/更新也不写入。
- **表达式返回结果**：默认 SELECT 渲染为 `({expr}) AS "PropertyName"`，读取结果回填到属性。
- **生成查询条件**：`SearchAsync(u => u.FullName == "张三 李四")` 会生成 `WHERE ("FirstName" || ' ' || "LastName") = @0`。
- 设了 `Expression` / `ExpressionExpr` 即使未写 `ColumnMode.Computed`，也会自动视为计算列；建议显式声明 `ColumnMode = ColumnMode.Computed`。
- 字符串形式按数据库方言书写（示例为 SQLite/PostgreSQL 的 `||`，MySQL 用 `CONCAT(...)`）；Expr 树形式自动按方言渲染。
- Expr 树形式同时设置时优先于字符串形式；仅允许固定 SQL（不生成参数）。`Expr.Const(100)` 可用；`Expr.Const(" ")` 等常规字符串常量以 `' '` 形式内联（单引号以 `''` 转义），含反斜杠或控制字符的字符串仍会参数化并抛异常；`Expr.Value("str")` 始终参数化，会抛异常。

### 动态修改列定义

通过 `TableInfoProvider.Instance.GetTableDefinition` 获取实体表定义后，可在运行时动态修改 `ColumnDefinition` 的属性，覆盖 `[Column]` 特性的静态声明：

```csharp
var table = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;

// 动态设置计算列表达式
var totalCol = table.Columns.First(c => c.Name == "Total");
totalCol.ExpressionExpr = Expr.Prop("Price") * Expr.Prop("Quantity");

// 动态修改列长度、允许空、DbType 等
var priceCol = table.Columns.First(c => c.Name == "Price");
priceCol.Length = 18;
priceCol.AllowNull = false;
priceCol.DbType = DbValueType.Decimal;
```

> **注意**：`TableDefinition` 由 `TableInfoProvider` 全局缓存，修改后对所有后续查询生效。建议在应用启动阶段（如 `AddLiteOrm()` 之后、首次查询之前）统一设置，避免运行期竞争。
>
> `GenerateTableView` 复用 `TableDefinition` 中的 `ColumnDefinition` 对象（不重复创建），因此动态修改 `ExpressionExpr` 等属性只需在 `TableDefinition` 上设置一处，`TableView`（SELECT 渲染）即可同时生效。

### `[Column]` 特性参数一览

| 参数 | 类型 | 说明 |
|------|------|------|
| `ColumnName` | `string` | 数据库列名，默认使用属性名。 |
| `IsColumn` | `bool` | 是否为实际列，默认 `true`。设为 `false` 时该属性不参与任何数据库操作。 |
| `IsPrimaryKey` | `bool` | 是否为主键。 |
| `IsIdentity` | `bool` | 是否为自增标识列。 |
| `IsTimestamp` | `bool` | 是否为时间戳列（乐观并发控制）。 |
| `IsIndex` | `bool` | 是否创建索引。 |
| `IsUnique` | `bool` | 是否具有唯一约束。 |
| `AllowNull` | `bool` | 是否允许为空。 |
| `Length` | `int` | 列长度，0 表示使用数据库默认值。 |
| `DbType` | `DbValueType` | 列的取值类型，`Default` 表示按属性类型自动推断。 |
| `Expression` | `string` | 计算列表达式（字符串形式），支持 `{属性名}` 占位符。 |
| `DefaultValue` | `string` | 列的默认值（SQL 片段）。 |
| `ColumnMode` | `ColumnMode` | 列操作模式（`Read`/`Insert`/`Update`/`Full`/`Computed`），默认 `Full`。 |
| `IdentityExpression` | `string` | 标识列表达式（如 Oracle 序列名）。 |
| `IdentityStart` | `long` | 自增起始值，默认 1。 |
| `IdentityIncreasement` | `int` | 自增步长，默认 1。 |

### 预注册 Lambda 解析实现计算列

除了通过 `Expression`（字符串）或 `ExpressionExpr`（Expr 树）设置计算列外，还可以通过预注册 Lambda 成员处理器的方式，让实体上的只读计算属性直接在 Lambda 查询中使用：

```csharp
public class User
{
    public DateTime BirthDate { get; set; }

    // Age 是只读计算属性，不存储在数据库
    public int Age => DateTime.Now.Year - BirthDate.Year;
}

// 注册成员处理器，将 Lambda 中对 Age 的访问转换为 SQL 表达式
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", (node, converter) =>
{
    return new FunctionExpr("YEAR", new FunctionExpr("CURRENT_DATE"))
         - new FunctionExpr("YEAR", new PropertyExpr("BirthDate"));
});

// 之后即可在 Lambda 查询中使用
var adults = await userService.SearchAsync(u => u.Age >= 18);
```

此方式适用于无法通过 `[Column]` 特性静态声明的动态计算逻辑。完整步骤（含 SQL 函数处理器注册）见 [表达式扩展 — 示例二：计算属性](../04-extensibility/01-expression-extension.md#5-示例二计算属性)。

## `[PropertyOrder]` 特性

用于控制实体属性在数据库操作（如建表、生成 SQL 列列表）中的排列顺序。

```csharp
[Table("Users")]
public class User
{
    [PropertyOrder(1)]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [PropertyOrder(2)]
    [Column("UserName")]
    public string? UserName { get; set; }

    [PropertyOrder(After = nameof(DeptId))]
    [Column("Age")]
    public int Age { get; set; }

    [PropertyOrder(0)]
    [Column("DeptId")]
    public int? DeptId { get; set; }
}
```

| 参数 | 说明 |
| --- | --- |
| `Order` | 排序优先级，数值越小越靠前，默认值为 0。同一拓扑层级中 Order 值较小的属性优先排列。 |
| `After` | 指定属性名，指示当前属性应排在该属性之后。 |
| `Before` | 指定属性名，指示当前属性应排在该属性之前。 |

> **排序规则**：首先按 Before/After 指定的拓扑依赖关系排序，同一层级按 Order 值升序排列，最后按属性原始声明顺序排列。当检测到循环依赖时，将抛出 `InvalidOperationException` 异常。

## 多数据源映射

如果项目中存在多个数据源，可以在实体上显式标注：

```csharp
[Table("Orders", DataSource = "OrderDb")]
public class Order
{
}
```

这样该实体的默认读写都会走 `OrderDb` 数据源。

## 分表参数与 `IArged`

当表名中包含占位符时，可通过 `IArged` 提供动态分表参数：

```csharp
[Table("Logs_{0}")]
public class Log : IArged
{
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}
```

更多内容请阅读 [分表分库与 TableArgs](../03-advanced-topics/02-sharding-and-tableargs.md)。

## 建模建议

- 实体优先保持简单，避免在实体中塞入大量业务逻辑。
- 主键、自增、数据源等元信息应在模型层一次性定义清楚。
- 需要关联查询的字段，优先用视图模型承载，不要污染基础实体。
- 涉及跨数据库或旧数据库兼容时，尽量提前确认对应方言行为。

## 相关链接

- [返回目录](../README.md)
- [视图模型与服务定义](./02-view-models-and-services.md)
- [关联查询](./08-associations.md)
- [术语表](../05-reference/03-glossary.md)


