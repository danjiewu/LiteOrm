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
```

| 参数 | 说明 |
| --- | --- |
| `Name` | 数据库表名，支持占位符分表。 |
| `DataSource` | 指定当前实体所属数据源。 |

## `[Column]` 特性

```csharp
[Column("Id", IsPrimaryKey = true, IsIdentity = true)]
[Column("Profile", DataType = typeof(UserProfile))]
```

| 参数 | 说明 |
| --- | --- |
| `Name` | 数据库列名。 |
| `IsPrimaryKey` | 是否主键。 |
| `IsIdentity` | 是否自增列。 |
| `DataType` | 序列化类型，用于复杂对象存储。 |

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

- [返回目录](../SUMMARY.md)
- [视图模型与服务定义](./02-view-models-and-services.md)
- [关联查询](./05-associations.md)
- [术语表](../05-reference/03-glossary.md)

