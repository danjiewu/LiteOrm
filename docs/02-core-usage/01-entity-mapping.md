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

### 计算列（非实际列）

计算列不生成物理数据库列、不参与插入/更新；查询时按 `Expression` 以表达式返回结果，查询条件中引用该属性时同样按表达式生成。表达式内用 `{属性名}` 引用同一实体的其他属性，占位符会按列名（含必要的引号与表限定）渲染；也可以直接书写数据库方言的原始 SQL 片段。

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

    // 计算列：不生成物理列，SELECT 返回 (FirstName || ' ' || LastName)，WHERE 中也按表达式生成
    [Column("FullName", Expression = "{FirstName} || ' ' || {LastName}", ColumnMode = ColumnMode.Computed)]
    public string? FullName { get; set; }
}
```

- **不生成物理列**：`CREATE TABLE` / `ALTER TABLE ADD COLUMN` 均跳过该列，插入/更新也不写入。
- **表达式返回结果**：默认 SELECT 渲染为 `({expr}) AS "PropertyName"`，读取结果回填到属性。
- **生成查询条件**：`SearchAsync(u => u.FullName == "张三 李四")` 会生成 `WHERE ("FirstName" || ' ' || "LastName") = @0`。
- 设了 `Expression` 即使未写 `ColumnMode.Computed`，也会自动视为计算列；建议显式声明 `ColumnMode = ColumnMode.Computed`。
- 表达式按数据库方言书写（示例为 SQLite/PostgreSQL 的 `||`，MySQL 用 `CONCAT(...)`）。

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


