# 数据权限典型应用

按当前用户过滤查询结果只是数据权限的一半，另一半是调用方绕开列表、直接按主键操作时，由谁判断这次访问越权。下面五个场景覆盖查询过滤、范围写入、对象级校验和接口级兜底。

先明确三类落点，判断标准只有一条：这个值在编译期能不能确定。

| 落点 | 承载什么 | 什么时候用 |
| --- | --- | --- |
| 运行时 `Expr` | 当前用户、当前租户、接口参数决定的过滤条件 | 默认选择，覆盖列表、统计、导出 |
| `GenericSqlExpr` 构件 | 需要复用、不想层层传参的过滤规则 | 同一套规则出现在多个入口 |
| `TableDefinition.ConstFilter` | 固定状态、固定分区、固定租户类型 | 模型层面恒定不变的规则 |

把当前登录用户写进 `ConstFilter`，结果是所有人都看到同一个用户的数据。固定切片的用法见[多租户隔离典型应用](./01-tenant-isolation.md)。

## 场景 1：列表、统计、导出只看到自己有权限的数据

**需求**：非管理员只能看到自己名下的 Orders，同一份过滤规则要同时作用在列表、总数、聚合和导出上。

**做法**：写一个条件拼装函数，所有入口共用：

```csharp
using static LiteOrm.Common.Expr;

public static class OrderScopes
{
    public static LogicExpr For(OrderQueryRequest request, ICurrentUser user)
    {
        var filter = (Prop(nameof(Order.Id)) > 0)
            & (Prop(nameof(Order.IsDeleted)) == false);

        if (!string.IsNullOrEmpty(request.Keyword))
            filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

        if (!user.IsAdmin)
            filter &= Prop(nameof(Order.OwnerId)) == user.Id;

        return filter;
    }
}
```

```csharp
var filter = OrderScopes.For(request, currentUser);

var page = await orderViewService.SearchAsync(
    From<OrderView>().Where(filter).OrderBy(Prop(nameof(Order.CreateTime)).Desc()).Section(0, 20));

var total = await orderViewService.CountAsync(filter);
var exists = await orderViewService.ExistsAsync(filter);
```

要点：

- 不要先按业务条件查出结果，再在内存里 `Where` 一遍。分页总数会失真，聚合与导出接口会绕开过滤，不该读的数据也已经进入应用进程。
- 导出接口最容易被漏掉，它通常有自己的查询方法。把它接到同一个拼装函数上，而不是复制一份条件。
- 管理员分支不要做成“无过滤”的独立重载并全局复用，普通请求也可能走到那个重载。用同一个函数按角色拼条件，过滤入口只有一个。

## 场景 2：批量更新与批量删除也要带范围

**需求**：运营后台支持“把某批订单整批取消”，范围限制不能只写在列表上。

**做法**：`UpdateAll` 接收 `UpdateExpr`，`DeleteAll` 接收条件表达式，范围条件与业务条件写在同一个 `Where` 里：

```csharp
using static LiteOrm.Common.Expr;

var updateExpr = new UpdateExpr
{
    Table = new TableExpr(typeof(Order)),
    Sets = new List<SetItem> { new(Prop(nameof(Order.State)), Const(OrderState.Cancelled)) },
    Where = Expr.Lambda<Order>(o => o.OwnerId == userId && o.State == OrderState.Pending)
};

var affected = orderService.UpdateAll(updateExpr);

var deleted = orderService.DeleteAll(Expr.Lambda<Order>(o => o.OwnerId == userId && o.CreateTime < deadline));
```

要点：

- `DeleteAll` 的条件里不带范围，删除就会越界，而且没有任何地方会报错。
- 查询路径上，`GenericSqlExpr` 构件返回空片段时会被忽略，连 `WHERE` 关键字都不输出。写入路径没有这个保证：`UPDATE` / `DELETE` 的 `WHERE` 是无条件写出的，构件返回空片段会留下一个悬空的 `WHERE`，语句直接执行失败。所以给写入入口传范围条件时，条件必须恒定存在。
- 一句话记：查询条件可以空，写入条件不能空。
- 单条 `Update(entity)` / `Delete(entity)` 不经过条件拼装，属于下一个场景。

## 场景 3：详情、修改、删除按主键访问时的对象级校验

**需求**：列表已经过滤过，但详情接口拿到的只是一个主键，任何登录用户改一下 URL 参数就能读到别人的数据。

**做法**：拿到对象之后单独判断归属：

```csharp
public async Task<Order> GetOrderAsync(long id, ICurrentUser user, CancellationToken cancellationToken = default)
{
    var order = await orderViewService.GetObjectAsync(id, cancellationToken: cancellationToken);
    if (order is null) throw new NotFoundException();
    if (!user.IsAdmin && order.OwnerId != user.Id) throw new ForbiddenException();
    return order;
}
```

要点：

- 把“不存在”和“无权访问”分开（`404` 与 `403`），前端才能给出正确提示。如果业务上不希望暴露资源是否存在，就统一返回 `404`，但要在所有入口保持一致。
- 修改与删除走同一套判断。更稳的写法是让写操作也带上范围条件（场景 2），把校验下沉到 SQL 里。
- 校验要读主库。从只读副本读到的是滞后数据，会误判归属，见[并发控制与读写分离典型应用](./06-concurrency-and-read-write-splitting.md)。

## 场景 4：把范围规则做成可复用构件

**需求**：同一套范围规则已经出现在三个以上入口，形参传递开始出错。

**做法**：注册成 `GenericSqlExpr`，规则内部自己取上下文：

```csharp
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("OwnerScope", (context, sqlBuilder, outputParams, _) =>
{
    var user = UserContext.Current ?? throw new InvalidOperationException("User not resolved.");
    if (user.IsAdmin) return null;   // 空片段会被忽略，不会留下多余的 AND / WHERE

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), user.Id));
    return $"{sqlBuilder.ToSqlName(nameof(Order.OwnerId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

```csharp
var filter = OrderScopes.For(request) & Expr.Sql("OwnerScope");
```

要点：

- 构件返回的 SQL 走参数化，值不拼进文本。安全边界见[安全性](../03-advanced-topics/08-security.md)。
- 管理员分支返回 `null` 而不是 `"1 = 1"`。条件组合处会把已经写入的 `" AND "` 一起回滚，`SELECT` 语句在没有任何条件时连 `WHERE` 都不输出。
- 拼装函数声明成返回 `LogicExpr`，`&` 才能和 `Expr.Sql(...)` 直接拼。左侧是 `Expr` 时会编译不过。
- 同一个构件不要在写入语句上依赖空片段语义，原因见场景 2。

## 场景 5：接口级角色控制

**需求**：某些服务方法只允许特定角色调用，希望像 `[Authorize]` 一样声明在方法上。

**做法**：`[ServicePermission]` 声明元数据，自己实现校验。声明部分：

```csharp
public class OrderService : EntityService<Order>, IOrderService
{
    public OrderService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    [ServicePermission(AllowRoles = "Admin,Operator")]
    public void CancelAll(long tenantId) { /* ... */ }

    [ServicePermission(AllowAnonymous = true)]
    public decimal GetPublicPrice(long id) { /* ... */ }

    [ServicePermission(AllowRoles = "Admin")]
    public void ResetOwner(long orderId, [Log(false)] string reason) { /* ... */ }
}
```

校验部分挂在服务调用事件上：

```csharp
public sealed class RoleCheckEvent : IServiceInvokingEvent
{
    private readonly ICurrentUser _currentUser;

    public RoleCheckEvent(ICurrentUser currentUser) => _currentUser = currentUser;

    public void OnInvoking(ServiceInvokeContext context)
    {
        var description = new ServiceDescription();
        description.LoadFrom(context.Method);

        if (description.AllowAnonymous) return;

        var roles = description.AllowRoles;
        if (roles is null || roles.Length == 0) return;

        foreach (var role in roles)
        {
            if (_currentUser.IsInRole(role)) return;
        }

        throw new UnauthorizedAccessException($"Role required: {string.Join(",", roles)}.");
    }
}

services.AddScoped<IServiceInvokingEvent, RoleCheckEvent>();
```

要点：

- 特性本身只是元数据。框架会把它读进 `ServiceDescription`（`ServiceExt.LoadFrom`），但不会替你执行校验，`ServiceInvokeInterceptor` 只用描述信息做事务开关、日志级别和调用上下文。
- 嵌套调用不触发事件。`ServiceInvokeInterceptor` 内部用「调用进行中」标志防止重复处理，服务方法内部再调另一个服务方法时只复用外层事务，`OnInvoking` / `OnInvoked` 不再触发，所以业务入口处该检查的还是要检查。
- `ServiceInvokeContext.Arguments` 是原始参数，不做掩码。把 `Arguments` 写进日志会把密码、证件号一起落盘。要控制框架日志里的参数，用参数级 `[Log(false)]`，被标记的参数显示为 `*`。
- 事件里抛出的异常沿调用链上抛，交给上层框架或 `IServiceExceptionEvent` 处理，LiteOrm 只负责记录日志。

## 场景 6：确认哪些入口绕过了过滤

**需求**：上线前排查一遍“哪些写法不会经过数据范围过滤”。

| 路径 | 会不会经过数据范围过滤 | 处理方式 |
| --- | --- | --- |
| `SearchAsync` / `CountAsync` / `ExistsAsync` | 会，只要条件拼装统一 | 保持单一拼装函数 |
| `GetObjectAsync(id)` | 不会 | 加对象级校验 |
| `Update` / `Delete(entity)` | 不会 | 先查再改，或改用带条件的 `UpdateAll` / `DeleteAll` |
| `UpdateAll` / `DeleteAll(expr)` | 取决于传入的 `expr` | 表达式里带上范围条件 |
| 直接注入 `IObjectDAO<T>` 写库 | 不会 | 业务层统一走服务接口，DAO 只用于基础设施代码 |
| Remote 服务端代理 | 由服务端实现决定 | 服务端同样按上面的规则实现 |

`LiteOrm.Remote` 只是把服务调用转发到服务端，数据权限判断发生在服务端实现内部。客户端能做的是不把不该发的权限信息发出去，服务端能做的是不假设“请求来自内部系统就可信”。

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../06-di/02-permission-filtering.md)
- [多租户隔离典型应用](./01-tenant-isolation.md)
- [软删除与历史数据典型应用](./03-soft-delete-and-archive.md)
- [审计与变更追踪典型应用](./04-audit-and-change-tracking.md)
- [安全性](../03-advanced-topics/08-security.md)
