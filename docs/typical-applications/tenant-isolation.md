# 多租户隔离

多租户落地时通常要回答三个问题：租户条件放在哪一层、租户值从哪里来、漏写一次会付出什么代价。

LiteOrm 没有内置的租户机制，提供的是一组原语，租户这件事要自己拼出来。下面先定义一个租户接口，再用四个示例覆盖四层隔离：行过滤、物理分表、固定筛选、物理分库。

| 隔离层次 | 用到的原语 | 粒度 | 示例 |
| --- | --- | --- | --- |
| 共享表按行隔离 | 运行时 `Expr` 条件 | 行 | 示例一 |
| 独立物理表 | `[Table("Orders_{0}")]` + `TableArgs` | 表名 | 示例二 |
| 共享表固定切片 | `TableDefinition.ConstFilter` | 行 | 示例三 |
| 独立物理库 | 重写 DAO 的 `DataSource` | 连接 | 示例四 |

四层可以叠加，例如先按租户分库、再在库里按月分表。

## 租户接口

租户值来自请求头、令牌或会话，同一个进程要同时服务多个租户。先用一个接口收口「当前租户是谁」：

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

需要按租户隔离的实体，再实现一个标记接口。接口只声明租户列，属性在实现类型上用表达式体给出当前值：

```csharp
public interface ITenantEntity
{
    string TenantId { get; }
}

[Table("Orders")]
public class Order : ObjectBase, ITenantEntity
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("TenantId")]
    public string TenantId => TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    [Column("Amount")]
    public decimal Amount { get; set; }
}
```

这样实体自己就知道租户列叫什么、值从哪里来，后面的注册与切片都只认这一个名字。

要点：

- 租户上下文里那个静态访问器（上面写作 `TenantContext.Current`）必须是 `AsyncLocal` 或请求作用域，单例会让所有租户读到同一个值。
- 租户列写成只读的表达式体属性。它照常参与读取映射，写入时由数据库或实体属性提供值，不需要业务代码手动赋值。
- 缓存键必须带租户维度。`"order:123"` 这种键在共享表模式下会跨租户命中。

## 示例一：按当前租户构造 `Expr`

**需求**：Orders 表所有租户共用，租户标识是普通列。列表、统计、导出、批量更新都不能出现别的租户的数据。

**做法**：把租户条件当成查询本身的一部分，由一个函数统一拼装，所有入口共用：

```csharp
using static LiteOrm.Common.Expr;

public static class OrderFilters
{
    public static LogicExpr For(OrderQueryRequest request)
    {
        var filter = (Prop(nameof(ITenantEntity.TenantId)) == TenantContext.Current?.TenantId)
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

**优点**：直接。条件写在查询里，读代码的人一眼能看出这次查询限定到了哪个租户；加日志、断点、单测都不需要绕路。`Prop(...)` 生成带表别名的列引用，join 进来同样有 `TenantId` 列的表也不会歧义。表达式查询、条件更新、条件删除都能用同一个函数。

**缺点**：只能限制当前表，也就是只能限制这一次语句里亲手写上的那张表。三个具体后果：

- 关联查询里另一张表不受约束。订单带出部门、部门本身也按租户隔离时，`OrderFilters.For(...)` 管不到 `JOIN` 进来的部门行。
- `EXISTS` 子查询、`UPDATE` / `DELETE` 的 `WHERE` 都要自己带上，漏一个入口就漏一个租户。批量更新、批量删除同样要带范围条件，写法见[数据权限](./data-permission.md)的场景 2。
- 主键读写路径要另做对象级校验。条件决定「查得到什么」，决定不了「按主键直接操作」，详情、修改、删除仍要单独判断归属。

## 示例二：`TableArgs` 按租户分表

**需求**：每个租户一张独立表，表名按租户码拼出来。

**做法**：表名带占位符，实体实现 `IArged`，框架在写入时自动带上参数：

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged, ITenantEntity
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    [Column("TenantId")]
    public string TenantId => TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string[] IArged.TableArgs => new[] { TenantId };
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
        context.TableArgs = new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
        return context;
    }
}
```

**优点**：租户维度落到真实表名，条件不进 `WHERE`，单表查询的语句形状和单租户部署完全一致。按租户清理、归档、迁移都是表级操作。

**缺点**：

- 写入路径上 `EntityService<T>` 检测到 `IArged` 会自动带上参数，批量写入先按 `TableArgs` 分组再逐组执行。直接调 `IObjectDAO<T>` 写库不会做这件事，需要自己调 `WithArgs`，所以分表实体的写入建议统一走服务层。
- `TableExpr` 自己带 `TableArgs` 时会覆盖上下文继承的值。租户上下文为 A 时 `From<TenantOrder>("tenant_b")` 仍然查 B 表，多租户代码里这类写死表参数的语句应该被评审拦下。
- 每一项参数都会做 SQL 名称合法性校验，租户码里带引号、分号、空格会在赋值时抛错，不会等到拼 SQL。
- 只解决表路由，不解决行权限。同一张表里还有别的租户维度（组织、区域）时，仍要靠示例一或示例三。

## 示例三：把租户条件设成 `ConstFilter`，`GenericSqlExpr` 直接从上下文取值

**需求**：几十个查询入口都要带租户条件，靠形参一路透传，参数多一层就漏一层。希望条件写一次，任何查询、任何关联、任何写入路径都自动带上。

**做法**：把租户条件设成实体表定义上的固定筛选（`TableDefinition.ConstFilter`），构件里直接读 `TenantContext.Current`。

`ConstFilter` 平时由 `[Column(Constant = ...)]` 聚合出来，值是编译期常量。这里走另一条路，直接给 `TableDefinition` 赋值，取值由 `GenericSqlExpr` 提供。两条路落在同一个属性上，运行时行为一致。

先注册构件，委托里直接取当前租户：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("TenantFilter", (context, _) =>
{
    string tenant = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = context.OutputParams.Count.ToString();
    context.OutputParams.Add(new Param(context.SqlBuilder.ToParamName(paramName), tenant));
    return $"{context.SqlBuilder.ToSqlName(nameof(ITenantEntity.TenantId))} = {context.SqlBuilder.ToSqlParam(paramName)}";
});
```

再给所有实现租户接口的实体装上这个条件。关键是这一句：`TableDefinition.ConstFilter` 是公开可写的 `get; set;` 属性，取到表定义后直接赋值即可，不需要自定义元数据提供器。

```csharp
public static class TenantEntitySetup
{
    public static void EnableTenantFilter(IEnumerable<Assembly> assemblies)
    {
        foreach (var type in assemblies.SelectMany(a => a.GetTypes())
                                       .Where(t => !t.IsAbstract && typeof(ITenantEntity).IsAssignableFrom(t)))
        {
            var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(type);
            if (tableDefinition is null) continue;

            tableDefinition.ConstFilter = Expr.Sql("TenantFilter");
        }
    }
}
```

启动时调用一次，之后每次操作都带上租户条件：

```csharp
TenantEntitySetup.EnableTenantFilter(AppDomain.CurrentDomain.GetAssemblies());

var orders = await orderViewService.Search(From<OrderView>());

// SELECT *
// FROM "Orders" "T0"
// WHERE "TenantId" = @0                            -- @0 = tenant_a
```

取值的时机在 SQL 生成那一刻，也就是每次查询都重新读一遍 `TenantContext.Current`，同一个进程切换租户即生效。

**生效范围**。这是它相对于示例一的价值，条件进入了框架自己的 SQL 生成路径，不依赖调用方记得写：

- 主表 `WHERE`，包括 `Search` / `Count` / `Exists` 这类表达式查询，以及 `GetObject` / `ExistsKey` 等按主键的读路径。
- 关联查询的 `JOIN ... ON`。附带租户切片的订单视图 join 部门时，两张表各自带上自己的条件。
- `ForeignExpr` / `Exists` / `ExistsRelated` 生成的 `EXISTS` 子查询，子查询目标表自己的 `ConstFilter` 会先并进去。
- `UPDATE` / `DELETE` 的 `WHERE`，含主键路径与批量更新、批量删除。

**代价与限制**：

- 声明了固定筛选的表不复用预定义命令缓存：每次操作都重新拼接 SQL、重建命令，代价是每次操作多一次 SQL 拼接。没有固定筛选的表照旧走缓存。
- 构件里只能写裸列名。`sqlBuilder.ToSqlName` 生成不带表别名的 `"TenantId"`，而框架自己生成的条件是带别名的 `"T0"."TenantId"`。单表没问题，语句里 join 了同样带 `TenantId` 列的表就可能被判成歧义列，需要和示例一配合，关联表用 `Prop(...)` 手写条件。
- 这个构件必须在 SQL 生成之前注册。同一 key 重复注册不覆盖，测试里换实现要换 key。
- 委托是在建表元数据那段代码里引用的，`TableDefinition` 一旦被 `TableInfoProvider` 缓存，后续取到的都是同一份对象；给已缓存的 `TableView` 再改定义不会自动重建，赋值要放在第一次查询之前。
- 严格地说这是「运行时用 `GenericSqlExpr` 给固定筛选提供取值」，而不是把当前租户当作编译期常量写进特性。反过来的做法仍然要避免：`[Column(Constant = "...")]` 写进一个具体租户，结果是所有租户共用同一个条件。

## 示例四：重写 DAO 的 `DataSource` 分库

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
public sealed class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    public TenantOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
        return context;
    }
}

services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

`DataSource` 是 `DAOBase` 上的 `protected virtual` 属性，默认返回表定义里的数据源名称。行过滤 DAO 与视图 DAO 都要重写，读和写各走一边。

**优点**：隔离度最高，租户之间连物理资源都不共享，备份、恢复、迁移、配额可以按库独立做。连接的选择和 `TableArgs` 的组合互不干扰，一个决定库、一个决定表。

**缺点**：

- 自定义 DAO 要把子类注册到基类的位置，否则服务层解析到的仍然是框架默认的 `ObjectDAO<T>`，连接不会切换。注册要覆盖所有用到的 DAO 类型：`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>`，视图 DAO 在 `DataViewDAO<T>` 上更容易被漏掉。
- 数据源本身在启动阶段登记（`AddDataSource` 或 `RegisterLiteOrm` 读配置），运行期只做选择。租户连接串从数据库或配置中心动态拉取时，要把登记时机和租户加载时机对齐。
- 跨租户的批量操作会落在不同连接上，没有跨库事务可用，要拆成各自独立的作用域。同一个 `SessionManager` 内的所有数据源上下文会被 `BeginTransaction` 一并纳入事务，只读连接跳过，因此一个租户失败会连带回滚另一个租户的写入。
- 示例三的 `ConstFilter` 仍然适用，分库不等于可以省掉行过滤：同一个库里可能还留着别的租户维度的数据。

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../di/permission-filtering.md)
- [分表分库](../advanced-topics/sharding-and-tableargs.md)
- [数据权限](./data-permission.md)
- [审计与变更追踪](./audit-and-change-tracking.md)
- [并发控制与读写分离](./concurrency-and-read-write-splitting.md)
