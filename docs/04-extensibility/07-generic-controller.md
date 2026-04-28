# 泛型 Controller 与动态 Controller 生成

当项目中实体数量增多时，为每个实体手写 Controller 会产生大量重复代码。本文介绍两种减少重复的方式：**泛型基类 Controller** 和 **动态 Controller 生成**。

## 场景选型

| 场景 | 推荐做法 | 原因 |
|------|----------|------|
| 少量实体，每个都有自定义逻辑 | 手写 Controller | 灵活度最高 |
| 实体较多，大部分只需标准 CRUD | 泛型基类 Controller | 减少重复，保留 override 扩展能力 |
| 实体很多，CRUD 模式高度统一 | 动态 Controller 生成 | 零手写，自动扫描实体并生成 |

## 1. 泛型基类 Controller

### 1.1 定义泛型基类

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

### 1.2 具体实体 Controller

具体实体的 Controller 只需继承并指定泛型参数：

```csharp
public class UsersController : EntityControllerBase<User, UserView>
{    
}
```

### 1.3 自定义行为

如果某个实体需要自定义行为，可以 override 对应方法：

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

### 1.4 PageQuery 分页查询

基类内置 `POST /api/{controller}/page` 端点，接受 JSON 序列化的 Expr 作为请求体，自动处理分页和总数统计：

**提交 LogicExpr（自动包装分页）：**

```json
POST /api/DemoDepartments/page
Content-Type: application/json

{
  "$": "==",
  "Left": {"#": "Name"},
  "Right": {"@": "IT"}
}
```

**提交完整查询链（含排序和分页）：**

```json
POST /api/DemoDepartments/page
Content-Type: application/json

{
  "$section": {
    "$orderby": {
      "$where": {"$from": {"$table": "LiteOrm.WebDemo.Models.DemoDepartment"}},
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

返回格式：

```json
{
  "skip": 0,
  "take": 20,
  "total": 1,
  "items": [...]
}
```

**分页安全限制**：`take` 最大值为 100，超过时自动截断为 100；小于 1 时默认为 20。

**Expr 类型校验**：使用 `ExprValidator.CreateQueryOnly()` 验证提交的 Expr，仅允许查询相关的表达式类型，防止注入风险。

### 1.5 扩展基类

可以在基类中添加更多通用方法，例如条件统计等：

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

## 2. 动态 Controller 生成

当实体数量很多且都遵循相同的 CRUD 模式时，可以通过 `System.Reflection.Emit` 在运行时动态生成 Controller 程序集，避免为每个实体手写 Controller 类。

### 2.1 前提条件

首先定义与第 1 节相同的 `EntityControllerBase<T, TView>`，作为动态 Controller 的父类。

### 2.2 动态生成 Controller

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

### 2.3 注册动态 Controller

在 `Program.cs` 中调用生成方法，并将动态程序集注册到 MVC：

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

### 2.4 View 类型查找规则

动态生成时会按以下规则查找 View 类型：

1. 在实体同程序集中查找 `实体名 + View`（如 `User` → `UserView`）
2. 如果找到且 View 类型是实体类型的子类，使用该 View 类型
3. 否则回退到实体类型本身（`TView == T`）

### 2.5 与手写 Controller 共存

动态生成时会检查是否已存在同名 Controller（通过 `Type.GetType` 查找）。如果已存在手写 Controller，则跳过该实体。因此可以混合使用：

- 大部分实体使用动态生成的标准 CRUD Controller
- 需要自定义逻辑的实体手写 Controller 继承泛型基类

## 3. 注意事项

- 动态 Controller 适合后台管理等 CRUD 模式高度统一的场景
- 如果某些实体需要自定义逻辑（权限过滤、参数校验等），建议手写 Controller 继承泛型基类
- 动态生成的 Controller 无法在编译时进行类型检查，调试时需要注意路由冲突
- `AssemblyBuilderAccess.Run` 生成的程序集无法保存到磁盘，每次启动都会重新生成

## 相关链接

- [返回目录](../README.md)
- [第一个完整示例](../01-getting-started/04-first-example.md)
- [视图模型与服务层](../02-core-usage/02-view-models-and-services.md)
- [权限过滤](../03-advanced-topics/06-permission-filtering.md)
