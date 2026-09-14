# 数据权限：查询过滤之外还要做什么

按当前用户过滤查询结果，只是数据权限的第一半。另一半是：当调用方绕开列表、直接按主键操作时，谁来判断这次访问是否越权。这一篇把数据权限的落点、可复用构件和兜底方式串起来。

## 1. 三类落点

| 落点 | 承载什么 | 什么时候用 |
| --- | --- | --- |
| 运行时 `Expr` | 当前用户、当前租户、接口参数决定的过滤条件 | 默认选择，覆盖列表、统计、导出 |
| `GenericSqlExpr` 构件 | 需要复用、不想层层传参的过滤规则 | 同一套规则出现在多个入口 |
| `TableDefinition.ConstFilter` | 固定状态、固定分区、固定租户类型 | 模型层面恒定不变的规则 |

判断标准只有一条：这个值在编译期能不能确定。能，走 `ConstFilter`；不能，走运行时条件。把当前登录用户写进 `ConstFilter`，结果是所有人都看到同一个用户的数据。

## 2. 条件必须在查询入口拼装

推荐把“业务条件 + 软删除 + 数据范围”放在同一个函数里，再由列表、统计、导出共用：

```csharp
using static LiteOrm.Common.Expr;

private Expr BuildOrderFilter(OrderQueryRequest request, ICurrentUser user)
{
    var filter = (Prop(nameof(Order.Id)) > 0)
        & (Prop(nameof(Order.IsDeleted)) == false);

    if (!string.IsNullOrEmpty(request.Keyword))
        filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

    if (!user.IsAdmin)
        filter &= Prop(nameof(Order.OwnerId)) == user.Id;

    return filter;
}
```

不推荐的做法是先按条件查出结果，再在内存里 `Where` 一遍。问题有三个：`Count` 与分页总数不匹配；聚合、统计、导出接口绕过过滤；不该读的数据已经进入应用进程。

同样的规则要覆盖写入侧的范围操作。`UpdateAll` / `DeleteAll` 接收的是 `LogicExpr` 或 `UpdateExpr`，如果这些入口只带主键或业务条件，就把数据范围漏掉了：

```csharp
var updateExpr = new UpdateExpr
{
    Table = new TableExpr(typeof(Order)),
    Sets = new List<SetItem> { new(Expr.Prop(nameof(Order.State)), Expr.Const(OrderState.Cancelled)) },
    Where = Expr.Lambda<Order>(o => o.OwnerId == user.Id && o.State == OrderState.Pending)
};

orderService.UpdateAll(updateExpr);
```

注意 `Where` 里的范围条件与业务条件同时写在这里。`DeleteAll(Expr.Lambda<Order>(...))` 同理，条件里不带范围，删除就会越界。

## 3. 单条对象必须单独校验

列表过滤不能替代对象级访问控制。详情、修改、删除这三个入口，拿到的都是主键：

```csharp
public async Task<Order> GetOrderAsync(long id, ICurrentUser user)
{
    var order = await _orderViewService.GetObjectAsync(id);
    if (order is null) throw new NotFoundException();
    if (!user.IsAdmin && order.OwnerId != user.Id) throw new ForbiddenException();
    return order;
}
```

把“不存在”和“无权访问”区分开（`404` 与 `403`），前端才能给出正确提示。如果业务上不希望暴露资源是否存在，那就统一返回 `404`，这是一个业务决策，但要在所有入口保持一致。

## 4. 复用它：把范围条件做成构件

规则一旦出现在三个以上入口，就值得做成 `GenericSqlExpr`：

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

需要走参数化而不是字符串拼接，安全边界见[安全性](../03-advanced-topics/08-security.md)。更完整的选型对比见[权限过滤与用户范围控制](../06-di/02-permission-filtering.md)。

### 空片段会被忽略，但只限查询路径

构件返回 `string.Empty` 或 `null` 时，这个片段不产生任何 SQL。条件组合处会把已经写入的 `" AND "` / `" OR "` 一起回滚，`SELECT` 语句在没有任何条件时连 `WHERE` 关键字都不输出。所以查询条件不需要 `"1 = 1"` 这类恒真占位来凑语法，管理员分支直接返回空串即可。

写入路径没有这个保证。`UpdateAll` / `DeleteAll` 生成的 `UPDATE` / `DELETE` 语句，`WHERE` 关键字是无条件写出的，构件返回空片段会留下一个悬空的 `WHERE`，语句执行失败。给这两个入口传范围条件时，条件必须恒定存在，或者在调用点判断后再决定是否带上（也就是第 2 节的做法）。

一句话记：查询条件可以空，写入条件不能空。

## 5. 服务层兜底：`[ServicePermission]` 提供元数据，校验要自己接

`LiteOrm` 提供 `[ServicePermission]` 特性：

```csharp
public class OrderService : EntityService<Order>, IOrderService
{
    [ServicePermission(AllowRoles = "Admin,Operator")]
    public void CancelAll(long tenantId) { /* ... */ }

    [ServicePermission(AllowAnonymous = true)]
    public decimal GetPublicPrice(long id) { /* ... */ }
}
```

这个特性的两个属性是 `AllowAnonymous` 与 `AllowRoles`（逗号分隔的角色名）。框架会把它们读进 `ServiceDescription`（`ServiceExt.LoadFrom`），但**不会替你执行校验**：`ServiceInvokeInterceptor` 读取描述信息用于事务开关、日志级别和调用上下文，权限字段只作为描述存在。

要真正拦住调用，需要自己接一层。最省事的位置是 `IServiceInvokingEvent`，它在方法执行前触发，抛异常即可中断调用：

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

三个注意点：

- **嵌套调用不会触发事件。** `ServiceInvokeInterceptor` 内部用「调用进行中」标志防止重复处理，服务方法内部再调用另一个服务方法时，只会复用外层的事务，`OnInvoking` / `OnInvoked` 不再触发。所以权限校验不能只挂在事件上，业务入口处该检查的还是要检查。
- **`ServiceInvokeContext.Arguments` 是原始参数，不做掩码。** 在订阅者里把 `Arguments` 写进日志会把密码、证件号一并落盘。要控制日志里的参数，用参数级 `[Log(false)]`，被标记的参数在框架日志中显示为 `*`：

  ```csharp
  [ServicePermission(AllowRoles = "Admin")]
  public void ResetPassword(long userId, [Log(false)] string newPassword) { /* ... */ }
  ```
- **异常会原样抛出。** 权限事件抛出的异常沿调用链上抛，交给上层框架（或 `IServiceExceptionEvent`）处理，LiteOrm 只负责记录日志。

## 6. 常见的绕过路径

| 路径 | 会不会经过数据范围过滤 | 处理方式 |
| --- | --- | --- |
| `SearchAsync` / `CountAsync` | 会，只要条件拼装统一 | 保持单一拼装函数 |
| `GetObjectAsync(id)` | 不会 | 加对象级校验 |
| `Update` / `Delete(entity)` | 不会 | 先查再改，或改用带条件的 `UpdateAll` / `DeleteAll` |
| `UpdateAll` / `DeleteAll(expr)` | 取决于传入的 `expr` | 表达式里带上范围条件 |
| 直接注入 `IObjectDAO<T>` 写库 | 不会 | 业务层统一走服务接口，DAO 只用于基础设施代码 |
| Remote 服务端代理 | 由服务端实现决定 | 服务端同样按上面的规则实现 |

最后一行值得单独说：`LiteOrm.Remote` 只是把服务调用转发到服务端，数据权限判断发生在服务端实现内部。客户端能做的是不把不该发的权限信息发出去，服务端能做的是不假设“请求来自内部系统就可信”。

## 7. 常见误区

| 误区 | 后果 |
| --- | --- |
| 只过滤列表 | 主键可直接访问他人数据 |
| 认为 `[ServicePermission]` 会自动拦截 | 属性只是元数据，调用照常执行 |
| 把权限校验只放在 `IServiceInvokingEvent` | 嵌套调用不触发事件 |
| 在订阅者里记录 `Arguments` | 敏感参数进入日志 |
| 给管理员开一个“无过滤”重载并全局复用 | 普通请求也可能走到这个重载 |

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../06-di/02-permission-filtering.md)
- [多租户隔离的三种落地层次](./01-multi-tenancy.md)
- [审计与变更追踪](./03-audit-trail.md)
- [日志与诊断](../06-di/03-logging.md)
- [安全性](../03-advanced-topics/08-security.md)
