# Generic Controller and Dynamic Controller Generation

When a project has many entities, writing a separate Controller for each one produces a lot of repetitive code. This document introduces two approaches to reduce duplication: **generic base Controller** and **dynamic Controller generation**.

## Scenario guide

| Scenario | Recommended approach | Why |
|------|----------|------|
| Few entities, each with custom logic | Hand-written Controller | Maximum flexibility |
| Many entities, most only need standard CRUD | Generic base Controller | Reduces repetition while retaining override extensibility |
| Many entities with highly uniform CRUD patterns | Dynamic Controller generation | Zero hand-written code; auto-scans entities and generates Controllers |

## 1. Generic base Controller

### 1.1 Define the generic base class

```csharp
[ApiController]
[Route("api/[controller]")]
public abstract class EntityControllerBase<T, TView> : ControllerBase
    where T : class
    where TView : class
{
    private const int MaxPageSize = 100;

    protected IEntityServiceAsync<T> EntityService => HttpContext.RequestServices.GetRequiredService<IEntityServiceAsync<T>>();
    protected IEntityViewServiceAsync<TView> ViewService => HttpContext.RequestServices.GetRequiredService<IEntityViewServiceAsync<TView>>();

    [HttpGet("{id}")]
    public virtual async Task<TView?> GetById(object id)
    {
        return await ViewService.GetObjectAsync(id);
    }

    [HttpGet]
    public virtual async Task<List<TView>> List()
    {
        return await ViewService.SearchAsync();
    }

    [HttpPost]
    public virtual async Task<bool> Create(T entity)
    {
        return await EntityService.InsertAsync(entity);
    }

    [HttpPut]
    public virtual async Task<bool> Update(T entity)
    {
        return await EntityService.UpdateAsync(entity);
    }

    [HttpDelete("{id}")]
    public virtual async Task<bool> Delete(object id)
    {
        return await EntityService.DeleteIDAsync(id);
    }

    [HttpPost("page")]
    public virtual async Task<IActionResult> PageQuery([FromBody] JsonElement exprJson)
    {
        Expr? expr;
        try
        {
            expr = exprJson.Deserialize<Expr>();
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return BadRequest(new { error = "Invalid Expr JSON.", detail = ex.Message });
        }

        if (expr is null)
            return BadRequest(new { error = "Request body must be a valid Expr JSON." });

        var validation = ExprValidator.CreateQueryOnly();
        if (!validation.VisitAll(expr))
            return BadRequest(new { error = "Expr contains disallowed node types.", failedType = validation.FailedExpr?.GetType().Name });

        int skip = 0, take = 20;

        if (expr is SectionExpr section)
        {
            skip = Math.Max(0, section.Skip);
            take = section.Take;
            if (take < 1) take = 20;
            if (take > MaxPageSize) take = MaxPageSize;
            expr = RebuildSection(section, skip, take);
        }
        else if (expr is LogicExpr logicExpr)
        {
            expr = Expr.From<TView>().Where(logicExpr).Section(0, take);
        }
        else
        {
            expr = Expr.From<TView>().Section(0, take);
        }

        var countExpr = ExtractFilter(expr);
        var total = await ViewService.CountAsync(countExpr);
        var items = await ViewService.SearchAsync(expr);

        return Ok(new { skip, take, total, items });
    }

    private static SectionExpr RebuildSection(SectionExpr section, int skip, int take)
    {
        var source = section.Source;
        var rebuilt = (source as ISectionAnchor)?.Section(skip, take)
            ?? Expr.From<TView>().Section(skip, take);
        return (rebuilt as SectionExpr)!;
    }

    private static Expr? ExtractFilter(Expr? expr)
    {
        return expr switch
        {
            SectionExpr s => ExtractFilter(s.Source),
            OrderByExpr o => ExtractFilter(o.Source),
            WhereExpr w => w.Where,
            LogicExpr l => l,
            _ => null
        };
    }
}
```

### 1.2 Concrete entity Controller

Concrete entity Controllers only need to inherit and specify the generic parameters:

```csharp
public class UsersController : EntityControllerBase<User, UserView>
{
}
```

### 1.3 Custom behavior

If an entity needs custom behavior, you can override the corresponding method:

```csharp
public class OrdersController : EntityControllerBase<DemoOrder, DemoOrderView>
{
    [HttpGet]
    public override async Task<List<DemoOrderView>> List()
    {
        return await ViewService.SearchAsync(o => o.Status == "Pending");
    }
}
```

### 1.4 PageQuery

The base class includes a built-in `POST /api/{controller}/page` endpoint that accepts a JSON-serialized Expr as the request body, automatically handling pagination and total count:

**Submit a LogicExpr (auto-wrapped with pagination):**

```json
POST /api/DemoDepartments/page
Content-Type: application/json

{
  "$": "==",
  "Left": {"#": "Name"},
  "Right": {"@": "IT"}
}
```

**Submit a complete query chain (with sorting and pagination):**

```json
POST /api/DemoDepartments/page
Content-Type: application/json

{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": {
        "$": "==",
        "Left": {"#": "Name"},
        "Right": {"@": "IT"}
      }
    },
    "OrderBys": [
      {"Field": {"#": "Name"}, "Asc": true}
    ]
  },
  "Skip": 0,
  "Take": 20
}
```

Response format:

```json
{
  "skip": 0,
  "take": 20,
  "total": 1,
  "items": [...]
}
```

**Pagination safety limit**: `take` is capped at 100; values exceeding 100 are automatically truncated. Values less than 1 default to 20.

**Expr type validation**: The endpoint uses `ExprValidator.CreateQueryOnly()` to validate the submitted Expr, allowing only query-related expression types and preventing injection risks.

### 1.5 Extending the base class

You can add more common methods to the base class, such as conditional counts:

```csharp
[HttpGet("count")]
public virtual async Task<int> Count([FromQuery] string? keyword)
{
    if (string.IsNullOrEmpty(keyword))
        return await ViewService.CountAsync();
    return await ViewService.CountAsync(Expr.Prop("Name").Contains(keyword));
}

[HttpGet("exists/{id}")]
public virtual async Task<bool> Exists(object id)
{
    return await ViewService.ExistsIDAsync(id);
}
```

## 2. Dynamic Controller Generation

When you have a large number of entities that all follow the same CRUD pattern, you can use `System.Reflection.Emit` to dynamically generate Controller assemblies at runtime, avoiding the need to write Controller classes for each entity manually.

### 2.1 Prerequisites

First, define the same `EntityControllerBase<T, TView>` as in Section 1, which serves as the parent class for dynamic Controllers.

### 2.2 Generate Controllers dynamically

```csharp
using System.Reflection;
using System.Reflection.Emit;

public static Assembly BuildDynamicControllers(string defaultNamespace)
{
    var assemblyName = new AssemblyName("DynamicControllers");
    var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
    var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

    foreach (var entityType in typeof(ObjectBase).Assembly.GetTypes())
    {
        if (!entityType.IsSubclassOf(typeof(ObjectBase)) || entityType.IsAbstract)
            continue;
        if (entityType.Name.EndsWith("View"))
            continue;
        if (entityType.GetConstructor(Type.EmptyTypes) == null)
            continue;

        var viewType = typeof(ObjectBase).Assembly.GetType(entityType.FullName + "View");
        if (viewType == null || !viewType.IsSubclassOf(entityType))
            viewType = entityType;

        var controllerName = $"{entityType.Name}Controller";
        var existingController = Type.GetType($"{defaultNamespace}.Controllers.{controllerName}");
        if (existingController != null)
            continue;

        var parentType = typeof(EntityControllerBase<,>).MakeGenericType(entityType, viewType);
        var typeBuilder = moduleBuilder.DefineType(
            $"{defaultNamespace}.Controllers.{controllerName}",
            TypeAttributes.Public, parentType);

        var ctorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);

        var il = ctorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, parentType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, Type.EmptyTypes));
        il.Emit(OpCodes.Ret);

        typeBuilder.CreateType();
    }

    return assemblyBuilder;
}
```

### 2.3 Register dynamic Controllers

In `Program.cs`, call the generation method and register the dynamic assembly with MVC:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();

var dynamicAssembly = BuildDynamicControllers("YourApp");
builder.Services
    .AddControllers()
    .AddApplicationPart(dynamicAssembly);

var app = builder.Build();
app.MapControllers();
app.Run();
```

### 2.4 View type lookup rules

During dynamic generation, the View type is resolved using the following rules:

1. Look up `EntityName + View` (e.g. `User` → `UserView`) in the same assembly as the entity
2. If found and the View type is a subclass of the entity type, use that View type
3. Otherwise, fall back to the entity type itself (`TView == T`)

### 2.5 Coexistence with hand-written Controllers

During dynamic generation, the code checks whether a Controller with the same name already exists (via `Type.GetType`). If a hand-written Controller already exists, that entity is skipped. This allows mixed usage:

- Most entities use dynamically generated standard CRUD Controllers
- Entities that need custom logic have hand-written Controllers that inherit the generic base class

## 3. Caveats

- Dynamic Controllers are best suited for admin panels and other scenarios where the CRUD pattern is highly uniform
- If certain entities require custom logic (permission filtering, parameter validation, etc.), write a Controller by hand that inherits the generic base class
- Dynamically generated Controllers cannot be type-checked at compile time; watch for route conflicts during debugging
- Assemblies generated with `AssemblyBuilderAccess.Run` cannot be saved to disk and are regenerated on each startup

## Related Links

- [Back to docs hub](../README.md)
- [First Complete Example](../01-getting-started/04-first-example.en.md)
- [View Models and Services](../02-core-usage/02-view-models-and-services.en.md)
- [Permission Filtering](../03-advanced-topics/06-permission-filtering.en.md)
