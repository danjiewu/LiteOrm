# Service Authorization

`[ServicePermission]` is LiteOrm's built-in method-level access control. It works at the service layer and decides whether a call is allowed at all; which rows that call returns afterwards is row-level filtering, a separate mechanism covered in [Data Permissions](../typical-applications/data-permission.en.md).

Authorization is enforced by `ServiceInvokeInterceptor` during AOP interception. A failed check throws `ServicePermissionException` and the method body never runs. This relies on the interface interception that `RegisterLiteOrm()` sets up; plain `AddLiteOrm()` and `LiteOrmContext` carry no AOP.

## 1. The rules

Four rules are evaluated in order, and the first match wins:

| Order | Condition | Result |
| --- | --- | --- |
| 1 | Neither the method nor its declaring type declares `[ServicePermission]` | Allowed, no checks at all |
| 2 | `AllowAnonymous = true` is declared | Allowed, no identity or role check (even if `AllowRoles` is also set) |
| 3 | No `IUserContext` is registered in the container | Allowed, no identity source means no role check |
| 4 | `IUserContext` is registered | An authenticated principal is required; if `AllowRoles` is declared, one of the roles must match |

Rule 3 is a compatibility design: applications that never wired up a user system keep their behavior after upgrading. Once `IUserContext` is registered, rule 4 takes over, and a missing principal counts as unauthenticated and is rejected.

Authentication status comes from `IPrincipal.Identity.IsAuthenticated`; role matching goes through `IPrincipal.IsInRole(role)`. `AllowRoles` is a comma-separated string; the framework splits on commas and trims each entry.

## 2. Declaring permissions

The attribute can sit on a method, a class, or an interface. A method-level declaration wins; the declaring type is consulted only when the method has none.

```csharp
public interface IOrderService
{
    // not declared: no checks
    Task<Order?> GetAsync(long id);

    // requires authentication, any role
    [ServicePermission]
    Task<Order> CreateAsync(Order order);

    // requires authentication and one of Admin or Manager
    [ServicePermission(AllowRoles = "Admin, Manager")]
    Task ApproveAsync(long id, bool approved);

    // allows anonymous access, no identity or role check
    [ServicePermission(true)]
    Task<HealthResult> HealthAsync();

    // role-restricted only: AllowAnonymous defaults to false, so authentication is implied
    [ServicePermission(AllowRoles = "Admin")]
    Task RebuildIndexAsync();
}
```

The three declarations differ as follows:

| Form | `AllowAnonymous` | Requires `IUserContext` | Behavior |
| --- | --- | --- | --- |
| `[ServicePermission]` | false | yes | With a registered context, requires authentication |
| `[ServicePermission(AllowRoles = "Admin")]` | false | yes | Authenticated plus role match |
| `[ServicePermission(true)]` | true | no | Allowed outright, role declarations ignored |

`AllowAnonymous` is the first positional parameter, so `[ServicePermission(true, AllowRoles = "Admin")]` compiles but the role part has no effect. Avoid this form; it misleads readers into thinking the role constrains anything.

## 3. Supplying the principal

The framework makes no assumption about where users come from. The application supplies the principal by implementing `IUserContext`:

```csharp
public interface IUserContext
{
    IPrincipal? UserPrincipal { get; }
}
```

A single member. Return `null` when no principal is available; the framework treats that as unauthenticated.

A web application reads the principal straight off the current request:

```csharp
using System.Security.Principal;

public sealed class HttpUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public IPrincipal? UserPrincipal => _accessor.HttpContext?.User;
}
```

Registering `IUserContext` works in several ways; pick one.

### 3.1 Through `RegisterLiteOrm` options

```csharp
builder.Services.AddHttpContextAccessor();

builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterUserContext<HttpUserContext>();          // generic, Scoped by default
});
```

Three overloads cover three sources:

```csharp
options.RegisterUserContext<HttpUserContext>();                          // generic + lifetime
options.RegisterUserContext(new FixedUserContext(principal));            // an instance (singleton)
options.RegisterUserContext(sp => new ScopedUserContext(sp));            // factory + lifetime
```

The second parameter of the generic and factory overloads is `Lifetime.Singleton / Scoped / Transient`, defaulting to `Scoped`. Constructor dependencies of the implementation type, such as `IHttpContextAccessor`, are injected by the container.

Calling it more than once stacks the registrations, and the most recently registered implementation is resolved first.

### 3.2 Registering directly in the container

The options are optional. Register `IUserContext` as an ordinary service and the interceptor resolves it through `IServiceProvider.GetService<IUserContext>()`:

```csharp
builder.Services.AddScoped<IUserContext, HttpUserContext>();
```

### 3.3 Choosing a lifetime

| Host shape | Recommendation |
| --- | --- |
| ASP.NET Core, per request | `Scoped` (the default); the principal changes per request |
| Background tasks / Worker | `Scoped`, with the principal taken from the task context; agree on a source when there is no request |
| Principal fixed for the process | `Singleton` or an instance registration |

If the principal source is `AsyncLocal` or request-scoped, do not wrap the accessor as `Singleton`; it may read another request's value.

## 4. Handling failures

A failed check throws `ServicePermissionException`, which derives from `ServiceException`.

```csharp
try
{
    await orderService.ApproveAsync(id, true);
}
catch (ServicePermissionException ex)
{
    // "Access to 'OrderService.ApproveAsync' requires one of the roles: Admin, Manager."
}
```

The message carries the service name, method name, and the missing roles. The interceptor logs it at `Warning`, ordinary exceptions at `Error`, so denied access stays out of error alerting.

At the web layer, `403` fits the semantics; `404` and `500` do not:

```csharp
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();
    if (feature?.Error is ServicePermissionException)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("The current user is not allowed to perform this operation.");
    }
}));
```

Returning `403` and `404` separately is what lets the frontend tell "not enough permission" from "record not found". A row-level filter that hides a record should still produce `404` or an empty result.

## 5. Prerequisites

Authorization rides on AOP interception, so several prerequisites must hold. Otherwise the attribute silently does nothing.

- The service type must actually be intercepted. Types carrying the `[Service]` attribute (`IsService = true`) get the interceptor applied during auto-registration; you can also declare `[Intercept(typeof(ServiceInvokeInterceptor))]` explicitly. Services registered by hand gain no interception.
- Calls must go through an interface. Castle generates interface proxies, so a hand-constructed implementation or resolving the concrete type yields an instance without interception. The same caveat appears in [Transactions](./transactions.en.md).
- The method must be `public` and either virtual or an interface method.
- The check runs once, on the outermost call. Nested calls (where the same interceptor is already in process) are not re-checked, so a method's permission declaration only governs calls made directly from outside.

## 6. Built-in interface permissions

The framework's read and write interfaces carry permission declarations of their own, which take effect as soon as `IUserContext` is wired up.

| Interface | Declaration | Meaning |
| --- | --- | --- |
| `IEntityViewService<T>` / `IEntityViewServiceAsync<T>` | `[ServicePermission(true)]` | Reads allow anonymous access |
| `IEntityService<T>` / `IEntityServiceAsync<T>` | `[ServicePermission(false)]` | Writes require authentication |

Once `IUserContext` is registered:

- Read operations on the generic view interfaces keep working and still pass.
- Write operations on the generic service interfaces start requiring an authenticated principal, throwing `ServicePermissionException` when there is none.

Existing applications hit this quickly: a background job or scheduled task that injects `IEntityService<T>` to write gets rejected when `IUserContext.UserPrincipal` returns `null` there. Three ways to handle it, chosen per scenario.

Option 1: supply a principal for those scenarios. A background job has no request context, so give it a fixed system principal:

```csharp
public sealed class SystemUserContext : IUserContext
{
    private static readonly IPrincipal _system =
        new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "system"), new Claim(ClaimTypes.Role, "System") },
            "System"));

    public IPrincipal? UserPrincipal => _system;
}
```

Option 2: switch the source by scenario. Use the request principal when there is a request and fall back to the system principal otherwise, branching inside one `IUserContext`:

```csharp
public sealed class AmbientUserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public AmbientUserContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public IPrincipal? UserPrincipal =>
        _accessor.HttpContext?.User ?? SystemPrincipal.Instance;
}
```

Option 3: allow those methods anonymously. When a write is genuinely public, such as sign-up or telemetry write-back, declare `[ServicePermission(true)]` on the custom service method. The framework's built-in write interfaces cannot be changed, so they rely on the first two options to supply a principal.

`ClaimsPrincipal.IsInRole` compares role claim values exactly and is **case-sensitive**, whereas `GenericPrincipal` and `WindowsPrincipal` are case-insensitive. Role names must match the claims in the principal precisely. The matching semantics belong to `IPrincipal.IsInRole`; the framework only passes the declared role name in.

## 7. Permissions on custom services

Custom services must satisfy the prerequisites in section 5 before declaring permissions:

```csharp
[Service]
public interface IUserService
{
    Task<User?> GetAsync(int id);

    [ServicePermission(AllowRoles = "Admin")]
    Task DeleteAsync(int id);
}

[AutoRegister(Lifetime = Lifetime.Scoped)]
[Service]
public sealed class UserService : EntityService<User>, IUserService
{
    public UserService(IServiceProvider serviceProvider) : base(serviceProvider) { }

    public Task<User?> GetAsync(int id) => GetObjectAsync(id);

    // read-only: no permission declared, allowed by rule 1 in section 1
}
```

Derived services do not redeclare inherited methods. The interceptor sees attributes declared on the interface and on the implementation method alike.

## 8. Common pitfalls

### 8.1 Treating method-level authorization as data isolation

`[ServicePermission]` only governs "can this be called", not "which data can be touched". An admin who passes the role check can still call `SearchAsync` and read the whole table. Row-level scope is separate work; see [Data Permissions](../typical-applications/data-permission.en.md) and [Permission Filtering and User Scopes](../advanced-topics/permission-filtering.en.md).

### 8.2 Combining `AllowAnonymous` with roles and expecting the roles to apply

`AllowAnonymous = true` is the highest-priority allowance; `AllowRoles` on the same attribute is not checked. To restrict by role, leave `AllowAnonymous` unset.

### 8.3 Registering `IUserContext` but expecting the old behavior

The compatibility allowance in rule 3 holds only while nothing is registered. Once registered, every method with a permission declaration and no `AllowAnonymous` starts requiring an authenticated principal, built-in write interfaces included.

### 8.4 Constructing service instances by hand

The interceptor only applies to interface proxies resolved from the container. Hand-constructed instances bypass it, and no permission declaration has any effect.

### 8.5 Using `ServicePermissionException` for missing data

It is an authorization exception, and its HTTP meaning is `403`. Missing data surfaces as an empty result or `404` from the respective service; mixing the two leaves the frontend unable to respond correctly.

## Related Links

- [Back to docs hub](../README.md)
- [Data Permissions](../typical-applications/data-permission.en.md)
- [Permission Filtering and User Scopes](../advanced-topics/permission-filtering.en.md)
- [Logging and Diagnostics](./logging.en.md)
- [Transactions](./transactions.en.md)
- [Security](../advanced-topics/security.en.md)