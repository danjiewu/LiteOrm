# 软删除与历史数据的落地方式

LiteOrm 没有内置软删除：没有 `[SoftDelete]` 特性，也没有把 `Delete` 自动改写成 `Update` 的开关。它提供的是两个原语，用它们可以搭出软删除，但边界要自己定清楚。这一篇讲清楚搭法与容易漏的地方。

## 1. 框架给了什么

| 原语 | 表达能力 | 自动注入位置 |
| --- | --- | --- |
| `[Column(Constant = ...)]` → `TableDefinition.ConstFilter` | 固定的表级筛选条件 | 主表 `WHERE`、关联表 `JOIN ... ON`、`UPDATE` / `DELETE` 语句 |
| 运行时 `Expr` 条件 | 随请求、角色、场景变化的筛选条件 | 由调用方拼装进查询 |

判断标准仍是“编译期能不能确定”。已删除行永远不可见，那 `Constant` 就够用；回收站、管理员查看已删除数据、按状态统计这些场景需要运行时条件。

## 2. 查询侧：两种过滤方式

### 2.1 视图模型承载固定切片

把“未删除”这件事挂在只读的视图模型上，读路径自动带上条件：

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

`Constant` 值是常量，属性写成只读（`=> false`），语义就是“这个模型看到的数据天然都是未删除的”。生成 SQL 时条件会自动进入 `WHERE`、`JOIN ... ON`、`UPDATE`、`DELETE`。测试用例见 `ExprSqlConverterConstFilterTests`。

写操作走真实实体（不带 `Constant`），否则你连“把 IsDeleted 改成 true”这条更新都会被自己的条件拦住；或者更准确地说，任何需要触碰已删除行的维护操作都会被自动注入的条件挡住。

### 2.2 运行时条件

需要按角色或场景放开时，用运行时条件：

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

要点与数据权限一致：所有查询入口共用同一个拼装函数，详情、修改、删除仍要单独校验。回收站列表这类入口不追加未删除条件，但必须限制为管理员或数据归属人。

## 3. 写入侧：删除动作怎么写

框架的删除方法多数是 `virtual`，可以覆盖成软删除：

| 方法 | 是否 `virtual` |
| --- | --- |
| `Delete(T)` / `DeleteAsync(T)` | 是 |
| `DeleteID(object, params string[])` | 是 |
| `DeleteAll(LogicExpr, params string[])` | 是 |
| `BatchDelete` / `BatchDeleteAsync` / `BatchDeleteID` / `BatchDeleteIDAsync` | 是 |
| `DeleteIDAsync(object, string[]?, CancellationToken)` | 否 |
| `DeleteAllAsync(LogicExpr, string[]?, CancellationToken)` | 否 |

后两个异步入口是接口成员的直接实现，子类覆盖不到。与其逐个覆盖、还得记住哪几个盖不住，不如把删除语义写在自己的服务方法里：

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

这样入口只有一个，审计与权限检查也好挂。硬删除保留给运维工具，并显式走 `IObjectDAO<T>`。

一个细节：软删除的 `Update` 会触发 `OnUpdating` / `OnUpdated` 事件，如果审计依赖 `OnDeleted`，软删除的记录不会出现在“删除”类审计里。要么在 `OnUpdating` 里识别 `IsDeleted` 从 false 变 true 并记为删除事件，要么在业务方法里显式调用审计写入。

## 4. 唯一约束怎么处理

软删除最常见的翻车点是唯一索引。记录还在表里，业务键仍然占用唯一约束，于是同一个编号无法再次创建。三种处理方式：

| 方式 | 唯一键 | 代价 |
| --- | --- | --- |
| 唯一键加入删除标记 | `UNIQUE (Code, IsDeleted)` | 只能删除一次，第二次删除会与上一次冲突 |
| 唯一键加入删除时间 | `UNIQUE (Code, DeletedTime)` | `DeletedTime` 为空值参与唯一约束的行为依数据库而异 |
| 删除时改写业务键 | `Code = 'C001#deleted#20260914'` | 业务键不再可读，追溯要额外记录原值 |

更稳的做法是拆分：表里只保留有效记录的唯一约束，历史记录搬到归档表。归档表用分表或分库承载（见下一节）。

## 5. 关联与级联

- 主表软删除后，子表的关联查询如果按外键直接 `JOIN`，仍会关联到已删除行。若业务上不希望看到，子表视图模型也要带上自己的 `Constant` 条件，或者在关联条件里补 `IsDeleted = false`。
- 级联软删除要在同一个事务里完成。`ExecuteInTransaction` / `[Transaction]` 会把同一个 `SessionManager` 内的所有数据源上下文纳入同一事务，主表与子表的更新要么一起成功，要么一起回滚。

## 6. 统计口径与索引

- 统计、报表、导出如果走不同的查询入口，很容易出现“列表 100 条、统计 120 条”的差异。把“是否包含已删除”做成查询请求上的显式参数，而不是各入口自己决定。
- 未删除行过滤条件在数据量大的表上会走索引。`IsDeleted` 这种取值高度倾斜的列（99% 都是 false）单独建索引收益很小，建议建成 `(IsDeleted, 常用过滤列)` 的组合索引，或直接依赖既有索引加上过滤条件。

## 7. 历史数据归档

三种规模和三种做法：

| 规模 | 做法 | 说明 |
| --- | --- | --- |
| 单表数千万以内 | 同表加时间列，按时间分区 | 查询仍需带时间范围，索引要覆盖时间列 |
| 按时间切分 | `[Table("Orders_{0}")]` + `TableArgs` | 归档表名带月份，查询通过 `tableArgs` 指定 |
| 冷热分离 | `[Table("Orders", DataSource = "ArchiveDb")]` | 归档表在独立库，主库只留热数据 |

分表细节见[分表分库](../03-advanced-topics/02-sharding-and-tableargs.md)。归档动作建议做成独立的批处理任务，按主键区间分批搬运，每批一个事务，避免长事务与锁等待。

## 8. 常见误区

| 误区 | 后果 |
| --- | --- |
| 期望框架自动把 `Delete` 变成软删除 | 没有这个开关，删除真的会删数据 |
| 在实体上挂 `Constant = false` | 需要触碰已删除行的维护操作被自己的条件挡住 |
| 软删除后不改唯一约束 | 同一业务键无法重建 |
| 只覆盖 `Delete`，漏掉 `DeleteIDAsync` / `DeleteAllAsync` | 部分入口仍然是硬删除 |
| 审计只订阅 `OnDeleted` | 软删除不入审计 |
| 关联查询不排除已删除行 | 已删除的父记录仍然出现在子查询结果里 |

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../06-di/02-permission-filtering.md)
- [分表分库](../03-advanced-topics/02-sharding-and-tableargs.md)
- [事务](../06-di/01-transactions.md)
- [审计与变更追踪](./03-audit-trail.md)
- [多租户隔离的三种落地层次](./01-multi-tenancy.md)
