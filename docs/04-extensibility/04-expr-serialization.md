# Expr JSON 序列化格式

LiteOrm 的 `ExprJsonConverter` 支持两种 JSON 序列化格式：**简洁模式**和**正常模式**。简洁模式使用短标记来减少 JSON 体积，适合前端传输和存储；正常模式使用完整类型名称，便于调试和理解。

## 1. 核心标记说明

### 1.1 简洁模式标记

| 标记 | 含义 | 示例 |
|-----|------|------|
| `#` | 属性引用（Property） | `{"#": "Name"}` 或 `{"#": "u.Name"}` |
| `@` | 变量值 | `{"@": 42}` 或 `{"@": "hello"}` |
| `$` | 类型/操作符标识符 | `{"$": "table"}` 或 `{"$": "=="}` |
| `!` | 取反（Not） | `{"!": {"#": "IsActive"}}` |
| `$and` | 逻辑与 | `{"$and": [...]}` |
| `$or` | 逻辑或 | `{"$or": [...]}` |

### 1.2 SQL 操作符映射

| SQL 操作符 | JSON 简洁表示 |
|-----------|-------------|
| `=` | `==` |
| `<>` | `!=` |
| `>` | `>` |
| `>=` | `>=` |
| `<` | `<` |
| `<=` | `<=` |
| `.In()` | `in` |
| `.NotIn()` | `notin` |
| `.Like()` | `like` |
| `.Contains()` | `contains` |
| `.StartsWith()` | `startswith` |
| `.EndsWith()` | `endswith` |

### 1.3 ExprType 类型表格

| ExprType | 说明 | 简洁标记 | 正常模式 `$` 值 |
|----------|------|---------|---------------|
| `Table` | 表片段，表示单表或子查询引用 | `$table` | `"table"` |
| `TableJoin` | 表连接片段，表示 JOIN 子句 | `$join` | `"join"` |
| `From` | From 片段，表示数据源（表或视图） | `$from` | `"from"` |
| `Select` | 选择片段，表示 SELECT 查询 | `$select` | `"select"` |
| `SelectItem` | Select项，用于 SELECT 列定义 | - | `"selectitem"` |
| `OrderByItem` | SELECT 中的字段排序项 | - | `"orderbyitem"` |
| `Function` | 函数调用表达式 | `$fn` | `"function"` |
| `Foreign` | 外键 EXISTS 表达式 | `$foreign` | `"foreign"` |
| `Lambda` | Lambda 包装表达式（仅用于解析） | - | `"lambda"` |
| `LogicBinary` | 逻辑二元表达式（比较运算） | `$==`, `$!=`, `$>`, `$>=`, `$<`, `$<=` | `"logic"` |
| `And` | 逻辑 AND 表达式组合 | `$and` | `"and"` |
| `Or` | 逻辑 OR 表达式组合 | `$or` | `"or"` |
| `Not` | 逻辑 NOT 表达式 | `!` | `"not"` |
| `ValueBinary` | 值二元表达式（算术或串联） | `$+`, `$-`, `$*`, `$/`, `$%` | `"valuebinary"` |
| `ValueSet` | 值集合表达式（用于 IN 或 CONCAT） | - | `"valueset"` |
| `Unary` | 一元表达式（如 DISTINCT, -a 等） | - | `"unary"` |
| `Property` | 属性（列）引用表达式 | `#` | `"property"` |
| `Value` | 值表达式 | `@`（变量）或直接值（常量） | `"value"` |
| `GenericSql` | 通过委托或注册生成的 SQL 片段 | - | `"genericsql"` |
| `Update` | 更新片段，表示 UPDATE 语句 | `$update` | `"update"` |
| `Delete` | 删除片段，表示 DELETE 语句 | `$delete` | `"delete"` |
| `Where` | 筛选片段，表示 WHERE 条件 | `$where` | `"where"` |
| `GroupBy` | 分组片段，表示 GROUP BY 子句 | `$groupby` | `"groupby"` |
| `OrderBy` | 排序片段，表示 ORDER BY 子句 | `$orderby` | `"orderby"` |
| `Having` | Having 片段，表示 HAVING 条件 | `$having` | `"having"` |
| `Section` | 分页片段，表示 LIMIT/OFFSET 子句 | `$section` | `"section"` |

## 2. 简洁模式 vs 正常模式对比

### 2.1 属性引用（PropertyExpr）

**简洁模式：**
```json
{"#": "Name"}
{"#": "u.Name"}
```

**正常模式：**
```json
{
  "$": "property",
  "PropertyName": "Name",
  "TableAlias": null
}
```

### 2.2 值表达式（ValueExpr）

**简洁模式 - IsConst=true（常量值）：**
```json
42
"hello"
```

**简洁模式 - IsConst=false（变量值）：**
```json
{"@": 42}
{"@": "variableName"}
```

**正常模式 - IsConst=true（常量值）：**
```json
{
  "$": "value",
  "Value": 42,
  "IsConst": true
}
```

**正常模式 - IsConst=false（变量值）：**
```json
{
  "$": "value",
  "Value": 42,
  "IsConst": false
}
```

### 2.3 逻辑二元表达式（LogicBinaryExpr）

**简洁模式：**
```json
{
  "$": "==",
  "Left": {"#": "Age"},
  "Right": {"@": 18}
}
```

**正常模式：**
```json
{
  "$": "logic",
  "Operator": 0,
  "Left": {
    "$": "property",
    "PropertyName": "Age",
    "TableAlias": null
  },
  "Right": {
    "$": "value",
    "Value": 18,
    "IsConst": false
  }
}
```

### 2.4 AND 表达式（AndExpr）

**简洁模式：**
```json
{
  "$and": [
    {"$": "==", "Left": {"#": "Status"}, "Right": {"@": "Pending"}},
    {"$": ">=", "Left": {"#": "TotalAmount"}, "Right": {"@": 300}}
  ]
}
```

**正常模式：**
```json
{
  "$": "and",
  "Items": [
    {
      "$": "logic",
      "Operator": 0,
      "Left": {"$": "property", "PropertyName": "Status"},
      "Right": {"$": "value", "Value": "Pending", "IsConst": false}
    },
    {
      "$": "logic",
      "Operator": 3,
      "Left": {"$": "property", "PropertyName": "TotalAmount"},
      "Right": {"$": "value", "Value": 300, "IsConst": false}
    }
  ]
}
```

### 2.5 NOT 表达式（NotExpr）

**简洁模式：**
```json
{
  "!": {"$": "==", "Left": {"#": "IsActive"}, "Right": {"@": false}}
}
```

**正常模式：**
```json
{
  "$": "not",
  "Operand": {
    "$": "logic",
    "Operator": 0,
    "Left": {"$": "property", "PropertyName": "IsActive"},
    "Right": {"$": "value", "Value": false, "IsConst": false}
  }
}
```

### 2.6 SQL 片段（SqlSegment）

**简洁模式 - TableExpr：**
```json
{"$table": "LiteOrm.Tests.Models.TestUser"}
```

**简洁模式 - TableExpr 带参数：**
```json
{
  "$table": {
    "$": "LiteOrm.Tests.Models.TestUser",
    "TableArgs": ["2024", "01"],
    "Alias": "u"
  }
}
```

**正常模式 - TableExpr：**
```json
{
  "$": "table",
  "Type": "LiteOrm.Tests.Models.TestUser",
  "TableArgs": ["2024", "01"],
  "Alias": "u"
}
```

**说明：** `FromExpr` 的简洁模式使用 `$from` 作为标记，内部直接包含 `TableExpr`（`$table`）和 `Joins`。

**简洁模式 - FromExpr：**
```json
{
  "$from": {
    "$table": {
      "$": "LiteOrm.Tests.Models.TestUser",
      "TableArgs": ["2024", "01"],
      "Alias": "u"
    }
  },
  "Joins": []
}
```

**正常模式 - FromExpr：**
```json
{
  "$": "from",
  "Source": {
    "$": "table",
    "Type": "LiteOrm.Tests.Models.TestUser",
    "TableArgs": ["2024", "01"],
    "Alias": "u"
  },
  "Joins": []
}
```

### 2.7 WHERE 表达式（WhereExpr）

**简洁模式：**
```json
{
  "$where": {"$from": {"$table": "LiteOrm.Tests.Models.TestUser"}},
  "Where": {
    "$and": [
      {"$": "==", "Left": {"#": "Status"}, "Right": {"@": "Pending"}},
      {"$": ">=", "Left": {"#": "TotalAmount"}, "Right": {"@": 300}}
    ]
  }
}
```

**正常模式：**
```json
{
  "$": "where",
  "Source": {
    "$": "from",
    "Type": {"$": "LiteOrm.Tests.Models.TestUser"}
  },
  "Where": {
    "$": "and",
    "Items": [
      {"$": "logic", "Operator": 0, "Left": {"#": "Status"}, "Right": {"@": "Pending"}},
      {"$": "logic", "Operator": 3, "Left": {"#": "TotalAmount"}, "Right": {"@": 300}}
    ]
  }
}
```

### 2.8 ORDER BY 表达式（OrderByExpr）

**简洁模式：**
```json
{
  "$orderby": {
    "$where": {"$from": {"$table": "LiteOrm.Tests.Models.TestUser"}}
  },
  "OrderBys": [
    {"Field": {"#": "TotalAmount"}, "Asc": false},
    {"Field": {"#": "CreatedTime"}, "Asc": false}
  ]
}
```

**正常模式：**
```json
{
  "$": "orderby",
  "Source": {...},
  "OrderBys": [
    {
      "$": "orderbyitem",
      "Field": {"$": "property", "PropertyName": "TotalAmount"},
      "Ascending": false
    },
    {
      "$": "orderbyitem",
      "Field": {"$": "property", "PropertyName": "CreatedTime"},
      "Ascending": false
    }
  ]
}
```

### 2.9 分页表达式（SectionExpr）

**简洁模式：**
```json
{
  "$section": {
    "$orderby": {...},
    "Skip": 0,
    "Take": 10
  }
}
```

## 3. 完整查询示例

### 简洁模式

```json
{
  "$section": {
    "$orderby": {
      "$where": {"$from": {"$table": "LiteOrm.Tests.Models.TestUser"}},
      "Where": {
        "$and": [
          {"$": "==", "Left": {"#": "Status"}, "Right": {"@": "Pending"}},
          {"$": ">=", "Left": {"#": "TotalAmount"}, "Right": {"@": 300}},
          {"$": "contains", "Left": {"#": "DepartmentName"}, "Right": {"@": "Operations"}}
        ]
      }
    }
  },
  "OrderBys": [
    {"Field": {"#": "TotalAmount"}, "Asc": false},
    {"Field": {"#": "CreatedTime"}, "Asc": false}
  ],
  "Skip": 0,
  "Take": 5
}
```

### 正常模式

```json
{
  "$": "section",
  "Source": {
    "$": "orderby",
    "Source": {
      "$": "where",
      "Source": {"$": "from", "Type": {"$": "LiteOrm.Tests.Models.Order"}},
      "Where": {
        "$": "and",
        "Items": [
          {
            "$": "logic",
            "Operator": 0,
            "Left": {"$": "property", "PropertyName": "Status"},
            "Right": {"$": "value", "Value": "Pending", "IsConst": false}
          },
          {
            "$": "logic",
            "Operator": 3,
            "Left": {"$": "property", "PropertyName": "TotalAmount"},
            "Right": {"$": "value", "Value": 300, "IsConst": false}
          },
          {
            "$": "logic",
            "Operator": 11,
            "Left": {"$": "property", "PropertyName": "DepartmentName"},
            "Right": {"$": "value", "Value": "Operations", "IsConst": false}
          }
        ]
      }
    },
    "OrderBys": [
      {
        "$": "orderbyitem",
        "Field": {"$": "property", "PropertyName": "TotalAmount"},
        "Ascending": false
      },
      {
        "$": "orderbyitem",
        "Field": {"$": "property", "PropertyName": "CreatedTime"},
        "Ascending": false
      }
    ]
  },
  "Skip": 0,
  "Take": 5
}
```

**说明：**
- 序列化时只能使用简洁模式，无法自定义修改
- 反序列化时简洁模式和正常模式都支持

## 4. 相关链接

- [返回目录](../README.md)
- [表达式扩展](./01-expression-extension.md)
- [前端原生 Expr 查询](../03-advanced-topics/08-frontend-native-expr.md)