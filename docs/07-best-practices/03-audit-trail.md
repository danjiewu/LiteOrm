# 审计与变更追踪怎么接

审计分两类需求：一类是“谁在什么时候调了哪个接口”，一类是“这条记录从什么变成了什么”。前者靠服务调用事件，后者靠实体服务事件。这一篇把两者的触发条件、能拿到什么、写到哪里去讲清楚。

## 1. 可用的三类事件

| 事件 | 定义位置 | 触发时机 | 适合记什么 |
| --- | --- | --- | --- |
| `IEntityServiceEvent<T>` | `LiteOrm` / `LiteOrm.Common` | 实体增删改前后 | 数据变更明细、字段 diff |
| `IServiceInvokingEvent` / `IServiceInvokedEvent` / `IServiceExceptionEvent` | `LiteOrm.DependencyInjection` | 服务方法调用前、成功后、异常后 | 操作日志、接口耗时、失败原因 |
| `DAOContextPool.DatabaseSync.OnTableSyncing` | `LiteOrm` | 建表同步前 | 建表审计（很少用） |

三类事件都通过容器注册订阅者，不需要改业务代码。下面分别说明。

## 2. 实体服务事件

接口有 16 个成员，8 个 Before 返回 `bool`（返回 `false` 取消本次操作），8 个 After 返回 `void`。只关心其中几个时，继承 `EntityServiceEventBase<T>`，基类把 Before 都实现为返回 `true`、After 实现为空方法：

```csharp
public sealed class OrderAuditEvent : EntityServiceEventBase<Order>
{
    private readonly ICurrentUser _user;
    private readonly IAuditWriter _writer;

    public OrderAuditEvent(ICurrentUser user, IAuditWriter writer)
    {
        _user = user;
        _writer = writer;
    }

    public override bool OnInserting(Order entity)
    {
        _writer.Record(new AuditEntry("Order", entity.Id, "Insert", _user.Id, null, Serialize(entity)));
        return true;
    }

    public override void OnUpdated(Order entity)
        => _writer.Record(new AuditEntry("Order", entity.Id, "Update", _user.Id, null, Serialize(entity)));
}
```

注册：

```csharp
services.AddScoped<IEntityServiceEvent<Order>, OrderAuditEvent>();
```

订阅者由容器解析，可以注入 `ICurrentUser`、`ILogger`、仓储等依赖。订阅者集合在首次触发事件通知时才向容器索取，所以订阅者构造函数里的异常会在第一次实体操作时暴露，而不是在解析 `IEntityService<Order>` 时暴露。

### After 事件的触发条件

不是所有路径都会触发 After，写审计时要知道这些差异：

| 方法 | After 触发条件 |
| --- | --- |
| `Insert` / `Update` / `Delete` | 操作返回 `true` |
| `UpdateOrInsert` | 结果为 `Inserted` 或 `Updated` |
| `DeleteID` | 按主键删除成功 |
| `DeleteAll` / `UpdateAll` | 受影响行数大于 0 |
| `BatchDeleteID` | 无条件触发 |
| `BatchInsert` / `BatchUpdate` / `BatchDelete` | 逐条触发单条事件 |

批量方法会先让所有 Before 回调过一遍，被取消的元素不进 `remaining`，After 只针对真正参与操作的元素触发。这意味着一批 100 条里如果有 3 条被 Before 取消，After 只会来 97 次。

### 触发路径的限制

实体服务事件由 `EntityService<T>` 触发。直接从 `IObjectDAO<T>` 写库、或用 `DataDAO<T>` 执行自定义 SQL，都不会触发这些事件。所以审计要求“所有业务写入都经过服务层”，这应该作为一条代码规范，最好配合架构测试（禁止业务项目直接引用 `IObjectDAO<T>`）。

## 3. 取旧值与字段 diff

`OnUpdating(Order entity)` 拿到的是新值，旧值需要自己查：

```csharp
public sealed class OrderDiffEvent : EntityServiceEventBase<Order>
{
    private readonly IEntityViewService<Order> _viewService;
    private readonly IAuditWriter _writer;

    public OrderDiffEvent(IEntityViewService<Order> viewService, IAuditWriter writer)
    {
        _viewService = viewService;
        _writer = writer;
    }

    public override bool OnUpdating(Order entity)
    {
        var before = _viewService.GetObject(entity.Id);
        if (before is not null)
            _writer.Record(BuildDiff(entity.Id, before, entity));

        return true;
    }

    private static AuditEntry BuildDiff(long id, Order before, Order after) { /* 逐列比较 */ }
}
```

三点说明：

- 事件回调本身就带返回值语义，`OnUpdating` 返回 `bool`。需要异步写审计时不要用 `async void` 覆盖同步回调，改成先收集变更、再在事务提交后批量落盘，或者直接提供服务入口处的显式审计步骤。
- 读路径如果是视图 DAO，可能落在只读副本上。存在复制延迟时，读到的旧值可能比真正提交的值更旧。审计要求严格一致时，把旧值查询绑定在主库上（例如在服务入口先读一次，再调用服务写入）。
- 分表实体要带上 `tableArgs`（`GetObject(id, tableArgs)`），否则旧值可能从默认表读出来。字段 diff 不要用反射逐个属性比字符串，按列的元数据遍历更稳。

## 4. 审计表写到哪里

审计记录放同库还是异库，取决于能不能接受“业务回滚、审计保留”。

同一个 `SessionManager` 内的所有数据源上下文会被 `BeginTransaction` 一并纳入事务（只读连接跳过），事务中新建的上下文也会加入。所以把审计实体标到另一个数据源，并不会让它独立于业务事务：

```csharp
[Table("AuditEntries", DataSource = "AuditDb")]
public class AuditEntry : ObjectBase { /* ... */ }
```

要让审计独立落地，两条路：

1. 审计写入放在事务提交之后。业务服务方法内部不开事务，由上层在提交成功后调用审计。
2. 另起一个作用域。`IServiceScopeFactory.CreateScope()` 会拿到独立的 `SessionManager` 与独立的事务边界，审计写入不受业务事务回滚影响。

反过来，如果要求“业务失败则审计也没有”，就把审计写入放进同一事务，并接受同库写入的成本。

`After` 系列在语句成功之后就触发，此时外层事务尚未提交。如果业务后续回滚，已经写下的审计记录会留在库里。审计链路需要明确选择一种语义：记录“尝试过的操作”，或者记录“最终生效的变更”。

## 5. 服务级审计

接口调用日志用三个事件接口，它们在服务方法执行前、成功返回后、抛异常后触发：

```csharp
public sealed class OperationLogEvent : IServiceInvokingEvent, IServiceInvokedEvent, IServiceExceptionEvent
{
    private readonly ILogger<OperationLogEvent> _logger;

    public OperationLogEvent(ILogger<OperationLogEvent> logger) => _logger = logger;

    public void OnInvoking(ServiceInvokeContext context)
        => _logger.LogInformation("Invoke {Service}.{Method}", context.ServiceName, context.MethodName);

    public void OnInvoked(ServiceInvokeContext context)
        => _logger.LogInformation("Return {Service}.{Method} in {Duration}", context.ServiceName, context.MethodName, context.Duration);

    public void OnException(ServiceExceptionContext context)
        => _logger.LogError(context.Exception, "Failed {Service}.{Method}", context.ServiceName, context.MethodName);
}

services.AddScoped<IServiceInvokingEvent, OperationLogEvent>();
services.AddScoped<IServiceInvokedEvent, OperationLogEvent>();
services.AddScoped<IServiceExceptionEvent, OperationLogEvent>();
```

`ServiceInvokeContext` 提供 `ServiceType`、`ServiceName`、`Method`、`MethodName`、`Arguments`、`SessionId`、`Duration`、`Result`。其中 `SessionId` 能把同一次会话里的多条服务调用串起来，是排查问题的关键字段。

两个限制：

- 事件只在通过接口调用服务时触发，需要 `LiteOrm.DependencyInjection` 的拦截器生效（方法必须是接口方法）。
- 嵌套的服务调用不触发事件。服务 A 内部调用服务 B，只有 A 的调用会记一次。审计需要完整的调用链时，应在应用层传递链路标识。

## 6. 脱敏

审计和日志都要避免记录敏感参数。框架侧的日志用参数级 `[Log(false)]` 控制：

```csharp
public void ChangePhone(long userId, string phone, [Log(false)] string idCardNumber) { /* ... */ }
```

被标记的参数在框架日志里显示为 `*`。但要注意：`ServiceInvokeContext.Arguments` 保存的是原始参数，不做掩码处理，订阅者写入 `Arguments` 时仍需自己判断。

审计表本身也要分类：手机号、证件号这类字段入库前做脱敏或加密（见[字段加密与脱敏存储](./04-field-encryption.md)）。

## 7. 批量场景的开销

批量写入会让事件逐条触发。1 万条批量插入意味着 1 万次 Before 和 1 万次 After 回调，如果每个回调都写一次数据库，审计本身会成为瓶颈。常见做法：

- 订阅者在内存里累积，按批落到审计表（注意请求结束时的 flush 时机）。
- 与业务写入共用同一条批量通道，用同一个 `SessionManager` 的批量写入。
- 只对关键实体开启逐条审计，其余实体只记数量与范围。

## 8. 常见误区

| 误区 | 后果 |
| --- | --- |
| 直接调 `IObjectDAO<T>` 写库 | 事件不触发，审计缺失 |
| 认为 After 等于“事务已提交” | 业务回滚后审计记录仍在 |
| 在事件里调用同一个服务的写方法 | 递归触发事件 |
| 订阅者注入 Singleton 服务缓存状态 | 跨请求串数据 |
| 审计写入与业务同库同事务却不接受回滚 | 审计与业务不一致 |

## 相关链接

- [返回目录](../README.md)
- [日志与诊断](../06-di/03-logging.md)
- [事务](../06-di/01-transactions.md)
- [数据权限与越权防护](./02-data-permission.md)
- [字段加密与脱敏存储](./04-field-encryption.md)
- [并发控制与读取路径](./06-concurrency-and-read-path.md)
