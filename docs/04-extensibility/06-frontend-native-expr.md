# 前端原生 Expr 查询

这篇文档同样属于**扩展接入方案**：当前端已经不是“固定几个筛选项”，而是需要动态组合字段、操作符、排序和分页时，可以直接提交 LiteOrm 原生 `Expr` JSON。

推荐做法是：**前端按照 `JsonSerializer.Serialize<Expr>(...)` 的实际输出形状来构造 JSON**，而不是另外发明一套只给前端使用的 DSL。

## 场景选型

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 动态多条件查询 | 原生 Expr | 字段、操作符和值都可以在运行时组合 |
| AND / OR 可切换 | 原生 Expr | 更容易表达复合逻辑 |
| 多列排序 | 原生 Expr | `OrderBys` 直接支持多列 |
| 自定义分页 | 原生 Expr | `Skip` / `Take` 原生可用 |

## 1. 接入原则

在前端直接提交 Expr 时，建议先统一下面三条约定：

- 前后端共用 LiteOrm 的表达式语义
- 前端按 LiteOrm 实际序列化结果构造 JSON
- 后端接收后仍然要补充权限与安全校验

## 2. 当前实际 JSON 形状

LiteOrm 在序列化 `SectionExpr -> OrderByExpr -> WhereExpr` 时，输出形状大致如下：

```json
{
  "$section": {
    "$orderby": {
      "$where": null,
      "Where": {
        "$": "and",
        "Items": [
          {
            "$": "==",
            "Left": { "#": "Status" },
            "Right": { "@": "Pending" }
          },
          {
            "$": ">=",
            "Left": { "#": "TotalAmount" },
            "Right": { "@": 300 }
          }
        ]
      }
    },
    "OrderBys": [
      {
        "Field": { "#": "CreatedTime" },
        "Asc": false
      }
    ]
  },
  "Skip": 0,
  "Take": 5
}
```

关键点：

1. `$section` 的值表示它的 `Source`
2. `Skip` / `Take` 写在同层
3. `$orderby` 的值表示它的 `Source`
4. `OrderBys` 写在同层
5. `$where` 的值表示它的 `Source`；如果没有上游片段，可以是 `null`
6. `Where` 写在同层

## 3. 前端构造步骤

### 3.1 先生成逻辑表达式

```javascript
const logicExpr = {
    "$": "and",
    "Items": [
        { "$": "==", "Left": { "#": "Status" }, "Right": { "@": "Pending" } },
        { "$": ">=", "Left": { "#": "TotalAmount" }, "Right": { "@": 300 } }
    ]
};
```

### 3.2 构造 `WhereExpr` 的序列化结果

```javascript
let expr = {
    "$where": null,
    "Where": logicExpr
};
```

### 3.3 构造 `OrderByExpr` 的序列化结果

```javascript
expr = {
    "$orderby": expr,
    "OrderBys": [
        { "Field": { "#": "CreatedTime" }, "Asc": false },
        { "Field": { "#": "TotalAmount" }, "Asc": true }
    ]
};
```

### 3.4 最外层包成 `SectionExpr` 的序列化结果

```javascript
expr = {
    "$section": expr,
    "Skip": 0,
    "Take": 5
};
```

## 4. JavaScript 调用示例

```javascript
const payload = {
    "$section": {
        "$orderby": {
            "$where": null,
            "Where": {
                "$": "contains",
                "Left": { "#": "CustomerName" },
                "Right": { "@": "Contoso" }
            }
        },
        "OrderBys": [
            { "Field": { "#": "CreatedTime" }, "Asc": false }
        ]
    },
    "Skip": 0,
    "Take": 5
};

const result = await demoApp.apiFetch("/api/orders/query/expr", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
});
```

## 5. 后端会做什么

后端通常直接按 `Expr` 接收这份 JSON，然后解析出：

- 过滤条件
- 排序项
- 分页参数

随后再补充权限过滤。对于非 `Admin` 用户，后端会自动附加：

```csharp
Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id
```

### 5.1 Count 缓存

原生 Expr 查询同样经常需要返回 `total`，因此也适合给 `Count` 增加短时缓存。

一个适合示例项目的做法是：

- 先把最终生效的过滤条件整理成 `Expr`
- 直接把该 `Expr` 作为 count 缓存键
- 把用户权限收敛后的过滤条件一起纳入缓存键语义
- 创建、更新、删除成功后，通过提升缓存版本号统一失效旧缓存

LiteOrm 的 `Expr` 已经实现了结构化 `Equals/GetHashCode`，因此相同结构的原生 Expr 过滤条件在连续翻页时可以复用同一个 count 结果，而不需要先转成 JSON 再做键。

### 5.2 注意点

- `OrderBy`、`Skip`、`Take` 不影响总数时，可以只按最终过滤条件缓存 count
- 如果你的原生 Expr 里允许注入用户范围，务必在权限过滤补齐后再生成缓存键
- 这类缓存更适合降低重复分页时的数据库压力，不适合替代真正的统计聚合缓存
- Demo 里用内存缓存即可；生产环境如有多实例，需要改成共享缓存方案

## 6. 常见误区

### 6.1 直接写 `"$": "section"` / `Source`

推荐直接和 LiteOrm 实际序列化结果保持一致，而不是再包装一层自定义结构。

### 6.2 把 `Skip` / `Take` 塞进 `$section` 对象内部

它们应该和 `$section` 平级，而不是写进 `$section` 的值里面。

### 6.3 把 `OrderBys` 塞进 `$orderby` 的值里面

`OrderBys` 应该和 `$orderby` 平级；`$orderby` 的值只表示它的 `Source`。

## 7. 相关链接

- [返回目录](../README.md)
- [权限过滤](../03-advanced-topics/06-permission-filtering.md)
- [前端 QueryString 查询](./05-frontend-querystring.md)
- [查询指南](../02-core-usage/03-query-guide.md)
- [LiteOrm.WebDemo](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.WebDemo/)
