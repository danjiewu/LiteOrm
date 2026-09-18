# 软删除与历史数据

LiteOrm 没有内置软删除：没有 `[SoftDelete]` 特性，也不会把 `Delete` 自动改写成 `Update`。可用的原语只有两个，区别在规则是否能在编译期确定：

| 原语 | 能力 | 注入位置 |
| --- | --- | --- |
| `[Column(Constant = ...)]` 聚合出 `TableDefinition.ConstFilter` | 固定表级筛选 | 主表 `WHERE`、关联 `JOIN ... ON`、`UPDATE` / `DELETE` 的 `WHERE` |
| 运行时 `Expr` 条件 | 随请求、角色、场景变化 | 由调用方拼装进查询 |

## 场景 1：列表默认不显示已删除数据

把“未删除”挂在只读视图模型上，读路径自动带上：

```csharp
[Table("Customers")]
public class CustomerView : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("IsDeleted", Constant = false)]
    public bool IsDeleted => false;
}
```

```sql
SELECT * FROM "Customers" "T0" WHERE "T0"."IsDeleted" = 0
```

- `Constant` 是编译期常量，只读属性的语义即“此模型看到的数据未删除”，布尔切片内联为字面量，不走参数。
- 写操作必须走不带 `Constant` 的真实实体，否则连“把 `IsDeleted` 改成 true”的更新都会被自己的条件挡住。
- 视图上的切片同样作用于统计与导出，前提是这些入口用同一个视图类型。
- NativeAOT 源生成下特性切片不生成 `ConstFilter`，需运行时赋 `TableDefinition.ConstFilter`，写法见[多租户隔离](./tenant-isolation.md)示例三。

## 场景 2：回收站与管理员查看已删除数据

把开关做成请求上的显式字段，在条件拼装处决定是否追加“未删除”：

```csharp
using static LiteOrm.Common.Expr;

private Expr BuildCustomerFilter(CustomerQueryRequest request, ICurrentUser user)
{
    var filter = Prop(nameof(Customer.Id)) > 0;

    if (!request.IncludeDeleted && !user.IsAdmin)
        filter &= Prop(nameof(Customer.IsDeleted)) == false;

    return filter;
}
```

- 回收站入口不追加该条件，但必须限管理员或数据归属人，否则等于把删除数据暴露给所有用户。
- `IncludeDeleted` 放请求字段上，列表、统计、报表、导出读同一个值，避免“列表 100 条、统计 120 条”的差异。
- 详情、修改、删除仍要各自校验，见[数据权限](./data-permission.md)场景 3。

## 场景 3：删除动作改成打标记

软删除收敛成一个服务方法，同时记录删除人与原因：

```csharp
public interface ICustomerService : IEntityService<Customer>
{
    Task<bool> SoftDeleteAsync(long id, string reason, CancellationToken cancellationToken = default);
}

public sealed class CustomerService : EntityService<Customer>, ICustomerService
{
    public CustomerService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public async Task<bool> SoftDeleteAsync(long id, string reason, CancellationToken cancellationToken = default)
    {
        var customer = await GetObjectAsync(id, cancellationToken: cancellationToken);
        if (customer is null || customer.IsDeleted) return false;

        customer.IsDeleted = true;
        customer.DeletedTime = DateTime.Now;
        customer.DeletedReason = reason;

        return await UpdateAsync(customer, cancellationToken);
    }
}
```

框架的所有删除入口都是 `virtual`，但逐个覆盖成全套软删除并不明智：

| 方法 | `virtual` |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | 是 |
| `DeleteID(object, params string[])` | 是 |
| `DeleteAll(LogicExpr, params string[])` | 是 |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | 是 |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | 是 |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | 是 |

- 软删除还要顺带记录删除人与原因，逐个覆盖这些入口容易遗漏、语义分裂。不如把软删除收敛成一个自定义服务方法（如上面的 `SoftDeleteAsync`），入口唯一、职责清晰。
- 硬删除留给运维工具，显式走 `IObjectDAO<T>`，不与业务删除混在同一个入口。
- 软删除是一次 `Update`，触发 `OnUpdating` / `OnUpdated`，不会触发 `OnDeleted`。审计若要区分删除，在 `OnUpdating` 里识别 `IsDeleted` 由 `false` 变 `true`，或在业务方法里显式写审计，见[审计与变更追踪](./audit-and-change-tracking.md)。

## 场景 4：软删除后同一业务编号还能再建

记录仍在表中，唯一约束仍占用业务键，删后重建会冲突。三种处理方式与代价：

| 方式 | 唯一键 | 代价 |
| --- | --- | --- |
| 唯一键加入删除标记 | `UNIQUE (Code, IsDeleted)` | 只能删除一次，第二次删除与上一次冲突 |
| 唯一键加入删除时间 | `UNIQUE (Code, DeletedTime)` | `DeletedTime` 为空值参与唯一约束的行为依数据库而异 |
| 删除时改写业务键 | `Code = 'C001#deleted#20260914'` | 业务键不再可读，需额外记录原值 |

```csharp
[Table("Customers")]
public class Customer : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Code", IsUnique = true)]
    public string Code { get; set; } = string.Empty;

    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }
}
```

- 更稳的做法是拆分：业务表只对有效记录建唯一约束，历史记录搬进归档表，冲突从根上消失。
- 归档表用分表或分库承载，见场景 6。
- 迁移脚本必须先处理已有的重复数据，否则约束建不上。

## 场景 5：关联查询与级联软删除

主表软删除后，子表的关联查询不应再关联到已删除的父记录；删父时子记录一起打标记。子表视图带上自己的切片，级联放进同一事务：

```csharp
[Table("Orders")]
public class OrderView : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("CustomerId")]
    [ForeignType(typeof(Customer), Alias = "Customer")]
    public long CustomerId { get; set; }

    [Column("IsDeleted", Constant = false)]
    public bool IsDeleted => false;
}
```

```csharp
[Transaction]
public async Task<bool> DeleteCustomerAsync(long customerId, string reason, CancellationToken cancellationToken = default)
{
    var affected = await customerService.UpdateAllAsync(
        Expr.Update<Customer>(c => new Customer { IsDeleted = true, DeletedReason = reason }, c => c.Id == customerId),
        null, cancellationToken);

    await orderService.UpdateAllAsync(
        Expr.Update<Order>(o => new Order { IsDeleted = true }, o => o.CustomerId == customerId),
        null, cancellationToken);

    return affected > 0;
}
```

- 按外键直接 `JOIN` 仍会关联到已删除行，子表视图要带自己的切片，或在关联条件里补 `IsDeleted = false`。
- 级联必须在一个事务里完成。`[Transaction]` / `ExecuteInTransaction` 把同一 `SessionManager` 内的所有数据源上下文纳入同一事务，要么一起成功，要么一起回滚。
- 被关联表的切片在视图构建时按别名备好，自动带进 `JOIN ... ON`；DAO 按主键读取（`GetObject`、`ExistsKey`）走模型自带的 `From` 片段，不含该条件。

## 场景 6：历史数据归档

按规模选方案：

| 规模 | 做法 | 说明 |
| --- | --- | --- |
| 单表数千万以内 | 同表加时间列，按时间分区 | 查询仍需带时间范围，索引要覆盖时间列 |
| 按时间切分 | `[Table("Orders_{0}")]` + `TableArgs` | 归档表名带月份，查询通过 `tableArgs` 指定 |
| 冷热分离 | `[Table("Orders", DataSource = "ArchiveDb")]` | 归档表在独立库，主库只留热数据 |

```csharp
[Table("Orders_{0}")]
public class OrderArchive : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    string[] IArged.TableArgs => new[] { CreateTime.ToString("yyyyMM") };
}

// 按月份读取归档表
var archived = await archiveService.SearchAsync(From<OrderArchive>("202609"));
```

- 归档做成独立的批处理任务，按主键区间分批搬运，每批一个事务，避免长事务与锁等待。
- 分表细节与 `TableArgs` 传递规则见[分表分库](../advanced-topics/sharding-and-tableargs.md)。
- 已删除行是否一起搬由保留策略决定。留在主库的已删除行持续参与过滤，取值高度倾斜的 `IsDeleted` 单列索引收益很小，建成 `(IsDeleted, 常用过滤列)` 组合索引更合适。

## 相关链接

- [返回目录](../README.md)
- [数据权限](./data-permission.md)
- [审计与变更追踪](./audit-and-change-tracking.md)
- [多租户隔离](./tenant-isolation.md)
- [分表分库](../advanced-topics/sharding-and-tableargs.md)
- [事务](../di/transactions.md)