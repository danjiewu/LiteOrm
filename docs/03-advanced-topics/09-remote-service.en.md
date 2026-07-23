# Remote Service (LiteOrm.Remote)

LiteOrm provides a complete remote service invocation solution, allowing business code to switch seamlessly between "local calls" and "remote calls" — **the interface definition stays the same, the call syntax stays the same**. Only the registration method needs to change to physically decouple the data access layer from the application process.

## 1. Overview

### 1.1 What Problem It Solves

In traditional monolithic applications, the data access layer and the application layer run within the same process, with database connection strings directly exposed in configuration files:

- Anyone with access to the application server can reach the database
- The frontend web project is tightly coupled with the database and cannot be independently deployed or scaled
- When multiple clients (Web, mobile, desktop) share the same codebase, database access logic cannot be reused

LiteOrm.Remote achieves physical separation of frontend and backend through **remote service proxies**:

```mermaid
graph TB
    subgraph frontend["Frontend Layer (Web / Desktop / Mobile)"]
        A1["Business Code"] --> A2["Remote Service Proxy"]
        B1["Business Code"] --> B2["Remote Service Proxy"]
    end
    subgraph backend["Backend Data Service Layer"]
        C["API Gateway / Data Service"]
        D["LiteOrm Local Service"]
        E["Database"]
    end
    A2 -->|HTTP / JSON| C
    B2 -->|HTTP / JSON| C
    C --> D --> E
```

| Value | Description |
|-------|-------------|
| **Database not exposed** | Connection strings exist only in the backend data service layer; the frontend layer cannot directly access the database |
| **Security isolation** | The frontend layer can only access data through controlled service interfaces; all queries pass through ExprValidator |
| **Multi-client reuse** | Web, desktop, and mobile share the same set of service interfaces; backend logic is maintained centrally |
| **Independent deployment** | The frontend and backend layers can be scaled and updated independently without affecting each other |
| **Interface unchanged** | Business code requires no changes — the local call `userService.InsertAsync(user)` and the remote call are written identically |

> **Compared to traditional approaches**: In traditional approaches, if both a web frontend and a desktop client need to access the database, you either maintain separate sets of data access code (redundant and error-prone) or manually wrap REST APIs (requiring extra Controllers and DTO mappings). LiteOrm.Remote makes the service interface definition itself the API protocol, eliminating the need for an additional wrapper layer.

### 1.2 Two NuGet Packages

| Package | Role | Description |
|---------|------|-------------|
| `LiteOrm.Remote` | Client | Generates dynamic proxies to intercept method calls and forward them to the server via HTTP |
| `LiteOrm.Remote.Server` | Server | Receives HTTP requests, parses them, resolves service instances from the DI container, and executes them |

Both ends share DTOs in `LiteOrm.Common` (`RemoteInvocationRequest` / `RemoteInvocationResponse`), ensuring protocol consistency.

---

## 2. Quick Start

Set up a runnable remote service invocation in 5 minutes. Both ends share the same service interface definition (typically placed in a separate `Contracts` class library).

### 2.1 Define the Service Interface

```csharp
using LiteOrm;
using LiteOrm.Service;

[Service]                                              // Mark as remote service
public interface IDemoUserService : IEntityServiceAsync<DemoUser>
{
    Task<DemoUser> GetByUserNameAsync(string userName);
}
```

### 2.2 Server

```bash
dotnet add package LiteOrm.Remote.Server
```

```csharp
using LiteOrm.Remote.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();        // Register LiteOrm main framework (with local service implementations)
builder.Services.AddRemoteServer();    // Register remote server

var app = builder.Build();
app.MapRemoteInvokeEndpoint();         // Map remote invocation endpoint
app.Run();
```

### 2.3 Client

```bash
dotnet add package LiteOrm.Remote
```

```csharp
using LiteOrm.Remote;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
    })
    .Build();
```

### 2.4 Calling — Identical to Local Services

```csharp
using var scope = host.Services.CreateScope();
var userService = scope.ServiceProvider.GetRequiredService<IDemoUserService>();

var user = new DemoUser { UserName = "alice" };
await userService.InsertAsync(user);          // Id auto-write-back
Console.WriteLine($"New user Id = {user.Id}");

var loaded = await userService.GetByUserNameAsync("alice");
Console.WriteLine($"Loaded user: {loaded.UserName}");
```

> `AutoRegisterEntityServices` defaults to `true`. The framework automatically scans interfaces marked with `[Service]` and registers them as remote proxies — no manual registration needed.

---

## 3. Defining and Calling Services

### 3.1 `[Service]` Attribute: Declaring Remote Service Interfaces

```csharp
[Service]                                        // Expose as remote service, auto-register name mapping
public interface IDemoUserService : IEntityServiceAsync<DemoUser>
{
}

[Service(Name = "UserSvc")]                      // Custom service name
public interface IUserService
{
}

[Service(IsService = false)]                     // Explicitly disable remote invocation
public interface IInternalService
{
}
```

### 3.2 `[ServiceMethod]` Attribute: Method Alias

```csharp
public interface IUserService
{
    [ServiceMethod(MethodName = "FindByAccount")]
    Task<User> GetByUserNameAsync(string userName);
}
```

When not specified, `MethodInfo.Name` is used as the method key.

### 3.3 Common Calling Patterns

#### Queries

```csharp
// Query by primary key
var user = await userService.GetObjectAsync(1);

// Lambda condition query
var admins = await userService.SearchAsync(u => u.Role == "Admin");

// Custom method
var user = await userService.GetByUserNameAsync("alice");

// Existence check and count
bool exists = await userService.ExistsAsync(u => u.UserName == "alice");
int count = await userService.CountAsync(u => u.Role == "Admin");
```

#### Writes

```csharp
// Insert (auto-increment Id auto-write-back)
var user = new User { UserName = "alice", Role = "Admin" };
await userService.InsertAsync(user);

// Update
user.DisplayName = "Alice Updated";
await userService.UpdateAsync(user);

// Batch insert (auto-increment Id per-item write-back)
var orders = new List<Order> { /* ... */ };
await orderService.BatchInsertAsync(orders);

// Update if exists, insert otherwise
await departmentService.UpdateOrInsertAsync(dept);

// Delete by condition
int deleted = await userService.DeleteAsync(u => u.UserName == "alice");
```

> Lambda condition queries are written identically to local services. The framework converts Lambda expressions to serializable `Expr` expression trees in the client process before transmission. See [Expression Guide](../02-core-usage/06-expr-guide.md).

---

## 4. Configuration

### 4.1 Server Configuration (`RemoteServerOptions`)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `InvokePath` | `string` | `"api/remote/invoke"` | Remote invocation HTTP endpoint path |
| `ConnectPath` | `string` | `"api/remote/connect"` | HTTP endpoint path for establishing an authenticated session |
| `EnableAuthentication` | `bool` | `true` | Enables Cookie authentication. When enabled, the Connect endpoint creates an identity ticket via `HttpContext.SignInAsync`; subsequent requests restore user context via `HttpContext.User` |
| `JsonSerializerOptions` | `JsonSerializerOptions` | `UnsafeRelaxedJsonEscaping` + case-insensitive | JSON serialization options |
| `ServiceTypeResolver` | `IRemoteServiceTypeResolver` | `DefaultServiceTypeResolver` | Service type resolver instance |
| `ServiceTypeResolverFactory` | `Func<IServiceProvider, IRemoteServiceTypeResolver>?` | `null` | Resolver factory, takes precedence over `ServiceTypeResolver` |
| `AutoRegisterEntityServices` | `bool` | `true` | Auto-scan interfaces with `[Service]` attribute |
| `Assemblies` | `Assembly[]?` | `null` | Scan assembly list; scans all referenced assemblies if not set |

### 4.2 Client Configuration (`LiteOrmOptions`)

| Property | Type | Description |
|----------|------|-------------|
| `RemoteServiceUri` | `Uri?` | Remote service base address. When set, automatically registers `HttpRemoteServiceTransport` based on `HttpClient` |
| `RemoteServicePath` | `string` | Request path relative to `RemoteServiceUri`, default `api/remote/invoke` |
| `RemoteConnectPath` | `string` | Connect path relative to `RemoteServiceUri`, default `api/remote/connect` |
| `Credentials` | `RemoteCredentials?` | Remote invocation credentials. Only used when `CredentialsMode = SingleCredential` (default). The caller must manually call `ConnectAsync(credentials)` once; the cookie returned by the server is cached, and subsequent `InvokeAsync` calls automatically reuse it |
| `CredentialsMode` | `RemoteCredentialsMode` | Credential mode, default `SingleCredential`. Set to `Dynamic` to resolve credentials per-session via `CredentialsResolver`. The transport caches each session's cookie by credential key, so multiple users do not interfere with each other |
| `CredentialsResolver` | `Func<IServiceProvider, RemoteCredentials?>?` | Dynamic credential resolver. Only effective in `Dynamic` mode. Receives the DI Scope's `IServiceProvider` and returns the `RemoteCredentials` for that session; returning null means anonymous connection (Invoke without Cookie) |
| `ConfigureHttpClient` | `Action<HttpClient>?` | Configure the internal `HttpClient` (timeout, default headers, etc.) |
| `Transport` | `IRemoteServiceTransport?` | Custom transport layer instance. Takes precedence over `RemoteServiceUri` when set |
| `AutoRegisterEntityServices` | `bool` | Whether to auto-register all entity services as remote proxies, default `true` |
| `Assemblies` | `Assembly[]?` | Custom interface scan assembly list; scans all referenced assemblies if not set |

> **Required**: At least one of `Transport` or `RemoteServiceUri` must be set, otherwise `InvalidOperationException` is thrown during registration.

#### HTTP client tuning example

```csharp
opts.RemoteServiceUri = new Uri("http://localhost:5000");
opts.RemoteServicePath = "api/remote/invoke";
opts.ConfigureHttpClient = client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("X-Api-Key", "...");
};
```

### 4.3 `AutoRegisterEntityServices` Auto-Registration

Both server and client provide this setting, defaulting to `true`. The framework automatically scans interfaces marked with `[Service]` (and `IsService == true`):

- **Client**: Registers interfaces as remote proxies (Castle DynamicProxy), forwarding all method calls to the remote server
- **Server**: Registers name mappings, ensuring ServiceName consistency between both ends

**Registration rules**:
- If `[Service(Name = "CustomName")]` sets `Name`, that name is used
- Otherwise, the short name generated by `TypeResolverHelper.GetName(type)` is used (e.g. `IDemoUserService`, `IEntityServiceAsync<DemoUser>`)

### 4.4 Manual Registration and Factory Pattern

`AddRemoteService<TService>()` registers any service interface as a remote proxy. It does **not depend on `AutoRegisterEntityServices`** and can be used standalone or coexist with it (manual registration takes priority; auto-scan skips already registered interfaces):

```csharp
// Standalone: register one by one
services.AddRemoteService<IUserService>()
        .AddRemoteService<IOrderService>();

// Or coexist with AutoRegisterEntityServices
services.AddRemoteService<ISpecialService>();
```

| Registration Method | Applicable Scenario | Detection Method |
|---------------------|---------------------|------------------|
| `AutoRegisterEntityServices` | Auto-scan interfaces with `[Service]` attribute | `[Service]` attribute |
| `AddRemoteService<TService>()` | Manually register any service interface | Explicit type specification |
| `AddRemoteServiceGenerator<TFactory>()` | Aggregate multiple services through a factory | Auto-scan factory return types |

#### Factory Pattern

Define a factory interface aggregating multiple business services, register once via `AddRemoteServiceGenerator`:

```csharp
public interface RemoteServiceFactory
{
    IDemoUserService DemoUserService { get; }
    IDemoOrderService DemoOrderService { get; }
    IDemoDepartmentService DemoDepartmentService { get; }
}

services.AddRemoteServiceGenerator<RemoteServiceFactory>();

var factory = scope.ServiceProvider.GetRequiredService<RemoteServiceFactory>();
var user = await factory.DemoUserService.GetByUserNameAsync("alice");
```

---

## 5. Authentication

LiteOrm.Remote uses ASP.NET Core Cookie authentication for user identity, replacing the traditional SessionID mechanism. Two grant types are supported:

| Grant Type | `AuthGrantType` | Required Fields | Use Case |
|------------|----------------|-----------------|----------|
| Password | `Password` (default) | `Username` + `Password` | Authenticating user identity with explicit credentials |
| Client Credentials | `ClientCredentials` | `ClientId` + `ClientSecret` | Authenticating client/application identity without a specific user (e.g., service-to-service calls) |

### 5.1 How It Works

```mermaid
sequenceDiagram
    participant Client as Client
    participant Server as Server
    participant Store as IRemoteAuthenticationHandler

    Client->>Server: POST /api/remote/connect {GrantType, Username/ClientId, Password/ClientSecret, Extensions}
    Server->>Server: Validate required fields by GrantType
    alt Missing fields
        Server-->>Client: 401 Unauthorized (indicates missing field)
    else Fields complete
        Server->>Store: ValidateCredentialsAsync(credentials)
        Store-->>Server: ClaimsPrincipal (valid) / null (invalid)
        alt Validation failed
            Server-->>Client: 401 Unauthorized
        else Validation passed
            Server->>Server: HttpContext.SignInAsync(principal) → set Cookie
            Server-->>Client: 200 OK (Cookie auto-written)
        end
    end
    Note over Client,Server: Subsequent requests carry Cookie automatically
    Client->>Server: POST /api/remote/invoke (with Cookie)
    Server->>Server: Auth middleware restores HttpContext.User
    Server->>Server: Execute remote invocation
    Server-->>Client: RemoteInvocationResponse
```

### 5.2 Implementing `IRemoteAuthenticationHandler`

`IRemoteAuthenticationHandler.ValidateCredentialsAsync` receives `RemoteCredentials`. Implementers can branch on `GrantType` to execute different validation logic. The framework has already validated required fields by grant type before calling this method.

**Password grant type example:**

```csharp
using System.Security.Claims;
using LiteOrm.Common;
using LiteOrm.Remote.Server;

public class PasswordAuthHandler : IRemoteAuthenticationHandler
{
    public Task<ClaimsPrincipal?> ValidateCredentialsAsync(
        RemoteCredentials credentials, CancellationToken cancellationToken = default)
    {
        // Password grant: validate username/password
        if (credentials.GrantType == AuthGrantType.Password
            && credentials.Username == "admin"
            && credentials.Password == "pass")
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, credentials.Username),
                new Claim("tenant_id", credentials.Extensions?["tenant"]?.ToString() ?? ""),
            }, "Cookies");
            return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
        }
        return Task.FromResult<ClaimsPrincipal?>(null);
    }
}
```

**Client credentials grant type example:**

```csharp
public class ClientCredentialsAuthHandler : IRemoteAuthenticationHandler
{
    public Task<ClaimsPrincipal?> ValidateCredentialsAsync(
        RemoteCredentials credentials, CancellationToken cancellationToken = default)
    {
        // ClientCredentials grant: validate ClientId/ClientSecret
        if (credentials.GrantType == AuthGrantType.ClientCredentials
            && credentials.ClientId == "my-app"
            && credentials.ClientSecret == "secret-key")
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, credentials.ClientId!),
                new Claim("client_id", credentials.ClientId!),
            }, "Cookies");
            return Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(identity));
        }
        return Task.FromResult<ClaimsPrincipal?>(null);
    }
}
```

**Supporting both grant types:**

```csharp
public class MixedAuthHandler : IRemoteAuthenticationHandler
{
    public Task<ClaimsPrincipal?> ValidateCredentialsAsync(
        RemoteCredentials credentials, CancellationToken cancellationToken = default)
    {
        return credentials.GrantType switch
        {
            AuthGrantType.Password => ValidatePasswordAsync(credentials),
            AuthGrantType.ClientCredentials => ValidateClientCredentialsAsync(credentials),
            _ => Task.FromResult<ClaimsPrincipal?>(null),
        };
    }

    private Task<ClaimsPrincipal?> ValidatePasswordAsync(RemoteCredentials credentials)
    {
        // Validate username/password (query user database, etc.)
        // ...
    }

    private Task<ClaimsPrincipal?> ValidateClientCredentialsAsync(RemoteCredentials credentials)
    {
        // Validate ClientId/ClientSecret (query app registry, etc.)
        // ...
    }
}
```

### 5.3 Registering the Server

```csharp
builder.Services.AddSingleton<IRemoteAuthenticationHandler, MyAuthHandler>();
builder.Services.AddRemoteServer();
```

> When no `IRemoteAuthenticationHandler` is registered, the server allows anonymous connections (no identity verification).

### 5.4 Client Credentials Configuration

**Password grant type (default):**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
        opts.Credentials = new RemoteCredentials
        {
            GrantType = AuthGrantType.Password,  // default, can be omitted
            Username = "admin",
            Password = "pass",
            Extensions = new Dictionary<string, string>
            {
                ["tenant"] = "tenant-a",
            },
        };
    })
    .Build();
```

**Client credentials grant type:**

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrmRemote(opts =>
    {
        opts.RemoteServiceUri = new Uri("http://localhost:5000");
        opts.Credentials = new RemoteCredentials
        {
            GrantType = AuthGrantType.ClientCredentials,
            ClientId = "my-app",
            ClientSecret = "secret-key",
        };
    })
    .Build();
```

After setting `Credentials`, the caller must manually call `ConnectAsync` once to complete authentication: the server validates the credentials and returns a cookie, which the transport caches by credential key. All subsequent `InvokeAsync` calls automatically reuse that cookie without re-authenticating. If no credentials are set or `ConnectAsync` is not called, Invoke proceeds as anonymous.

#### Manual Connect Trigger

The framework does not auto-issue Connect. The caller must explicitly call `ConnectAsync` to establish an authenticated session. On success, the cookie is cached and subsequent Invoke calls reuse it:

```csharp
using var scope = host.Services.CreateScope();
var transport = scope.ServiceProvider.GetRequiredService<IRemoteServiceTransport>();

try
{
    // Actively establish an authenticated connection; throws RemoteTransportException immediately on invalid credentials
    // On success, the cookie returned by the server is cached and all subsequent Invoke calls automatically reuse it
    await transport.ConnectAsync(new RemoteCredentials
    {
        GrantType = AuthGrantType.ClientCredentials,
        ClientId = "my-app",
        ClientSecret = "secret-key",
    });
    Console.WriteLine("Connected");
}
catch (RemoteTransportException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
}
```

> After a successful manual `ConnectAsync`, subsequent `InvokeAsync` calls will not repeat the Connect request; calling `ConnectAsync` again with new credentials switches identity.

### 5.5 Dynamic Credentials Mode (Multi-User Session Isolation)

`SingleCredential` mode shares one credential and one cookie across the entire process, which cannot distinguish multiple users. When a web backend needs to call the remote data service with the identity of "the end user of the current request" (i.e., BFF / gateway forwarding scenario), switch to `Dynamic` mode:

- `IRemoteServiceTransport` is registered as **Singleton**, with a single shared `HttpClient` (`UseCookies` disabled)
- `HttpRemoteServiceTransport` uses an internal `ConcurrentDictionary<string, string>` to **cache each session's cookie by credential key**, so multiple users do not interfere with each other
- On `InvokeAsync`, `CredentialsResolver` resolves the current session's credentials and the cached cookie for that key is written to the `Cookie` request header; on cache miss, an anonymous (cookie-less) request is sent
- The caller must call `ConnectAsync` once at the start of the session (e.g., the BFF login flow) to establish the cookie; all subsequent `InvokeAsync` calls in that session reuse it, **avoiding repeated authentication on every request**

```mermaid
graph LR
    Browser["Browser (stores username/password cookie)"] -->|HTTP request with cookie| Web["Web Backend (BFF)"]
    Web -->|Login flow calls ConnectAsync once| Transport["HttpRemoteServiceTransport (Singleton)"]
    Transport -->|Caches cookie by credential key| Cache["Internal _dynamicCookies dictionary"]
    Web -->|Subsequent business calls| Transport
    Transport -->|InvokeAsync auto-writes Cookie header| Remote["Remote Data Service"]
```

#### Configuration

```csharp
builder.Services.AddHttpContextAccessor(); // required

builder.Host.RegisterLiteOrmRemote(opts =>
{
    opts.RemoteServiceUri = new Uri("http://localhost:5000");
    opts.CredentialsMode = RemoteCredentialsMode.Dynamic;
    opts.CredentialsResolver = sp =>
    {
        var httpCtx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var request = httpCtx?.Request;
        if (request is null || !request.Cookies.TryGetValue("username", out var user) || user is null
            || !request.Cookies.TryGetValue("password", out var pwd) || pwd is null)
        {
            return null; // anonymous connection
        }
        return new RemoteCredentials
        {
            GrantType = AuthGrantType.Password,
            Username = user,
            Password = pwd,
        };
    };
});
```

> - `Credentials` is ignored in `Dynamic` mode; only the return value of `CredentialsResolver` is used
> - Returning `null` means anonymous connection; Invoke will not carry a Cookie
> - The caller must explicitly call `ConnectAsync` (with the user's credentials) at the start of the session to establish the cookie; all subsequent `InvokeAsync` calls in that session reuse the cached cookie without re-connecting

#### Web Application Example: Store Credentials in Cookie and Forward

Below is a complete example of an ASP.NET Core web backend acting as a BFF to forward LiteOrm remote calls. The end user's credentials are stored in a browser cookie, and the BFF extracts them on each request and forwards them to the remote data service.

**1. Login endpoint: write credentials to the client cookie, and trigger a Connect to cache the server cookie**

```csharp
app.MapPost("/api/login", async (HttpContext ctx, LoginDto dto, IRemoteServiceTransport transport) =>
{
    // In real projects, validate username/password by querying a database / external auth service
    if (dto.Username != "admin" || dto.Password != "pass")
    {
        ctx.Response.StatusCode = 401;
        return;
    }

    // 1. Issue a Connect to the remote data service with the user's credentials once;
    //    the cookie returned by the server is cached inside the transport.
    //    All subsequent Invoke calls for this user automatically reuse this cookie without re-authenticating
    await transport.ConnectAsync(new RemoteCredentials
    {
        GrantType = AuthGrantType.Password,
        Username = dto.Username,
        Password = dto.Password,
    });

    // 2. Write credentials to browser cookies (HttpOnly prevents XSS; in production, also encrypt / use Secure / SameSite)
    //    CredentialsResolver extracts the user identity from the client cookie at Invoke time to look up the cached cookie
    ctx.Response.Cookies.Append("username", dto.Username, new CookieOptions
    {
        HttpOnly = true,
        MaxAge = TimeSpan.FromHours(2),
    });
    ctx.Response.Cookies.Append("password", dto.Password, new CookieOptions
    {
        HttpOnly = true,
        MaxAge = TimeSpan.FromHours(2),
    });
});

public class LoginDto
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
```

**2. Configure LiteOrm.Remote in BFF with Dynamic mode**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor(); // required for Dynamic mode

builder.Host.RegisterLiteOrmRemote(opts =>
{
    opts.RemoteServiceUri = new Uri("http://localhost:5000"); // remote data service address
    opts.CredentialsMode = RemoteCredentialsMode.Dynamic;
    opts.CredentialsResolver = sp =>
    {
        var httpCtx = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var request = httpCtx?.Request;
        if (request is null) return null;

        // Extract user identity from client cookie
        if (!request.Cookies.TryGetValue("username", out var user) || string.IsNullOrEmpty(user)
            || !request.Cookies.TryGetValue("password", out var pwd) || string.IsNullOrEmpty(pwd))
        {
            return null;
        }
        return new RemoteCredentials
        {
            GrantType = AuthGrantType.Password,
            Username = user,
            Password = pwd,
        };
    };
});

var app = builder.Build();
// ... app.MapController / app.MapRemoteInvokeEndpoint etc.
app.Run();
```

**3. BFF business endpoint calls the LiteOrm remote proxy; cookie reused automatically**

```csharp
app.MapGet("/api/me", async (HttpContext httpContext, IDemoUserService userService) =>
{
    // The userService proxy resolved within this request scope
    // On InvokeAsync, CredentialsResolver resolves the current user's credential key,
    // looks up the cached cookie established at login from the transport, and writes it to the request header — no repeated Connect
    var username = httpContext.Request.Cookies["username"];
    if (string.IsNullOrEmpty(username)) return Results.Unauthorized();
    var user = await userService.GetByUserNameAsync(username);
    return Results.Ok(user);
});
```

Full call flow:

| Step | Actor | Action |
|------|-------|--------|
| 1 | Browser | POST `/api/login` |
| 2 | BFF | `transport.ConnectAsync(user credentials)` once; the cookie returned by the remote service is cached in the transport's internal dictionary |
| 3 | BFF | Login succeeds; writes browser cookie, returns login response |
| 4 | Browser | GET `/api/me`, carries browser cookie |
| 5 | BFF | `CredentialsResolver` extracts `username`/`password` from `HttpContext.Request.Cookies` to derive the credential key |
| 6 | BFF | `InvokeAsync` looks up the cached cookie for that key and writes it to the request header, then calls the remote business endpoint (no Connect) |
| 7 | Remote service | `HttpContext.User` is restored, business logic runs |

> **Production notes**:
> - Storing plaintext passwords in cookies is insecure. Prefer storing encrypted credentials or short-lived tokens; decrypt them in `CredentialsResolver` to derive the credential key
> - The server cookie cache is held by the `ConcurrentDictionary<string, string>` inside `HttpRemoteServiceTransport`, keyed by credential, thread-safe for concurrent multi-user access
> - If the cookie expires and the remote service returns 401, the caller must re-call `ConnectAsync` to refresh the cache; optionally clear the cache entry for that credential key when InvokeAsync raises a 401
> - If the remote service already uses stateless auth like JWT, return a `RemoteCredentials` with only `Extensions["token"]` and provide a custom `IRemoteAuthenticationHandler` on the server to validate the token

### 5.6 Accessing the Current User on the Server

The `Invoke` endpoint automatically restores `HttpContext.User` through the ASP.NET Core authentication middleware. To access the current user in business code, use `IHttpContextAccessor`:

```csharp
public class MyService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public MyService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task DoSomethingAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var userName = user?.Identity?.Name ?? "anonymous";
        // ...
    }
}
```

### 5.7 Disabling Built-in Authentication

To use JWT or other custom authentication schemes, set `EnableAuthentication = false`:

```csharp
builder.Services.AddRemoteServer(options =>
{
    options.EnableAuthentication = false;
});

// Configure your own authentication middleware
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...);
```

---

## 6. Type Resolution and ServiceName

The server finds the corresponding `Type` based on `ServiceName` in the request, and the client generates `ServiceName`. Understanding this section helps with custom service names and handling same-name type conflicts.

### 6.1 `TypeResolverHelper` — Bidirectional Type Name ↔ Type Resolution

`LiteOrm.Common.TypeResolverHelper` is a public utility class providing bidirectional conversion between type names and `Type`.

| Method | Description |
|--------|-------------|
| `GetName(Type)` | Generates a serializable type name. Non-generic returns `Type.Name`; generic returns `BaseName<ParamShortName1,...>` (strips the backtick arity suffix) |
| `FindType(string typeName, string? defaultNamespace = null)` | Looks up a type by name |
| `Register(string name, Type type)` | Registers a custom name ↔ type mapping (**highest priority**) |
| `Unregister(string name)` | Unregisters a custom mapping |
| `TryParseGenericServiceName(string)` | Parses a generic service name into (baseName, paramName array), e.g. `IEntityService<User>` → `("IEntityService", ["User"])` |

`FindType` resolution order: custom registrations → `Type.GetType` → exact full name match → default namespace + short name → short name scan.

> **Generic type names**: Generic types should use the CLR name format `Foo`1` (with backtick arity suffix), to avoid conflicts with non-generic types of the same name.

### 6.2 `IRemoteServiceTypeResolver` — Server Type Resolver

The server uses `IRemoteServiceTypeResolver` to resolve the `ServiceName` (short type name) in the request to the actual service interface type.

| Implementation | Behavior |
|---------------|----------|
| `DefaultServiceTypeResolver` | Default implementation. Scans all assemblies by short type name when no namespace is specified; when `ServiceNamespace`/`ModelNamespace` is specified, prefers exact match by `Namespace.TypeName`, falling back to full assembly short-name scan on failure |
| `DelegateRemoteServiceTypeResolver` | Custom resolution logic via delegate |
| Custom `IRemoteServiceTypeResolver` | Full control over the resolution process |

```csharp
// Default: scan all assemblies by short type name
options.ServiceTypeResolver = new DefaultServiceTypeResolver();

// Specify namespaces for faster exact matching and to avoid name conflicts
options.ServiceTypeResolver = new DefaultServiceTypeResolver(
    serviceNamespace: "MyApp.Services",
    modelNamespace: "MyApp.Models");

// Or use a factory (can inject other DI services)
builder.Services.AddRemoteServer(options =>
{
    options.ServiceTypeResolverFactory = sp =>
        new DefaultServiceTypeResolver("MyApp.Services", "MyApp.Models");
});
```

### 6.3 ServiceName Consistency Convention

- When both ends enable `AutoRegisterEntityServices`, the framework ensures consistency automatically
- When manually registering custom names, both ends must call `TypeResolverHelper.Register`
- Generic service interfaces use the CLR name format `Foo`1` to look up open generics

---

## 7. Argument Write-back (ArgumentOut)

> Due to the **loss of reference semantics** in remote calls (parameters are deserialized new instances on the server), modifications to parameters on the server are not automatically reflected back to the client. The `[ArgumentOut]` family of attributes is used to declare parameters that need write-back, with the framework extracting write-back values on the server and applying them on the client.

### 7.1 `[IdentityOut]` — Auto-increment Primary Key Write-back

`IEntityServiceAsync<T>`'s `InsertAsync` / `BatchInsertAsync` are already annotated with `[IdentityOut]` by default. After calling, the Id is automatically written back:

```csharp
var user = new User { UserName = "alice" };
await userService.InsertAsync(user);
Console.WriteLine($"New user Id = {user.Id}");  // Id has been written back

var orders = new List<Order> { /* ... */ };
await orderService.BatchInsertAsync(orders);
foreach (var o in orders)
    Console.WriteLine($"OrderNo={o.OrderNo}, Id={o.Id}");  // Each Id has been written back
```

> **Dependency**: `IdentityOutAttribute` resolves the Identity column through `TableInfoProvider.Default`. Both client and server must register it (`LiteOrm` main library's `LiteOrmCoreInitializer` initializes it automatically).

### 7.2 `[CopyableOut]` — Full Object Write-back

Applicable to parameter types that implement the `ICopyable` interface. The server returns the parameter object itself directly, and the client copies it entirely to the original object via `ICopyable.CopyFrom`.

```csharp
public class CopyableUser : ICopyable
{
    public long Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    public void CopyFrom(object other)
    {
        var src = (CopyableUser)other;
        Id = src.Id;
        Name = src.Name;
        CreatedAt = src.CreatedAt;
    }
}

public interface ICopyableUserService
{
    Task CreateAsync([CopyableOut(typeof(CopyableUser))] CopyableUser user);
}
```

### 7.3 `ArgumentMode` Enum

| Value | Description | `ReturnType` Meaning |
|-------|-------------|----------------------|
| `Single` (default) | Single parameter write-back | The type of the write-back value |
| `Collection` | Iterates `IEnumerable`/`IList`, calling handler per item | Write-back value type for **each element** (framework automatically wraps as `List<ReturnType>` for serialization) |

### 6.4 Custom Write-back Handler

Implement the `IArgumentOutHandler` interface (in the `LiteOrm.Common` namespace), and mark the parameter with `[ArgumentOut(typeof(YourHandler), typeof(ReturnType))]`:

```csharp
using LiteOrm.Common;

public class TimestampOutHandler : IArgumentOutHandler
{
    public Type ReturnType { get; }

    // Constructor must accept a Type parameter (the framework passes attribute.ReturnType)
    public TimestampOutHandler(Type returnType) { ReturnType = returnType; }

    // Server: extract the value to send back from the parameter object
    public object GenerateReturnValue(object argument)
    {
        var entity = (MyEntity)argument;
        return entity.UpdatedAt;   // Return the server-generated timestamp
    }

    // Client: apply the write-back value to the original parameter object (keeping the reference unchanged)
    public void WriteBack(object originalArg, object returnValue)
    {
        var entity = (MyEntity)originalArg;
        entity.UpdatedAt = (DateTime)returnValue;
    }
}

// Usage
public interface IMyService
{
    Task InsertAsync([ArgumentOut(typeof(TimestampOutHandler), typeof(DateTime))] MyEntity entity);
}
```

**Handler instantiation rules**:

1. If the attribute itself directly implements `IArgumentOutHandler` (e.g. `[IdentityOut]`, `[CopyableOut]`), use the attribute instance itself
2. Otherwise, prefer resolving `HandlerType` from the DI container
3. If DI resolution fails, create via `(Type returnType)` constructor

> **Note**: The argument to `GenerateReturnValue` is a **deserialized copy generated on the server**; modifications to it do not affect the client. Write-back can only be done through return value + `WriteBack`.

---

## 8. Custom Transport Layer

Beyond the default HTTP transport, you can implement custom transports based on named pipes, gRPC, message queues, etc.

### 8.1 `IRemoteServiceTransport` Interface

The base interface for all transport layer implementations, covering connection (authentication) and invocation:

```csharp
public interface IRemoteServiceTransport
{
    Task ConnectAsync(RemoteCredentials credentials, CancellationToken cancellationToken = default);
    Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken = default);
}
```

| Method | Description |
|--------|-------------|
| `ConnectAsync(RemoteCredentials)` | Establishes an authenticated connection with the server. After the server validates credentials via `HttpContext.SignInAsync`, subsequent requests carry the authentication cookie automatically |
| `InvokeAsync` | Sends a remote invocation request and returns the response |

### 8.2 `JsonRemoteServiceTransport` Abstract Base Class (Recommended)

In the `LiteOrm.Remote` namespace, handles request/response serialization and deserialization via `System.Text.Json`. **Custom transport layers should prefer inheriting from this class**, only needing to implement two abstract methods:

```csharp
public abstract class JsonRemoteServiceTransport : IRemoteServiceTransport
{
    // Already implemented (virtual): serialize request → call GetResponseJsonAsync → deserialize response
    public virtual async Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken = default);

    // Subclass must implement: carry credentials to establish an authenticated connection
    public abstract Task ConnectAsync(RemoteCredentials credentials,
        CancellationToken cancellationToken = default);

    // Subclass must implement: send JSON string to remote, return response JSON string
    public abstract Task<string> GetResponseJsonAsync(
        string requestJson, CancellationToken cancellationToken = default);
}
```

**Built-in serialization config**: `UnsafeRelaxedJsonEscaping` + `PropertyNameCaseInsensitive = true`.

**Inheritance example** (named pipe based):

```csharp
using LiteOrm.Common;

public class NamedPipeTransport : JsonRemoteServiceTransport
{
    private readonly string _pipeName;
    public NamedPipeTransport(string pipeName) => _pipeName = pipeName;

    public override Task ConnectAsync(RemoteCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        // Named pipe can pass credentials through the pipe; simplified to no-op here
        return Task.CompletedTask;
    }

    public override async Task<string> GetResponseJsonAsync(
        string requestJson, CancellationToken cancellationToken = default)
    {
        using var client = new NamedPipeClientStream(".", _pipeName);
        await client.ConnectAsync(cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(requestJson);
        await client.WriteAsync(bytes.AsMemory(0, bytes.Length), cancellationToken);
        // Read response JSON ...
        return responseJson;
    }
}

opts.Transport = new NamedPipeTransport("liteorm-remote");
```

### 8.3 Default HTTP Transport (`HttpRemoteServiceTransport`)

Built-in subclass of `JsonRemoteServiceTransport`, based on `HttpClient`. Configure via `RemoteServiceUri` + `ConfigureHttpClient` (see [Section 4.2](#42-client-configuration-liteormoptions)).

### 8.4 Fully Custom Transport

Implement `IRemoteServiceTransport` directly (without inheriting `JsonRemoteServiceTransport`), handling serialization yourself:

```csharp
public class MyTransport : IRemoteServiceTransport
{
    public Task<RemoteInvocationResponse> InvokeAsync(
        RemoteInvocationRequest request, CancellationToken cancellationToken)
    {
        // Must handle request serialization, transport, and response deserialization on your own
    }
}

opts.Transport = new MyTransport();
```

---

## 9. Serialization Constraints

Remote service invocation **completely relies on JSON serialization of input parameters and return values**. Understanding the following constraints helps avoid common pitfalls.

| Constraint | Description |
|------------|-------------|
| **Loss of reference semantics** | Parameter objects are deserialized new instances on the server; modifications to them are not automatically reflected back to the client. Use `[ArgumentOut]` attributes when write-back is needed |
| **Circular references not supported** | `System.Text.Json` does not support circular references by default; parameter/return value object graphs must be tree-shaped |
| **Types must be serializable** | Parameter and return value types must be public, have a parameterless constructor, and public readable/writable properties. Private fields and read-only collections do not participate in serialization |
| **`CancellationToken` not serialized** | The cancellation token is passed end-to-end by the transport layer as call context and does not appear in `Arguments` |
| **`Expr` parameters serialized by declared type** | Lambda expressions written in business code are **first converted to `Expr` derived classes** in the client process by `LambdaExprConverter.ToLogicExpr` before transmission. `Expression<Func<T,bool>>` itself is never serialized |

For the JSON structure of requests and responses, see [Expression Serialization](../04-extensibility/04-expr-serialization.md) and the source code `LiteOrm.Common/Remote/RemoteInvocationMessage.cs`.

---

## 10. Notes

1. **`ForEachAsync` is not supported for remote calls**: Streaming iteration requires continuous data return, which the remote protocol does not support; throws `NotSupportedException`
2. **`CancellationToken` transparent passing**: The cancellation token is not serialized; it is passed end-to-end by the transport layer
3. **Client and server must register the same `TableInfoProvider.Default`**: `IdentityOutAttribute` resolves the Identity column through `TableInfoProvider.Default`, with no reflection fallback
4. **`ServiceName` consistency**: When both ends enable `AutoRegisterEntityServices`, the framework ensures consistency automatically; when manually registering custom names, both ends must call `TypeResolverHelper.Register`
5. **Generic service interfaces**: `DefaultServiceTypeResolver` uses the CLR name format `Foo`1` to look up open generics, avoiding conflicts with non-generic types of the same name
6. **Base interface method inheritance**: Methods declared in the service type and all its base interfaces can be invoked; throws `AmbiguousMatchException` on duplicate method keys
7. **Castle DynamicProxy compatibility**: When intercepting methods inherited from base interfaces, the framework automatically resolves the most derived service interface

### Comparison with Local Services

| Dimension | Local Service | Remote Service |
|-----------|---------------|----------------|
| Registration | `RegisterLiteOrm` auto-scans `[Service]` | `RegisterLiteOrmRemote` + proxy registration |
| Invocation | Direct reflection call | Dynamic proxy interception + HTTP forwarding |
| Transactions | `[Transaction]` AOP | Cross-process transactions not supported (see [Transactions Guide](01-transactions.en.md)) |
| `ForEachAsync` | Streaming iteration | Throws `NotSupportedException` |
| Parameter write-back | Direct object modification | Serialized write-back via `OutArguments` |
| Exception propagation | Original exception | `RemoteInvocationResponse.Error` carries exception info |

---

## 11. Features and Advantages

| Feature | Description |
|---------|-------------|
| **Zero intrusion** | Business code requires no changes — local and remote calls are written identically; only the registration method changes |
| **Interface as contract** | The service interface definition itself is the API protocol; no need to write Controllers, DTO mappings, or OpenAPI docs |
| **Auto Identity write-back** | `[IdentityOut]` attribute automatically handles auto-increment primary key write-back; batch insert supports collection mode write-back |
| **Flexible transport layer** | Built-in HTTP transport; quickly implement named pipe, gRPC, and other custom transports by inheriting `JsonRemoteServiceTransport` |
| **Smart type resolution** | `$type` wrapping strategy automatically handles parameter type polymorphism; `TypeResolverHelper` supports custom service name registration |
| **Auto-registration** | `AutoRegisterEntityServices` enabled by default; scans `[Service]` attribute to automatically complete name mapping and proxy registration |
| **Progressive evolution** | Smoothly evolve from a monolithic app (`RegisterLiteOrm`) to frontend-backend separation (`RegisterLiteOrmRemote`) without changing service interface definitions |

---

## Related Links

- [Configuration and Registration](../01-getting-started/03-configuration-and-registration.en.md) — Full documentation for `RegisterLiteOrm` / `RegisterLiteOrmRemote`
- [Expression Guide](../02-core-usage/06-expr-guide.en.md) — Lambda condition queries, also applicable to remote calls
- [Expression Serialization](../04-extensibility/04-expr-serialization.en.md) — Serialization mechanism for `Expr` expression trees
- [RemoteServiceDemo.cs](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/RemoteServiceDemo.cs) — 13 typical client operation scenarios
