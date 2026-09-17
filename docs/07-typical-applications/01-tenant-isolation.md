# 多租户隔离典型应用

多租户落地时通常要回答三个问题：租户条件放在哪一层、租户值从哪里来、漏写一次会付出什么代价。下面把常见需求拆成七个场景，每个场景给出完整代码。

先看可选的四层隔离，以及每层用到的原语：

| 隔离层次 | 用到的原语 | 粒度 |
| --- | --- | --- |
| 共享表按行隔离 | 运行时 `Expr` 条件，或注册成 `GenericSqlExpr` 构件 | 行 |
| 同结构表固定切片 | `[Column(Constant = ...)]` 聚合出的 `TableDefinition.ConstFilter`（仅编译期常量） | 表 |
| 独立物理表 | `[Table("Orders_{0}")]` + `TableArgs`（`IArged` / `From<T>(tableArgs)` / `WithArgs`） | 表名 |
| 独立物理库 | `[Table(DataSource = "...")]`，或 DAO 重写 `DataSource` | 连接 |

四层可以叠加，例如先按租户分库、再在库里按月分表。

## 场景 1：一张共享表服务所有租户

**需求**：Orders 表所有租户共用，租户标识是普通列。列表、统计、导出、批量更新都不能出现别的租户的数据。

**做法**：把租户条件当成查询本身的一部分，由一个函数统一拼装，所有入口共用：

```csharp
using static LiteOrm.Common.Expr;

public static class OrderFilters
{
    public static LogicExpr For(OrderQueryRequest request)
    {
        var tenantId = TenantContext.Current?.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved.");

        var filter = (Prop(nameof(Order.TenantId)) == tenantId)
            & (Prop(nameof(Order.IsDeleted)) == false);

        if (!string.IsNullOrEmpty(request.Keyword))
            filter &= Prop(nameof(Order.Title)).Like($"%{request.Keyword}%");

        return filter;
    }
}
```

列表和统计走同一个函数，只是最外层语句不同：

```csharp
var page = await orderViewService.SearchAsync(
    From<OrderView>().Where(OrderFilters.For(request)).OrderBy(Prop(nameof(Order.CreateTime)).Desc()).Section(0, 20));

var total = await orderViewService.CountAsync(OrderFilters.For(request));
```

要点：

- 不要在查完之后用内存 `Where` 过滤。这样 `Count` 与分页总数会不一致，聚合、导出接口会绕开过滤，不该读的数据也已经进了应用进程。
- 批量更新、批量删除同样要带范围条件，写法见[数据权限典型应用](./02-data-permission.md)的场景 2。
- 行过滤只决定“查得到什么”，决定不了“按主键直接操作”。详情、修改、删除要另做对象级校验。

## 场景 2：租户条件不想层层传参

**需求**：几十个查询入口都要带租户条件，靠形参一路透传，参数多一层就漏一层。

**做法**：把条件注册成可复用构件，租户值从上下文里取：

```csharp
using static LiteOrm.Common.Expr;

// 注册一次即可，重复注册不会覆盖已有实现
GenericSqlExpr.Register("TenantFilter", (context, sqlBuilder, outputParams, _) =>
{
    var tenantId = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), tenantId));
    return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
});
```

使用处按名字引用，和其他 `Expr` 条件照常组合：

```csharp
var filter = OrderFilters.For(request) & Expr.Sql("TenantFilter");

var orders = await orderViewService.SearchAsync(From<OrderView>().Where(filter));
```

要点：

- 参数值通过 `outputParams.Add` 走参数化，不会拼进 SQL 文本。安全边界见[安全性](../03-advanced-topics/06-security.md)。
- 构件返回 `null` 或空串时整个片段不产生 SQL，查询条件里不需要写 `"1 = 1"` 这类恒真条件来凑语法。写入路径没有这个保证，见[数据权限典型应用](./02-data-permission.md)。
- 拼接时 `&` 左侧要是 `LogicExpr`。`Expr.Sql(...)` 返回的是 `GenericSqlExpr`，左侧声明成 `Expr` 会编译不过，把拼装函数的返回类型写成 `LogicExpr` 就能直接用。
- 构件里只能写表名和列名，属性名写错会在生成 SQL 时暴露，不会静默走到别的列上。

## 场景 3：某个模型天然只服务固定一类租户

**需求**：平台自营订单、归档租户、历史兼容模型这类对象，规则在编译期就确定，不希望每个查询都重复写。

**做法**：把规则下沉到列元数据，用 `Constant` 声明固定值：

```csharp
public enum TenantKind
{
    Platform = 1,
    Merchant = 2
}

[Table("Orders")]
public class PlatformOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("TenantKind", Constant = TenantKind.Platform)]
    public TenantKind TenantKind => TenantKind.Platform;
}
```

`[Column(Constant = ...)]` 在构建表元数据时聚合为 `TableDefinition.ConstFilter`，条件自动进入主表 `WHERE`、关联查询的 `JOIN ... ON`、`UPDATE` 的 `WHERE` 和 `DELETE` 的 `WHERE`：

```sql
-- 查询：枚举切片按底层值内联成字面量
SELECT * FROM "Orders" "T0" WHERE "T0"."TenantKind" = 1                        -- Platform = 1

-- 另一个视图模型上的切片：被关联表的切片进 ON，布尔切片同样内联
SELECT * FROM "Orders" "T0"
LEFT JOIN "Departments" "Dept" ON "T0"."DepartmentId" = "Dept"."Id" AND "Dept"."State" = 1
WHERE "T0"."IsDeleted" = 0                                                     -- Enabled = 1

-- 更新与删除
UPDATE "Customers" SET "Name" = @0 WHERE "Id" = @1 AND "IsDeleted" = 0
DELETE FROM "Customers" WHERE "Id" = @0 AND "IsDeleted" = 0
```

要点：

- `Constant` 只接受编译期常量，能写枚举成员、枚举名、数字、字符串、布尔值，框架会按属性类型做转换。
- 枚举与布尔切片都按底层值内联成字面量：枚举取底层数值（`= 1`），布尔取 `= 0` / `= 1`。切片是编译期常量，不占参数位，切片条件的参数个数也不会随元数据变化。
- 它表达的是“这张表天然只有这类数据”。把当前请求的租户写进去，结果是所有租户共用同一个固定条件，这是多租户下最严重的一类故障。
- `INSERT` 不受这个条件约束，语句里不会附带切片条件，切片值来自实体属性本身（上例的只读属性恒为 `Platform`，插入时就把这个值写进去）。属性可写时要留意另一面：写入切片外的值不会在插入时被纠正，这行插进去马上对模型不可见，写入前先确认取值落在切片内。
- 关联表的切片进的是关联查询的 `JOIN ... ON`。DAO 按主键读取（`GetObject`、`ExistsKey`）走的是模型自带的 `From` 片段，这段片段只拼关联键、不带关联表的切片条件，需要连关联表一起限定时改用 `Search(...)` 这类表达式查询。完整边界见[权限过滤与用户范围控制](../06-di/02-permission-filtering.md)。
- 若程序集启用了 `TableInfo` 源生成（NativeAOT 构建，或显式声明 `[assembly: LiteOrmCodeGen(LiteOrmCodeGenKind.TableInfo)]`），表元数据由 `CommonTableInfoProvider` 提供，而生成的 `ColumnInfo` 不携带 `Constant`，此时特性声明的切片不会变成 `ConstFilter`，上面这些条件一个都不会出现。这类项目改走运行时构件，见下一个场景。

## 场景 4：租户值在启动阶段才确定，还想让它自动生效

**需求**：同一个实体在不同部署环境服务不同的租户或分区，值来自配置文件或环境变量。特性参数必须是编译期常量写不进去；又不想每个查询入口都记得加条件。

**做法**：把租户值的来源封装成一个构件，注册一次，用 `GenericSqlExpr.Get` 构造时把租户值作为 `Arg` 传进去：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

public static class TenantFilter
{
    private const string Key = "TenantFilter";

    /// <summary>启动阶段调用一次即可，之后的查询都从配置里取值。</summary>
    public static void Register(IConfiguration configuration)
    {
        string? tenantId = configuration["Tenant:Id"];
        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException("Tenant is not configured.");

        GenericSqlExpr.Register(Key, (context, sqlBuilder, outputParams, arg) =>
        {
            if (arg is not string tenant || tenant.Length == 0)
                throw new InvalidOperationException("Tenant is not resolved.");

            string paramName = outputParams.Count.ToString();
            outputParams.Add(new Param(sqlBuilder.ToSqlParam(paramName), tenant));
            return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
        });

        DefaultTenantId = tenantId;   // 构造时用它填充 Arg
    }

    public static string? DefaultTenantId { get; private set; }

    public static LogicExpr For(string? tenantId = null)
    {
        string tenant = string.IsNullOrEmpty(tenantId) ? DefaultTenantId! : tenantId;
        if (string.IsNullOrEmpty(tenant))
            throw new InvalidOperationException("Tenant is not resolved.");

        return GenericSqlExpr.Get(Key, tenant);
    }
}
```

查询处只写一句 `TenantFilter.For()`，租户值不经过任何形参：

```csharp
var orders = await orderViewService.SearchAsync(
    From<OrderView>().Where(TenantFilter.For() & (Prop(nameof(Order.IsDeleted)) == false)));

// SELECT *
// FROM "Orders" "T0"
// WHERE "TenantId" = @0 AND "T0"."IsDeleted" = @1    -- @0 = tenant_a
```

要点：

- 关键在于租户值走 `Arg`，不依赖环境上下文。构件拿到的是构造表达式那一刻的值，`Clone` 也会带上它，表达式在缓存、队列、跨线程传递之后仍然是同一个租户，不会跟着当时的 `AsyncLocal` 漂移。
- 表达式类型要声明成 `LogicExpr`。`GenericSqlExpr` 本身是 `LogicExpr` 的子类，直接返回它能和 `&`、`|` 正常组合。
- 校验要拦空串而不能只拦 `null`，同时注意构件有个容易踩的默认行为：返回 `null` 或空串时整段不产生 SQL，连前面的 `AND` 也会一起收回去。`null` 会让租户条件整段消失，空串会生成 `"TenantId" = @0` 且值为空串（条件在但匹配不到行），两种失败方式都不容易在测试里发现，所以上面把两种都直接抛掉。
- 构件里列的写法要留意：`sqlBuilder.ToSqlName` 只接受裸列名，生成出来的是不带表别名的 `"TenantId"`，而框架自己生成的条件是带别名的 `"T0"."IsDeleted"`。单表查询没问题，一旦语句里 join 了同样带 `TenantId` 列的表，未限定列名就可能被数据库判成歧义列。多表场景下要把别名一并写进片段，或者干脆退回场景 1 的 `Prop(...)` 写法。
- 这个构件管的是「语句条件」，进的是你写在 `Where` 里的那条语句。它不像 `ConstFilter` 那样自动作用于关联表 `JOIN ... ON`、`EXISTS` 子查询和所有 `UPDATE` / `DELETE`，这些位置要自己带上 `TenantFilter.For()`：

```csharp
var affected = await orderService.UpdateAsync(
    Expr.Update<Order>().Set((nameof(Order.Amount), Const(1m))).Where(TenantFilter.For()));

// UPDATE "Orders" SET "Amount" = @0 WHERE "TenantId" = @1
```

- 源生成路径下这个做法是闭式解：它完全不碰表元数据，`ColumnInfo` 里有没有 `Constant` 都无所谓，AOT 构建同样可用。
- 与场景 3 的分工要清楚：`ConstFilter` 表达「这张表天然只有这类数据」，构件表达「本次查询要限定到谁」。同一个进程要按租户切换时只能用后者，前者是进程级元数据，改了就全局生效。
- 构件键名是全局的，重复注册不会覆盖已有实现，同一个 key 只会认第一次注册的那个委托。测试里想换实现要先换 key，别指望重新注册能生效。

## 场景 5：租户决定物理表名

**需求**：每个租户一张独立表，表名按租户码拼出来。

**做法**：表名带占位符，实体实现 `IArged`，框架在写入时自动带上参数：

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[] IArged.TableArgs =>
        new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
}
```

查询有三种指定方式，按复用范围从小到大：

```csharp
// 1) 单条语句显式指定
var orders = await orderViewService.SearchAsync(From<TenantOrder>("tenant_a"));

// 2) DAO 上带参数，同一批操作复用
var dao = viewDAO.WithArgs("tenant_a");
var count = dao.Count(Prop(nameof(TenantOrder.Amount)) > 100m);
```

```csharp
// 3) 重写 CreateSqlBuildContext，该 DAO 上的所有查询自动继承
public sealed class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    public TenantOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[]
        {
            TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.")
        };
        return context;
    }
}
```

要点：

- 写入路径上 `EntityService<T>` 检测到实体实现 `IArged` 会自动带上参数，批量写入先按 `TableArgs` 分组再逐组执行。直接调 `IObjectDAO<T>` 写库不会做这件事，需要自己调 `WithArgs`，所以分表实体的写入建议统一走服务层。
- `TableExpr` 自己带 `TableArgs` 时会覆盖上下文继承的值，租户上下文为 A 时 `From<TenantOrder>("tenant_b")` 仍然查 B 表。多租户代码里这类写死表参数的语句应该被评审拦下。
- 每一项参数都会做 SQL 名称合法性校验，租户码里带引号、分号、空格会在赋值时抛错，不会等到拼 SQL。
- 第三种写法“漏写就报错”：解析不到租户直接抛异常，而不是静默落回别的表名。

## 场景 6：租户值从哪里来

**需求**：租户来自请求头、令牌或会话，同一个进程要同时服务多个租户。

**做法**：用一个 Scoped 上下文对象承载，而不是到处传参：

```csharp
public interface ITenantContext
{
    string TenantId { get; }
    string? DataSourceName { get; }
}

public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

    public string TenantId => _accessor.HttpContext?.Request.Headers["X-Tenant"].ToString()
        ?? throw new InvalidOperationException("Tenant header is missing.");

    public string? DataSourceName => _accessor.HttpContext?.Request.Headers["X-Tenant-Db"].ToString();
}

builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
```

要点：

- 上下文必须是 Scoped 或请求级。注册成 Singleton 会让所有租户读到同一个值。
- 实体里依赖静态上下文的实现（例如 `IArged.TableArgs`）要基于 `AsyncLocal`，否则并发请求会互相串值。更稳的做法是在构造实体时就把租户值固化到字段上。
- 缓存键必须带租户维度。`"order:123"` 这种键在共享表模式下会跨租户命中。

## 场景 7：租户决定连接

**需求**：租户数据落在不同数据库，连接按租户选。

**做法**：实体固定走某个连接用 `[Table(DataSource = ...)]`；运行时按租户选连接则重写 DAO 的 `DataSource`：

```csharp
public sealed class TenantOrderDAO : ObjectDAO<TenantOrder>
{
    public TenantOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;
}
```

```csharp
services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

要点：

- 自定义 DAO 要把子类注册到基类的位置，否则服务层解析到的仍然是框架默认的 `ObjectDAO<T>`，连接不会切换。
- 数据源本身在启动阶段登记（`AddDataSource` 或 `RegisterLiteOrm` 读配置），运行期只做选择。
- 同一个 `SessionManager` 内的所有数据源上下文会被 `BeginTransaction` 一并纳入事务，只读连接跳过。跨租户的后台任务要拆成各自独立的作用域，否则一个租户失败会连带回滚另一个租户的写入。

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../06-di/02-permission-filtering.md)
- [分表分库](../03-advanced-topics/01-sharding-and-tableargs.md)
- [数据权限典型应用](./02-data-permission.md)
- [审计与变更追踪典型应用](./04-audit-and-change-tracking.md)
- [并发控制与读写分离典型应用](./06-concurrency-and-read-write-splitting.md)
