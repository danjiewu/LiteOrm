# 多租户隔离的三种落地层次

多租户的难点通常不在“怎么存租户标识”，而在把租户隔离放在哪一层。放在行上、表上还是库上，决定了查询过滤条件、表名路由、连接选择的写法，也决定了后面哪些地方容易漏。

LiteOrm 不提供“多租户开关”，它提供的是三组原语：

| 隔离层次 | 用到的原语 | 隔离粒度 |
| --- | --- | --- |
| 同一张表按行隔离 | 运行时 `Expr` 条件（或 `GenericSqlExpr` 构件） | 行 |
| 同一批表按结构固定切片 | `[Column(Constant = ...)]` → `TableDefinition.ConstFilter` | 表 |
| 不同物理表 | `[Table("Orders_{0}")]` + `TableArgs`（`IArged` / `WithArgs` / `From<T>(tableArgs)`） | 表名 |
| 不同数据库 | `[Table(DataSource = "...")]`，或 DAO 重写 `DataSource` | 连接 |

四种可以叠加使用，例如“先按租户分库、再在库里按月分表”。下面逐层说明写法与容易被忽略的地方。

## 1. 共享表：行级隔离

租户字段是普通列，条件在查询入口拼装：

```csharp
using static LiteOrm.Common.Expr;

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & (Prop(nameof(Order.TenantId)) == currentTenantId);

var orders = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreateTime)).Desc())
        .Section(0, 20)
);
```

要点：

- 租户条件属于查询本身，不要查完再由内存裁剪。后者会让 `Count`、分页总数、聚合、导出全部失真，而且数据已经读到应用进程里。
- 列表、统计、导出、批量更新、批量删除都应该经过同一个“条件拼装函数”，否则总有一天会漏掉某个入口。
- 单条详情、修改、删除还要做对象级校验，行过滤只覆盖“查询得到什么”，覆盖不了“直接按主键操作”。

如果希望租户条件不靠调用方传参，可以把它做成可复用构件：

```csharp
using static LiteOrm.Common.Expr;

GenericSqlExpr.Register("TenantFilter", (context, sqlBuilder, outputParams, _) =>
{
    var tenantId = TenantContext.Current?.TenantId
        ?? throw new InvalidOperationException("Tenant not resolved.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new Param(sqlBuilder.ToParamName(paramName), tenantId));
    return $"{sqlBuilder.ToSqlName(nameof(Order.TenantId))} = {sqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request) & Expr.Sql("TenantFilter");
```

这样租户值来自上下文而不是形参，仍然走参数化，不会把值拼进 SQL 文本。`GenericSqlExpr` 的安全边界见[安全性](../03-advanced-topics/08-security.md)。

## 2. 固定切片：用 `ConstFilter` 表达“这张表天然只有某类租户”

如果某个模型只服务固定的一类数据，例如平台自营、归档租户、历史兼容模型，可以把规则下沉到元数据：

```csharp
public enum TenantKind
{
    Platform = 1,
    Merchant = 2
}

[Table("Orders")]
public class PlatformOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }

    [Column("TenantKind", Constant = TenantKind.Platform)]
    public TenantKind TenantKind => TenantKind.Platform;
}
```

`[Column(Constant = ...)]` 在元数据阶段会聚合成 `TableDefinition.ConstFilter`。生成 SQL 时，主表条件进 `WHERE`，关联表条件进 `JOIN ... ON`，`UPDATE` / `DELETE` 语句同样带上。

它表达的是“编译期就确定”的规则。当前请求的租户来自令牌、请求头或会话，就不属于这一层。把运行时值写进 `ConstFilter` 会得到一个所有租户共用的固定条件，这在多租户下是严重故障。

## 3. 物理分表：租户决定表名

表名带占位符，租户码作为 `TableArgs` 代入：

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase, IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }

    string[]? IArged.TableArgs => new[] { TenantContext.Current?.TenantId ?? throw new InvalidOperationException("Tenant not resolved.") };
}
```

写入路径上，`EntityService` 检测到实体实现 `IArged` 会自动带上 `TableArgs`，最终表名由 `string.Format("Orders_{0}", "tenant_a")` 得到 `Orders_tenant_a`。批量写入会先按 `TableArgs` 分组，再对每个租户分别执行。直接调 `ObjectDAO<T>` 写库不会做这件事，需要自己调 `WithArgs`，所以分表实体的写入建议统一走服务层。

查询路径有三种写法，按复用范围从大到小排列：

```csharp
// 1) 单条语句显式指定
var orders = await orderService.SearchAsync(
    From<TenantOrder>("tenant_a").Where(Prop(nameof(TenantOrder.Amount)) > 100m)
);

// 2) DAO 上带参数，同一批操作复用
var dao = viewDAO.WithArgs("tenant_a");
var count = dao.Count(Expr.Prop(nameof(TenantOrder.Amount)) > 100m);

// 3) 重写 CreateSqlBuildContext，让所有 Expr / ExprString / DAO 查询自动继承
public class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
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

第三种方式的好处是“漏写就报错”。租户解析不到时直接抛异常，而不是静默落回无占位符的表名或别的租户表。

### 需要注意的覆盖行为

`TableExpr` 自己带 `TableArgs` 时会覆盖上下文中继承的值：

```csharp
var context = dao.CreateSqlBuildContext();
context.TableArgs = new[] { "tenant_a" };

// 这条语句会走 tenant_b，不是 tenant_a
From<TenantOrder>("tenant_b");
```

在多租户代码里，这意味着“手工给某个 `TableExpr` 写死表参数”可能把查询带出租户边界。这类写法应该被代码评审拦下，或干脆用分析器规则禁掉。

另外 `TableArgs` 的每一项都会做 SQL 名称合法性校验（`Expr.ThrowIfInvalidSqlName`），租户码里带引号、分号、空格会在赋值时就抛错，不会等到拼 SQL。

## 4. 按租户分库

连接选择有两个入口：

```csharp
// 实体固定走某个连接
[Table("Orders", DataSource = "OrderDbEast")]
public class EastOrder : ObjectBase { /* ... */ }
```

```csharp
// 运行时按租户决定连接：自定义 DAO 覆盖 DataSource
public class TenantOrderDAO : ObjectDAO<TenantOrder>
{
    public TenantOrderDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override string? DataSource => TenantContext.Current?.DataSourceName;
}
```

第二种要记得在容器里把子类注册到基类的位置，否则服务层解析到的仍然是框架默认的 `ObjectDAO<T>`：

```csharp
services.AddScoped<ObjectDAO<TenantOrder>, TenantOrderDAO>();
services.AddScoped<ObjectViewDAO<TenantOrder>, TenantOrderViewDAO>();
```

数据源本身在启动阶段登记（`AddDataSource` / `RegisterLiteOrm` 读配置），运行期只做选择。

> `[DataSource]` 特性目前只是元数据，框架不会用它选择连接。要选连接，用 `[Table(DataSource = ...)]` 或重写 `DAOBase.DataSource`。

## 5. 租户上下文怎么传

推荐用一个 Scoped 的上下文对象承载租户，而不是到处传参：

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
}
```

几个约束：

- 上下文必须是 Scoped 或请求级。放到 Singleton 里会让所有租户读到同一个值。
- 实体里的 `IArged.TableArgs` 若依赖静态上下文，要确认实现是基于 `AsyncLocal` 的，否则并发请求会串。把租户值在构造实体时就固化到实体字段上是更稳的做法。
- 缓存键要含租户维度。`"order:123"` 这种键在共享表模式下会跨租户命中。

## 6. 事务与多租户

同一个 `SessionManager` 里的所有数据源上下文会被 `BeginTransaction` 一并纳入事务，只读连接跳过。租户隔离通常意味着“一个请求只操作一个租户”，所以正常路径不会出问题；如果某个后台任务要在一个事务里跨租户写，需要把它拆成各自独立的作用域，否则一个租户失败会连带回滚另一个租户的写入。

## 7. 常见误区

| 误区 | 后果 | 正确做法 |
| --- | --- | --- |
| 用 `ConstFilter` 承载当前租户 | 所有租户共用同一个固定条件 | 运行时条件用 `Expr` / `GenericSqlExpr` |
| 行过滤当权限用，详情接口不校验 | 直接按主键就能读写他租户数据 | 单条操作加对象级校验 |
| 手工给 `TableExpr` 指定表参数 | 查询跳出租户表 | 让 `TableArgs` 从上下文统一注入 |
| 用 Singleton 缓存租户元数据 | 串租户 | 缓存键带上租户维度 |
| 分库时忘记注册自定义 DAO | 仍然走默认连接 | 把子类注册到 `ObjectDAO<T>` / `ObjectViewDAO<T>` |

## 相关链接

- [返回目录](../README.md)
- [权限过滤与用户范围控制](../06-di/02-permission-filtering.md)
- [分表分库](../03-advanced-topics/02-sharding-and-tableargs.md)
- [数据权限与越权防护](./02-data-permission.md)
- [审计与变更追踪](./03-audit-trail.md)
- [并发控制与读取路径](./06-concurrency-and-read-path.md)
