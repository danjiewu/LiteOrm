# 前端 QueryString 查询

这篇文档讨论的不是 LiteOrm 内置查询语法，而是一种**前后端协作的接入模式**：前端把筛选条件放进 QueryString，后端再把这些参数转换成 LiteOrm `Expr` 并执行查询。

当查询字段比较稳定、又希望保留“地址栏可分享、可刷新、可回退”的体验时，这通常是成本最低的方案。

## 场景选型

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 简单列表筛选 | QueryString | 易于调试、易于分享 URL |
| 条件较固定的后台列表页 | QueryString | 前后端心智成本低 |
| 复杂嵌套逻辑、动态条件组 | 原生 Expr | QueryString 表达力不足 |
| 需要浏览器回退/刷新保留状态 | QueryString | 查询条件天然体现在地址栏 |

## 1. 接入原则

落地这类查询接口时，推荐先约定清楚三件事：

- 前端只负责传递简单、可序列化的筛选参数
- 后端统一负责把参数转换成 `Expr`
- 权限、排序白名单、分页兜底始终放在后端

如果条件已经发展到多组 AND / OR、可视化条件构造器、多列动态排序，通常应切换到“前端原生 Expr 查询”。

## 2. 可用参数

例如 `GET /api/orders/query` 这类接口通常会支持以下常用参数：

| 参数 | 说明 |
|------|------|
| `keyword` | 同时匹配订单号、客户名、商品名 |
| `status` | 订单状态 |
| `departmentName` | 部门名称包含匹配 |
| `createdByUserName` | 创建人名称包含匹配 |
| `minTotalAmount` / `maxTotalAmount` | 金额区间 |
| `createdFrom` / `createdTo` | 创建时间区间 |
| `sortBy` | 排序字段 |
| `desc` | 是否倒序 |
| `page` / `pageSize` | 分页参数 |
| `onlyMine` | 是否强制只看当前用户自己的数据 |

## 3. 前端调用流程

### 3.1 使用 `URLSearchParams` 组装参数

```javascript
const params = new URLSearchParams();
params.set("keyword", "Contoso");
params.set("status", "Pending");
params.set("sortBy", "CreatedTime");
params.set("desc", "true");
params.set("page", "1");
params.set("pageSize", "5");
```

### 3.2 发起请求

```javascript
const result = await demoApp.apiFetch(`/api/orders/query?${params.toString()}`);
```

如果还需要统计汇总，可以复用同一组筛选条件去调用：

```javascript
const stats = await demoApp.apiFetch(`/api/orders/stats?${params.toString()}`);
```

### 3.3 完整示例

```javascript
async function queryOrders() {
    const params = new URLSearchParams();
    params.set("keyword", "Contoso");
    params.set("status", "Pending");
    params.set("sortBy", "CreatedTime");
    params.set("desc", "true");
    params.set("page", "1");
    params.set("pageSize", "5");

    const result = await demoApp.apiFetch(`/api/orders/query?${params.toString()}`);
    demoApp.renderOrders("resultTable", result.items);
    demoApp.renderJson("jsonOutput", result);
}
```

## 4. 后端负责的事情

前端只是传参，真正的查询规则仍应放在后端统一处理。常见职责包括：

- 把 `keyword`、区间、排序字段等参数转换成 `Expr`
- 对非法排序字段做白名单校验
- 注入权限过滤
- 补充分页默认值和最大页大小

这样才能保证 QueryString、统计接口、导出接口共用同一套查询规则。

### 4.1 Count 缓存

如果列表接口每次都要返回 `total`，后端通常会额外执行一次 `Count`。在筛选条件频繁重复、翻页频繁发生时，这类计数查询很适合做短时缓存。

一个适合示例项目的实现方式是：

- 仅缓存 `Count` 结果，不缓存列表数据本身
- 缓存键直接使用**最终过滤条件对应的 `Expr` 对象**，并结合当前缓存版本号做有效性判断
- 创建、更新、删除订单成功后，统一提升缓存版本号，使旧的 count 缓存整体失效
- 使用短 TTL，避免演示环境里长期保留过时统计

### 4.2 实现方式

一个比较直接的做法是：

1. 在服务层构造完整过滤条件，拿到最终生效的 `Expr`
2. 直接把最终 `Expr` 作为 `IMemoryCache` 的键
3. 优先从 `IMemoryCache` 读取 count
4. 未命中时执行 `CountAsync(...)`，再把结果写回缓存
5. 在增删改成功后统一做版本失效

LiteOrm 的 `Expr` 已经实现了结构化 `Equals/GetHashCode`，因此只要表达式结构一致，就可以稳定命中同一条缓存，而不必额外转成 JSON 字符串。

这样可以保证“同一个用户、同一组过滤条件”的分页查询命中同一个 count 缓存，而不同用户范围不会串缓存。

### 4.3 注意点

- 缓存的是 `total`，不是分页结果；分页列表仍应实时查询
- 如果查询条件里已经包含用户范围，缓存键也必须包含这一部分，避免越权复用
- 演示项目适合用进程内内存缓存；多实例部署时应改成分布式缓存或直接关闭这层优化
- 版本号失效简单可靠，但会让所有 count 缓存一起过期，适合 Demo 和中小型后台，不适合极端高写入场景

## 5. 响应结构

查询接口返回的核心字段包括：

| 字段 | 说明 |
|------|------|
| `page` / `pageSize` | 当前页与每页条数 |
| `total` | 符合条件的总记录数 |
| `items` | 当前页结果 |
| `sql` | 最近执行的 SQL 片段 |

统计接口会返回：

| 字段 | 说明 |
|------|------|
| `total` | 总记录数 |
| `totalAmount` | 金额汇总 |
| `pendingCount` ~ `cancelledCount` | 各状态计数 |
| `sql` | 最近执行的 SQL 片段 |

## 6. 与权限过滤配合

QueryString 只是传参方式，不承担授权职责。后端应统一注入用户范围条件：

- `admin` 可以查看全部数据
- 非 `Admin` 用户会自动只看到自己的订单
- `onlyMine=true` 可以让管理员也主动缩小到自己的数据范围

前端推荐把这一点显示在页面说明里，但不要尝试在浏览器端自行替代后端授权。

## 7. 常见误区

### 7.1 手工拼接字符串而不做 URL 编码

优先使用 `URLSearchParams`，避免关键字中含空格、中文、特殊符号时出现编码问题。

### 7.2 只请求列表，不处理 `total`

分页列表如果不读取 `total`，前端无法正确显示总页数、总记录数和翻页边界。

### 7.3 把复杂条件强行塞进 QueryString

如果已经出现多组 AND/OR、动态排序、多条件组合，应该切换到原生 Expr 方式，而不是继续扩展 QueryString。

## 8. 相关链接

- [返回目录](../README.md)
- [权限过滤](../03-advanced-topics/06-permission-filtering.md)
- [查询指南](../02-core-usage/03-query-guide.md)
