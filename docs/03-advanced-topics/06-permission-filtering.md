# 权限过滤与用户范围控制

当系统既要展示查询能力，又要避免普通用户读写到不属于自己的数据时，权限过滤就不能只停留在前端页面提示层。本文结合 `LiteOrm.WebDemo` 展示一种适合 LiteOrm 的做法：**在进入 `Search` / `Count` 之前，把用户范围条件拼进 Expr，再对详情、修改、删除补充显式访问校验。**

## 场景选型

| 场景 | 推荐做法 | 原因 |
|------|----------|------|
| 管理员查看全部订单 | 不附加用户范围条件 | 保留完整运维/审计视角 |
| 普通用户查询列表、统计 | 自动附加 `CreatedByUserId == 当前用户Id` | 统一限制可见范围 |
| 普通用户读取详情、修改、删除 | 在接口层再做一次访问校验 | 避免只靠列表过滤被绕过 |
| 前端界面提示 | 只做提示，不做最终裁决 | 权限判断必须以后端为准 |

## 1. WebDemo 中的过滤行为

### 1.1 QueryString 查询与统计

`LiteOrm.WebDemo` 的 `GET /api/orders/query` 与 `GET /api/orders/stats` 会先构造业务过滤条件，再根据当前用户角色补充范围条件：

```csharp
if (request.OnlyMine == true || !IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

这样做的关键点在于：**权限条件属于查询本身的一部分**，而不是查询完成之后再在内存中裁剪结果。

### 1.2 Expr 查询

`POST /api/orders/query/expr` 同样会在进入 `SearchAsync` / `CountAsync` 之前，把当前用户的范围条件并入原生 Expr：

```csharp
filter ??= Expr.Prop(nameof(DemoOrder.Id)) > 0;

if (!IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
}
```

这样无论前端是通过可视化构造器，还是自行提交显式 `Source` 链的原生 Expr JSON，最终都会落到一致的后端权限边界上。

### 1.3 详情、修改、删除

列表过滤不能替代对象级访问控制。`LiteOrm.WebDemo` 对以下接口额外做了显式访问校验：

- `GET /api/orders/{id}`
- `PUT /api/orders/{id}`
- `DELETE /api/orders/{id}`

推荐返回明确的 `403`，这样前端更容易区分“资源不存在”和“无权访问”。

## 2. 推荐实现方式

### 2.1 把权限条件插入 Expr，而不是事后裁剪

推荐：

```csharp
var filter = BuildBusinessFilter(request);

if (!IsAdmin(currentUser))
{
    filter &= Expr.Prop(nameof(Order.CreatedByUserId)) == currentUser.Id;
}

var result = await orderService.SearchAsync(
    Expr.From<OrderView>()
        .Where(filter)
        .OrderBy(Expr.Prop(nameof(Order.CreatedTime)).Desc())
        .Section(0, 20)
);
```

不推荐：

```csharp
var items = await orderService.SearchAsync(expr);
var myItems = items.Where(x => x.CreatedByUserId == currentUser.Id).ToList();
```

后者虽然“看起来也能限制结果”，但会带来三个问题：

1. `Count` 与分页总数不准确。
2. 无法阻止不受限的聚合、统计或导出。
3. 查询层已经读到了不该读取的数据。

### 2.2 列表过滤与详情校验要同时存在

一个常见误区是：列表里已经只返回自己的数据，因此详情接口就不再校验。只要调用方能手工拼接 URL，请求 `GET /api/orders/1` 这类接口，就必须再次检查当前用户是否有权访问该对象。

## 3. 前端联动建议

- 在 UI 中明确告知普通用户“查询结果已自动按当前账号范围过滤”。
- 遇到 `403` 时提示“当前用户无权访问这条数据”，不要误报成“记录不存在”。
- 不要依赖前端隐藏按钮来实现权限控制；按钮隐藏只是体验优化，不是安全边界。

## 4. 常见误区

### 4.1 只在前端做权限控制

前端可以隐藏按钮，但不能作为最终授权依据。真正的权限边界必须在后端。

### 4.2 只限制列表，不限制详情和删除

只要详情、修改、删除接口没有校验，用户就仍然可能通过直接请求访问到不属于自己的对象。

### 4.3 把权限过滤写死在控制器里

更推荐把“用户范围条件”收敛到服务层查询构建逻辑，这样 QueryString、Expr、统计和导出才能共享同一条规则。

## 5. 下一步

- [返回目录](../README.md)
- 前端 QueryString 查询：[前端 QueryString 查询](./07-frontend-querystring.md)
- 前端原生 Expr 查询：[前端原生 Expr 查询](./08-frontend-native-expr.md)
- 关联查询：[关联查询](../02-core-usage/05-associations.md)
