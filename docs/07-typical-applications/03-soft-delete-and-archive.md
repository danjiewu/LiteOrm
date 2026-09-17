# 软删除与历史数据典型应用

LiteOrm 没有内置软删除：没有 `[SoftDelete]` 特性，也没有把 `Delete` 自动改写成 `Update` 的开关。能用的两个原语是固定切片（`[Column(Constant = ...)]`）与运行时 `Expr` 条件，判断标准还是那条：这个规则在编译期能不能确定：

| 原语 | 表达能力 | 自动注入位置 |
| --- | --- | --- |
| `[Column(Constant = ...)]` 聚合出的 `TableDefinition.ConstFilter` | 固定的表级筛选条件 | 主表 `WHERE`、关联查询的 `JOIN ... ON`、`UPDATE` / `DELETE` 的 `WHERE` |
| 运行时 `Expr` 条件 | 随请求、角色、场景变化的筛选条件 | 由调用方拼装进查询 |

## 场景 1：列表默认不显示已删除数据

**需求**：Customers 的删除只是打标记，所有查询默认都看不到已删除行，不希望每个查询都手写这个条件。

**做法**：把“未删除”挂在只读的视图模型上，读路径自动带上：

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

要点：

- `Constant` 的值是编译期常量，属性写成只读（`=> false`），语义就是“这个模型看到的数据天然都是未删除的”。布尔切片直接内联成字面量，不会走参数。
- 写操作要换成真实实体（不带 `Constant`），否则连“把 IsDeleted 改成 true”这条更新都会被自己的条件挡住，任何需要触碰已删除行的维护操作都做不了。
- 视图模型上声明的切片同样作用于统计与导出，前提是这些入口也走同一个视图类型。
- 启用 `TableInfo` 源生成（NativeAOT）时特性里的切片不会生成 `ConstFilter`，需要把固定条件改成运行时构件，写法见[多租户隔离典型应用](./01-tenant-isolation.md)的场景 4。

## 场景 2：回收站与管理员查看已删除数据

**需求**：管理员能查到已删除记录，普通用户不能，删除原因与删除时间要能显示。

**做法**：把开关做成查询请求上的显式参数，在条件拼装处决定是否追加未删除条件：

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

要点：

- 回收站入口不追加未删除条件，但必须限制为管理员或数据归属人，否则等于把删除数据暴露给所有用户。
- 统计、报表、导出如果各自决定“是否包含已删除”，就会出现“列表 100 条、统计 120 条”的差异。把 `IncludeDeleted` 做成请求上的字段，所有入口读同一个值。
- 详情、修改、删除仍要单独校验，见[数据权限典型应用](./02-data-permission.md)的场景 3。

## 场景 3：删除动作改成打标记

**需求**：业务要求删除留痕，同时记录删除人和删除原因。

**做法**：把软删除写成一个服务方法，入口只有一个：

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

框架的删除方法多数是 `virtual`，可以覆盖成软删除：

| 方法 | 是否 `virtual` |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | 是 |
| `DeleteID(object, params string[])` | 是 |
| `DeleteAll(LogicExpr, params string[])` | 是 |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | 是 |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | 否 |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | 否 |

要点：

- 后两个异步入口是接口成员的直接实现，子类覆盖不到。与其逐个覆盖、还要记住哪几个盖不住，不如把删除语义写在自己的服务方法里。
- 硬删除留给运维工具，并显式走 `IObjectDAO<T>`，不要和业务删除混在同一个入口上。
- 软删除走的是一次 `Update`，触发的是 `OnUpdating` / `OnUpdated`，不会触发 `OnDeleted`。审计依赖删除事件时要么在 `OnUpdating` 里识别 `IsDeleted` 由 `false` 变 `true` 记为删除，要么在业务方法里显式写审计，见[审计与变更追踪典型应用](./04-audit-and-change-tracking.md)。

## 场景 4：软删除之后同一个业务编号还能再建

**需求**：记录还在表里，唯一约束仍然占用业务键，删除后重新创建同编号的客户会冲突。

**做法**：三种处理方式与代价：

| 方式 | 唯一键 | 代价 |
| --- | --- | --- |
| 唯一键加入删除标记 | `UNIQUE (Code, IsDeleted)` | 只能删除一次，第二次删除会与上一次冲突 |
| 唯一键加入删除时间 | `UNIQUE (Code, DeletedTime)` | `DeletedTime` 为空值参与唯一约束的行为依数据库而异 |
| 删除时改写业务键 | `Code = 'C001#deleted#20260914'` | 业务键不再可读，追溯要额外记录原值 |

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

要点：

- 更稳的做法是拆分：业务表只保留有效记录的唯一约束，历史记录搬到归档表，唯一键冲突从根上消失。
- 归档表用分表或分库承载，见场景 6。
- 无论选哪种方式，都要在迁移脚本里处理已经存在的重复数据，否则约束建不上。

## 场景 5：关联查询与级联软删除

**需求**：主表记录软删除后，子表关联查询不应该再关联到已删除的父记录；删除父记录时子记录一起打标记。

**做法**：子表的视图模型带上自己的切片条件；级联删除放进同一个事务：

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

要点：

- 主表软删除后，子表如果按外键直接 `JOIN`，仍会关联到已删除行。子表视图模型要带自己的切片，或在关联条件里补 `IsDeleted = false`。
- 级联软删除必须在同一个事务里完成。`[Transaction]` 与 `ExecuteInTransaction` 会把同一个 `SessionManager` 内的所有数据源上下文纳入同一事务，主表与子表的更新要么一起成功，要么一起回滚。
- 被关联表的切片条件在视图构建时就按别名备好，关联查询的 `JOIN ... ON` 会自动带上，运行时不需要额外拼条件；DAO 按主键读取（`GetObject`、`ExistsKey`）走的是模型自带的 `From` 片段，不含这条条件。

## 场景 6：历史数据归档

**需求**：表越来越大，历史数据要移出去，查询仍然按范围可达。

**做法**：按规模选方案：

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

要点：

- 归档动作做成独立的批处理任务，按主键区间分批搬运，每批一个事务，避免长事务与锁等待。
- 分表细节与 `TableArgs` 的传递规则见[分表分库](../03-advanced-topics/01-sharding-and-tableargs.md)。
- 已删除行要不要一起搬走由保留策略决定。留在主库的已删除行会持续参与条件过滤，取值高度倾斜的 `IsDeleted` 列单独建索引收益很小，建议建成 `(IsDeleted, 常用过滤列)` 组合索引。

## 相关链接

- [返回目录](../README.md)
- [数据权限典型应用](./02-data-permission.md)
- [审计与变更追踪典型应用](./04-audit-and-change-tracking.md)
- [多租户隔离典型应用](./01-tenant-isolation.md)
- [分表分库](../03-advanced-topics/01-sharding-and-tableargs.md)
- [事务](../06-di/01-transactions.md)
