# 前端原生 Expr 查询

当查询条件不再是“几个固定字段 + 一个排序”时，前端直接提交 LiteOrm 原生 Expr JSON 会更灵活。`LiteOrm.WebDemo` 当前采用的规则是：**前端按照 `JsonSerializer.Serialize<Expr>(...)` 的实际输出形状来构造 JSON**，而不是另外发明一套自定义结构。

## 场景选型

| 场景 | 推荐方式 | 原因 |
|------|----------|------|
| 动态多条件查询 | 原生 Expr | 字段、操作符和值都可以在运行时组合 |
| AND / OR 可切换 | 原生 Expr | 更容易表达复合逻辑 |
| 多列排序 | 原生 Expr | `OrderBys` 直接支持多列 |
| 自定义分页 | 原生 Expr | `Skip` / `Take` 原生可用 |

## 1. 当前实际 JSON 形状

LiteOrm 在序列化 `SectionExpr -> OrderByExpr -> WhereExpr` 时，输出形状大致如下：

```json
{
  "$section": {
    "$order": {
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
3. `$order` 的值表示它的 `Source`
4. `OrderBys` 写在同层
5. `$where` 的值表示它的 `Source`；如果没有上游片段，可以是 `null`
6. `Where` 写在同层

## 2. 前端构造步骤

### 2.1 先生成逻辑表达式

```javascript
const logicExpr = {
    "$": "and",
    "Items": [
        { "$": "==", "Left": { "#": "Status" }, "Right": { "@": "Pending" } },
        { "$": ">=", "Left": { "#": "TotalAmount" }, "Right": { "@": 300 } }
    ]
};
```

### 2.2 构造 `WhereExpr` 的序列化结果

```javascript
let expr = {
    "$where": null,
    "Where": logicExpr
};
```

### 2.3 构造 `OrderByExpr` 的序列化结果

```javascript
expr = {
    "$order": expr,
    "OrderBys": [
        { "Field": { "#": "CreatedTime" }, "Asc": false },
        { "Field": { "#": "TotalAmount" }, "Asc": true }
    ]
};
```

### 2.4 最外层包成 `SectionExpr` 的序列化结果

```javascript
expr = {
    "$section": expr,
    "Skip": 0,
    "Take": 5
};
```

## 3. JavaScript 调用示例

```javascript
const payload = {
    "$section": {
        "$order": {
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

## 4. 后端会做什么

`LiteOrm.WebDemo` 直接按 `Expr` 接收这份 JSON，然后解析出：

- 过滤条件
- 排序项
- 分页参数

随后再补充权限过滤。对于非 `Admin` 用户，后端会自动附加：

```csharp
Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id
```

## 5. 常见误区

### 5.1 直接写 `"$": "section"` / `Source`

当前 WebDemo 前端页不再使用这套写法，而是和 LiteOrm 实际序列化结果保持一致。

### 5.2 把 `Skip` / `Take` 塞进 `$section` 对象内部

它们应该和 `$section` 平级，而不是写进 `$section` 的值里面。

### 5.3 把 `OrderBys` 塞进 `$order` 的值里面

`OrderBys` 应该和 `$order` 平级；`$order` 的值只表示它的 `Source`。

## 6. 下一步

- [返回目录](../README.md)
- 权限过滤：[权限过滤与用户范围控制](./06-permission-filtering.md)
- 前端 QueryString 查询：[前端 QueryString 查询](./07-frontend-querystring.md)
- 查询指南：[查询指南](../02-core-usage/03-query-guide.md)
