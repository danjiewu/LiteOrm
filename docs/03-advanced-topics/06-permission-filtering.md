# 权限过滤与用户范围控制

当系统既要展示查询能力，又要避免普通用户读写到不属于自己的数据时，权限过滤就不能只停留在前端页面提示层。LiteOrm 中常见的承载位置有三层：

1. **运行时 Expr**：按当前用户、当前租户、接口参数动态追加条件。
2. **模型级 `ConstFilter`**：承载固定状态、固定分区、历史兼容模型这类恒定规则。
3. **`CreateSqlBuildContext` / `TableArgs`**：当租户维度已经变成物理分表或路由参数时，直接改 SQL 构建上下文。

核心原则是：**当前用户 / 当前租户属于运行时上下文，优先用 Expr 或 `GenericSqlExpr`；只有固定不变的规则才适合落到 `TableDefinition.ConstFilter`。**  
在实际项目里，一条查询通常不是只有“权限条件”这一项，而是会同时叠加：

- 业务条件
- 软删除条件（例如 `IsDeleted == false`）
- 当前用户 / 当前租户范围条件

## 场景选型

| 场景 | 推荐做法 | 原因 |
|------|----------|------|
| 管理员查看全部订单 | 不附加用户范围条件 | 保留完整运维/审计视角 |
| 普通用户查询列表、统计 | 运行时追加 `Expr` | 当前用户属于请求时上下文 |
| 当前用户详情、修改、删除 | 详情接口再做显式访问校验 | 避免只靠列表过滤被绕过 |
| 模型天然固定状态 / 固定分区 | `Column.Constant` / `TableDefinition.ConstFilter` | 规则在模型层面恒定不变 |
| 共享表多租户 | 运行时追加 `TenantId == currentTenantId` | 同一张表内按行隔离 |
| 按租户物理分表 | `TableArgs` 或重写 `CreateSqlBuildContext` | 租户决定真实表名或路由 |

## 1. WebDemo 中的当前用户过滤

### 1.1 QueryString 查询与统计

`GET /api/orders/query` 与 `GET /api/orders/stats` 这类接口通常会先构造业务过滤条件，再叠加软删除与当前用户范围条件：

```csharp
using static LiteOrm.Common.Expr;
filter &= Prop(nameof(DemoOrder.IsDeleted)) == false;
if (request.OnlyMine == true || !IsAdmin(currentUser))
{
    filter &= Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

这样做的关键点在于：**权限条件属于查询本身的一部分**，而不是查询完成之后再在内存中裁剪结果。

### 1.2 Expr 查询

`POST /api/orders/query/expr` 同样会在进入 `SearchAsync` / `CountAsync` 之前，把软删除和当前用户范围条件并入原生 Expr：

```csharp
using static LiteOrm.Common.Expr;
filter ??= Prop(nameof(DemoOrder.Id)) > 0;
filter &= Prop(nameof(DemoOrder.IsDeleted)) == false;

if (!IsAdmin(currentUser))
{
    filter &= Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

这样无论前端是通过可视化构造器，还是自行提交显式 `Source` 链的原生 Expr JSON，最终都会落到一致的后端权限边界上。

### 1.3 详情、修改、删除

列表过滤不能替代对象级访问控制。对以下接口仍应额外做显式访问校验：

- `GET /api/orders/{id}`
- `PUT /api/orders/{id}`
- `DELETE /api/orders/{id}`

推荐返回明确的 `403`，这样前端更容易区分“资源不存在”和“无权访问”。

## 2. 过滤条件应该放在哪一层

### 2.1 优先在查询入口统一拼装业务条件、软删除和用户范围

推荐：

```csharp
using static LiteOrm.Common.Expr;
var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false);

if (!IsAdmin(currentUser))
{
    filter &= Prop(nameof(Order.CreatedByUserId)) == currentUser.Id;
}

var result = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

不推荐：

```csharp
var items = await orderService.SearchAsync(expr);
var myItems = items.Where(x => x.CreatedByUserId == currentUser.Id).ToList();
```

推荐把“业务条件 + `IsDeleted` + 用户范围”在同一个查询入口统一拼好，再复用到列表、统计、导出等查询。  
后者虽然“看起来也能限制结果”，但会带来三个问题：

1. `Count` 与分页总数不准确。
2. 无法阻止不受限的聚合、统计或导出。
3. 查询层已经读到了不该读取的数据。

### 2.2 `Column.Constant` 与 `TableDefinition.ConstFilter`

`Column.Constant` 是**针对整张表生效的全局固定筛选**，包括关联查询里的 `JOIN ... ON`。在实现上，它会在元数据阶段收敛为 `TableDefinition.ConstFilter`：

```csharp
public enum RecordState
{
    Disabled = 0,
    Enabled = 1
}

[Table("Departments")]
public class Department
{
    [Column("Id", IsPrimaryKey = true)]
    public int Id { get; set; }

    [Column("State", Constant = RecordState.Enabled)]
    public RecordState State => RecordState.Enabled;
}
```

`Constant` 不只支持枚举，也支持其他可转换到属性类型的常量值，例如：

- `Constant = 1`：适用于 `int`、`long` 等数值列
- `Constant = "tenant_a"`：适用于字符串列
- `Constant = false`：适用于布尔列

如果属性本身是枚举，则仍然支持：

- `Constant = "Enabled"`：按枚举名解析
- `Constant = 1`：按整型值解析
- `Constant = RecordState.Enabled`：直接使用枚举成员

链路如下：

1. `Column.Constant` 在元数据阶段被解析。
2. 多个固定列条件会合并成 `TableDefinition.ConstFilter`。
3. 生成 SQL 时，主表固定筛选进入 `WHERE`。
4. 关联表固定筛选进入 `JOIN ... ON`。
5. `ForeignExpr` / `Exists` / `ExistsRelated` 这类 `EXISTS` 子查询，也会先并入目标表自己的 `ConstFilter`，再叠加关联条件和你传入的 `InnerExpr`。
6. `UPDATE` / `DELETE` 这类语句也会继续带上这条固定规则。

它适合：

- 启用态、正式态、历史兼容表等**模型级恒定条件**
- 固定业务分区、固定来源、固定租户类型这类**编译期就确定**的规则
- 固定数值、布尔、字符串标记这类**不会随请求变化**的表级条件

它不适合：

- 当前登录用户
- 当前请求租户
- 来自接口参数、令牌或运行时上下文的条件

如果你有自定义元数据提供器，也可以在生成 `TableDefinition` 时直接设置 `ConstFilter`；但语义仍然应该保持“固定规则”，而不是“当前请求变量”。

这也意味着：如果你在 `ExistsRelated<Department>(...)` 里按部门表过滤用户，而 `Department` 本身又声明了 `State == Enabled` 一类的固定规则，那么这条规则会自动进入 `EXISTS` 子查询，不需要你在 `InnerExpr` 里再手写一次。

### 2.3 用 `GenericSqlExpr` 封装“从用户上下文取值”的过滤

当你希望把“当前用户过滤”封装成可复用规则，但又不想把 `currentUser.Id` 作为调用参数层层往下传时，可以让 `GenericSqlExpr` 直接从用户上下文中取值：

```csharp
using static LiteOrm.Common.Expr;

// 这里的 UserContext.Current 只是示意，请替换成你自己的用户上下文访问器
GenericSqlExpr.Register("CurrentUserFilter", (context, sqlBuilder, outputParams, _) =>
{
    var currentUser = UserContext.Current
        ?? throw new InvalidOperationException("Current user not found.");

    string paramName = outputParams.Count.ToString();
    outputParams.Add(new(sqlBuilder.ToParamName(paramName), currentUser.Id));
    return $"{sqlBuilder.ToSqlName(nameof(Order.CreatedByUserId))} = {sqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & Expr.Sql("CurrentUserFilter");
```

这种方式的价值在于：

- 可以把“当前用户数据过滤”做成统一构件复用
- 当前用户值来自用户上下文，而不是由调用方手动传参
- 可以和普通 Expr、软删除条件、统计查询一起组合
- 仍然通过 `outputParams` 走参数化，不需要把用户值直接拼进 SQL

安全注意事项见[安全性](./08-security.md)中的 `GenericSqlExpr` 章节。

## 3. 多租户实现方式

多租户不是单一方案，而是“隔离层次”的选择问题。LiteOrm 中最常见的是下面三种方式。

### 3.1 共享表多租户：程序里构造 `Expr`

如果所有租户共用同一张表，最直接的办法是在查询构建阶段统一追加 `TenantId` 与 `IsDeleted` 条件：

```csharp
using static LiteOrm.Common.Expr;

var tenantFilter = Prop(nameof(Order.TenantId)) == currentTenantId;
var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & tenantFilter;

var result = await orderService.SearchAsync(
    From<OrderView>()
        .Where(filter)
        .OrderBy(Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

这是最常见、也最容易和当前用户过滤叠加的方案。

### 3.2 固定租户模型：用 `ConstFilter` 承载固定规则

如果某个模型本身就只代表某一类固定租户切片，也可以把规则下沉到 `ConstFilter`。典型场景包括：

- 只查询平台租户的数据
- 只查询内部租户归档表的数据
- 为历史兼容保留一个“天然固定过滤条件”的旧模型

例如：

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

请注意：这里表达的是“这个模型天然只看平台租户”，**不是**“每次根据当前租户动态切换”。  
一旦租户值来自当前请求，就应该回到运行时 Expr 或 `GenericSqlExpr`。

### 3.3 按租户物理分表：重写 `CreateSqlBuildContext`

如果租户维度已经体现在真实表名里，例如 `[Table("Orders_{0}")]`，那么比起加 `WHERE TenantId = ...`，更合适的是直接控制 SQL 构建上下文中的 `TableArgs`：

```csharp
[Table("Orders_{0}")]
public class TenantOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true)]
    public long Id { get; set; }
}

public class TenantOrderViewDAO : ObjectViewDAO<TenantOrder>
{
    private readonly ITenantProvider _tenantProvider;

    public TenantOrderViewDAO(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
    {
        var context = base.CreateSqlBuildContext(initTable);
        context.TableArgs = new[] { _tenantProvider.CurrentTenantCode };
        return context;
    }
}
```

这样生成 SQL 时会自动把当前租户代码代入真实表名，例如 `Orders_tenant_a`。

适合这种方式的场景：

- 不同租户落在不同物理表
- 希望 `Expr`、`ExprString`、DAO 查询统一继承同一套路由规则
- 租户路由是“表名问题”，而不是“行过滤问题”

之所以这条路径能生效，是因为 DAO 和 `ExprString` 都会通过 `CreateSqlBuildContext(...)` 创建 SQL 构建上下文；当你重写它并写入 `TableArgs` 时，后续 SQL 生成自然会沿用这组路由参数。更多分表细节见[分表分库](./02-sharding-and-tableargs.md)。

同时也要注意：如果某个下层 `TableExpr` 又显式指定了自己的 `TableArgs`，它会覆盖当前上下文中继承下来的值。  
在多租户或受范围约束的查询里，这意味着你可能无意中跳出了原本的租户 / 分片边界，因此这种显式覆盖必须经过审查。

### 3.4 三种方案如何选择

| 方案 | 适合场景 | 优点 | 限制 |
|------|----------|------|------|
| 运行时 Expr | 当前用户、当前租户、接口参数驱动的过滤 | 直观、灵活、最通用 | 需要在查询入口统一拼装 |
| `ConstFilter` | 固定状态、固定业务切片、固定租户类型 | 自动注入 SQL，主表 / 关联表都生效 | 不适合当前请求上下文 |
| `CreateSqlBuildContext` + `TableArgs` | 按租户物理分表或路由 | 直接命中真实表，适合分表设计 | 解决的是表路由，不是行权限 |

## 4. 前端联动建议

- 在 UI 中明确告知普通用户“查询结果已自动按当前账号或租户范围过滤”。
- 遇到 `403` 时提示“当前用户无权访问这条数据”，不要误报成“记录不存在”。
- 不要依赖前端隐藏按钮来实现权限控制；按钮隐藏只是体验优化，不是安全边界。

## 5. 常见误区

### 5.1 只在前端做权限控制

前端可以隐藏按钮，但不能作为最终授权依据。真正的权限边界必须在后端。

### 5.2 只限制列表，不限制详情和删除

只要详情、修改、删除接口没有校验，用户就仍然可能通过直接请求访问到不属于自己的对象。

### 5.3 用 `ConstFilter` 承载当前用户或当前租户

`ConstFilter` 表达的是固定规则，不是“本次请求是谁”。如果值来自当前登录态、令牌、请求头或租户上下文，就应改用 Expr、`GenericSqlExpr` 或表路由。

### 5.4 把行过滤和物理分表混为一谈

`TenantId == currentTenantId` 解决的是共享表里的**行隔离**；`TableArgs` / `CreateSqlBuildContext` 解决的是**真实表路由**。两者可以同时存在，但不应该相互替代。

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/06-associations.md)
- [分表分库](./02-sharding-and-tableargs.md)
- [安全性](./08-security.md)
- [Lambda 与 Expr 组合使用](../02-core-usage/07-lambda-expr-mixing.md)

