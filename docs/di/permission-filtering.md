# 权限过滤与用户范围控制

当系统既要展示查询能力，又要避免普通用户读写到不属于自己的数据时，权限过滤就不能只停留在前端页面提示层。LiteOrm 中常见的承载位置有两层：

1. **运行时 Expr**：按当前用户、当前租户、接口参数动态追加条件。
2. **模型级 `ConstFilter`**：承载固定状态、固定分区、历史兼容模型这类恒定规则；值来自运行时上下文时，用 `GenericSqlExpr` 提供取值。

核心原则是：**当前用户 / 当前租户属于运行时上下文，优先用 Expr；需要它们自动覆盖关联与写入路径时，落到 `TableDefinition.ConstFilter`，取值由 `GenericSqlExpr` 提供；而特性里的 `Constant` 只适合固定不变的规则。**

多租户按隔离层次拆出的四种落地写法（构造 `Expr`、`TableArgs` 分表、`ConstFilter` 切片、重写 `DataSource` 分库）见[多租户隔离](../typical-applications/tenant-isolation.md)。

在实际项目里，一条查询通常不是只有“权限条件”这一项，而是会同时叠加：

- 业务条件
- 软删除条件（例如 `IsDeleted == false`）
- 当前用户 / 当前租户范围条件

本篇聚焦各机制的**原理与用法**；面向业务的落地场景清单见[数据权限](../typical-applications/data-permission.md)。

## 场景选型

| 场景 | 推荐做法 | 原因 |
|------|----------|------|
| 管理员查看全部订单 | 不附加用户范围条件 | 保留完整运维/审计视角 |
| 普通用户查询列表、统计 | 运行时追加 `Expr` | 当前用户属于请求时上下文 |
| 当前用户详情、修改、删除 | 详情接口再做显式访问校验 | 避免只靠列表过滤被绕过 |
| 模型天然固定状态 / 固定分区 | `[Column(Constant = ...)]` / `TableDefinition.ConstFilter` | 规则在模型层面恒定不变 |

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

`[Column]` 的 `Constant` 参数是**针对整张表生效的全局固定筛选**，在关联查询（`From<...>()`、`TableJoinExpr`）里也会进入 `JOIN ... ON`。在实现上，它会在元数据阶段收敛为 `TableDefinition.ConstFilter`：

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
4. 关联查询里，关联表的固定筛选进入 `JOIN ... ON`。
5. `ForeignExpr` / `Exists` / `ExistsRelated` 这类 `EXISTS` 子查询，也会先并入目标表自己的 `ConstFilter`，再叠加关联条件和你传入的 `InnerExpr`。
6. `UPDATE` / `DELETE` 语句同样带上这条规则；DAO 走主键的读写路径（`GetObject`、`ExistsKey`、`Update`、`DeleteByKeys`、批量更新与删除）也一并生效，即模型看不见的行读不出、改不动、删不掉。条件更新与删除（`Update(UpdateExpr)`、`Delete(LogicExpr)`）同样生效。
7. 关联表的固定筛选只进表达式查询生成的关联语句。DAO 按主键读取（`GetObject`、`ExistsKey`）走的是模型自带的 `From` 片段，这段片段只拼关联键，不带关联表的固定筛选，用它读关联列时仍可能读到固定筛选之外的关联行内容；要把关联表一起限定住，用 `Search(...)` 这类表达式查询。

声明了固定筛选的表不复用命令缓存：缓存保留的是首次生成的 SQL 与参数，而切片条件来自表元数据、运行时可能被替换（取值乃至条件结构都会变），复用会把旧内容固化下来；这类表每次调用都新建命令，新建的命令也不写入缓存，因此不会占用常规命令的缓存槽位。运行时替换 `TableDefinition.ConstFilter` 后，下一次调用即按新条件执行；代价是这类表每次操作多一次 SQL 拼接，未声明固定筛选的表照旧使用命令缓存（缓存里存的是底层命令本身，每次取用新建一个不拥有它的代理，释放代理不会影响缓存）。

它适合：

- 启用态、正式态、历史兼容表等**模型级恒定条件**
- 固定业务分区、固定来源、固定租户类型这类**编译期就确定**的规则
- 固定数值、布尔、字符串标记这类**不会随请求变化**的表级条件
- 租户、组织这类需要**在任意查询、关联与写入路径都自动生效**的行级切片。这类值来自运行时上下文，由 `GenericSqlExpr` 承载取值，做法见本节结尾

它不适合：

- 用 `Column.Constant` 把某个具体的当前用户、当前租户写进模型。特性参数是编译期常量，写进去的值对所有请求相同，结果是所有人都看到同一个租户的数据

如果你有自定义元数据提供器，也可以在生成 `TableDefinition` 时直接设置 `ConstFilter`。`TableDefinition.ConstFilter` 本身是公开可写属性，还有一条更直接的路：取到表定义后赋值，取值来源交给 `GenericSqlExpr`。

```csharp
var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(Order))!;
tableDefinition.ConstFilter = Expr.Sql("TenantFilter");   // 构件内部直接读当前租户
```

这样条件就脱离了「编译期常量」的约束，同时保留了 `ConstFilter`「任意查询、关联与写入路径都自动生效」的能力。取舍在于语义和代价：`ConstFilter` 变成运行时取值的属性之后，声明了它的表不再复用预定义命令缓存，每次操作都要重新拼接 SQL 并重建命令。这种做法完整的落地写法，包括租户接口、构件注册与生效范围，见[多租户隔离](../typical-applications/tenant-isolation.md)的示例三。

这也意味着：如果你在 `ExistsRelated<Department>(...)` 里按部门表过滤用户，而 `Department` 本身又声明了 `State == Enabled` 一类的固定规则，那么这条规则会自动进入 `EXISTS` 子查询，不需要你在 `InnerExpr` 里再手写一次。

### 2.3 用 `GenericSqlExpr` 封装“从用户上下文取值”的过滤

当你希望把“当前用户过滤”封装成可复用规则，但又不想把 `currentUser.Id` 作为调用参数层层往下传时，可以让 `GenericSqlExpr` 直接从用户上下文中取值：

```csharp
using static LiteOrm.Common.Expr;

// 这里的 UserContext.Current 只是示意，请替换成你自己的用户上下文访问器
GenericSqlExpr.Register("CurrentUserFilter", (context, _) =>
{
    var currentUser = UserContext.Current
        ?? throw new InvalidOperationException("Current user not found.");

    string paramName = context.OutputParams.Count.ToString();
    context.OutputParams.Add(new(context.SqlBuilder.ToParamName(paramName), currentUser.Id));
    return $"{context.SqlBuilder.ToSqlName(nameof(Order.CreatedByUserId))} = {context.SqlBuilder.ToSqlParam(paramName)}";
});

var filter = BuildBusinessFilter(request)
    & (Prop(nameof(Order.IsDeleted)) == false)
    & Expr.Sql("CurrentUserFilter");
```

这种方式的价值在于：

- 可以把“当前用户数据过滤”做成统一构件复用
- 当前用户值来自用户上下文，而不是由调用方手动传参
- 可以和普通 Expr、软删除条件、统计查询一起组合
- 仍然通过 `context.OutputParams` 走参数化，不需要把用户值直接拼进 SQL

安全注意事项见[安全性](../advanced-topics/security.md)中的 `GenericSqlExpr` 章节。

## 4. 前端联动建议

- 在 UI 中明确告知普通用户“查询结果已自动按当前账号或租户范围过滤”。
- 遇到 `403` 时提示“当前用户无权访问这条数据”，不要误报成“记录不存在”。
- 不要依赖前端隐藏按钮来实现权限控制；按钮隐藏只是体验优化，不是安全边界。

## 5. 常见误区

### 5.1 只在前端做权限控制

前端可以隐藏按钮，但不能作为最终授权依据。真正的权限边界必须在后端。

### 5.2 只限制列表，不限制详情和删除

只要详情、修改、删除接口没有校验，用户就仍然可能通过直接请求访问到不属于自己的对象。

### 5.3 用 `Column.Constant` 承载当前用户或当前租户

`[Column(Constant = ...)]` 的参数是编译期常量。把当前登录态、令牌、请求头里的值写进去，写进去的具体值对所有请求都相同，结果是所有人都看到同一个用户或同一个租户的数据。

需要「值随请求变化、但生效范围仍覆盖全部路径」时，把 `ConstFilter` 在表定义上赋值，取值交给 `GenericSqlExpr`，见 2.2 结尾。值只在查询入口使用、不需要覆盖关联与写入路径时，用运行时 `Expr` 或 `GenericSqlExpr` 构件更轻。

### 5.4 把行过滤和物理分表混为一谈

`TenantId == currentTenantId` 解决的是共享表里的**行隔离**；`TableArgs` / `CreateSqlBuildContext` 解决的是**真实表路由**。两者可以同时存在，但不应该相互替代。

## 相关链接

- [返回目录](../README.md)
- [关联查询](../core-usage/associations.md)
- [分表分库](../advanced-topics/sharding-and-tableargs.md)
- [安全性](../advanced-topics/security.md)
- [Lambda 与 Expr 组合使用](../core-usage/lambda-expr-mixing.md)

