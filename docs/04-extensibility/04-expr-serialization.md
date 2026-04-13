# Expr JSON 序列化格式

LiteOrm 的 `ExprJsonConverter` 主要围绕两种 JSON 形状展开：

- **简洁模式**：体积更小，适合前端传输、缓存和持久化。
- **正常模式**：字段名更完整，适合阅读、调试和理解结构。

为了更容易学习，建议按这个顺序阅读：

1. 先看“核心标记说明”，认识 `$ / # / @ / $where` 这些基础符号。
2. 再看“常见表达式对比”，理解属性、值、逻辑表达式和 SQL 片段如何拼接。
3. 最后看“完整查询示例”，把整条查询链路串起来。

## 1. 核心标记说明

### 1.1 简洁模式标记

| 标记 | 含义 | 示例 |
|-----|------|------|
| `!` | 取反（Not） | `{"!": {"#": "IsActive"}}` |
| `#` | 属性引用（Property） | `{"#": "Name"}` 或 `{"#": "u.Name"}` |
| `$` | 类型/操作符标识符 | `{"$": "table"}` 或 `{"$": "=="}` |
| `$and` | 逻辑与 | `{"$and": [...]}` |
| `$or` | 逻辑或 | `{"$or": [...]}` |
| `@` | 变量值 | `{"@": 42}` 或 `{"@": "hello"}` |

### 1.2 Expr 操作符映射

| Expr 比较操作符 | JSON 简洁表示 |
|-----------|-------------|
| `.Contains()` | `contains` |
| `.EndsWith()` | `endswith` |
| `.In()` | `in` |
| `.Like()` | `like` |
| `.NotContains()` | `notcontains` |
| `.NotEndsWith()` | `notendswith` |
| `.NotIn()` | `notin` |
| `.NotLike()` | `notlike` |
| `.RegexpLike()` | `regexp` |
| `.NotRegexpLike()` | `notregexp` |
| `.NotStartsWith()` | `notstartswith` |
| `.StartsWith()` | `startswith` |
| `<` | `<` |
| `<=` | `<=` |
| `<>` | `!=` |
| `=` | `==` |
| `>` | `>` |
| `>=` | `>=` |

### 1.3 ExprType 类型表格

| ExprType | 说明 | 简洁标记 | 正常模式 `$` 值 |
|----------|------|---------|---------------|
| `And` | 逻辑 AND 表达式组合 | `$and` | `"and"` |
| `Delete` | 删除片段，表示 DELETE 语句 | `$delete` | `"delete"` |
| `Foreign` | 外键 EXISTS 表达式 | `$foreign` | `"foreign"` |
| `From` | From 片段，表示数据源（表或视图） | `$from` | `"from"` |
| `Function` | 函数调用表达式 | `{"$":"func","函数名":[...]}` | `"func"` |
| `GenericSql` | 通过委托或注册生成的 SQL 片段 | - | `"sql"` |
| `GroupBy` | 分组片段，表示 GROUP BY 子句 | `$group` | `"group"` |
| `Having` | Having 片段，表示 HAVING 条件 | `$having` | `"having"` |
| `Lambda` | Lambda 会被解析为内部表达式 | - | - |
| `LogicBinary` | 逻辑二元表达式（比较运算） | `"$":"=="`, `"$":"!="`, `"$":">"`, `"$":">="`, `"$":"<"`, `"$":"<="`, `"$":"in"`... | `"logic"` |
| `Not` | 逻辑 NOT 表达式 | `!` | `"not"` |
| `Or` | 逻辑 OR 表达式组合 | `$or` | `"or"` |
| `OrderBy` | 排序片段，表示 ORDER BY 子句 | `$orderby` | `"orderby"` |
| `OrderByItem` | ORDER BY 项 | - | `"orderbyitem"` |
| `Property` | 属性（列）引用表达式 | `#` | `"prop"` |
| `Section` | 分页片段，表示 LIMIT/OFFSET 子句 | `$section` | `"section"` |
| `Select` | 选择片段，表示 SELECT 查询 | `$select` | `"select"` |
| `SelectItem` | Select 项，用于 SELECT 列定义 | - | `"selectitem"` |
| `Table` | 表片段，表示单表或子查询引用 | `$table` | `"table"` |
| `TableJoin` | 表连接片段，表示 JOIN 子句 | `$join` | `"join"` |
| `Unary` | 一元表达式（如 DISTINCT, -a 等） | - | `"unary"` |
| `Update` | 更新片段，表示 UPDATE 语句 | `$update` | `"update"` |
| `Value` | 值表达式 | `@`（变量）或直接值（常量） | `"value"` |
| `ValueBinary` | 值二元表达式（算术或串联） | `"$":"+"`,`"$":"-"`,`"$":"*"`, `"$":"/`, `"$":"%"`, `"$":"||"` | `"bin"` |
| `ValueSet` | 值集合表达式（用于 IN 或 CONCAT） | - | `"set"` |
| `Where` | 筛选片段，表示 WHERE 条件 | `$where` | `"where"` |

## 2. 简洁模式 vs 正常模式对比

阅读下面的示例时，可以先记住一个简单原则：

- **简洁模式**更像“压缩后的传输形状”；
- **正常模式**更像“展开后的阅读形状”；
- 当前默认序列化输出仍以简洁模式为主。

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

这类表达式最容易看花，因为它们通常会一层层把 `From -> Where -> OrderBy -> Section` 串起来。阅读时建议先看最外层标记，再看其 `Source` 或同层属性。

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
    "Source": {
      "$": "table",
      "Type": "LiteOrm.Tests.Models.TestUser"
    }
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
      "Asc": false
    },
    {
      "$": "orderbyitem",
      "Field": {"$": "property", "PropertyName": "CreatedTime"},
      "Asc": false
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
    "OrderBys": [
      {"Field": {"#": "CreatedTime"}, "Asc": false}
    ]
  },
  "Skip": 0,
  "Take": 10
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
      },
      "OrderBys": [
        {"Field": {"#": "TotalAmount"}, "Asc": false},
        {"Field": {"#": "CreatedTime"}, "Asc": false}
      ]
    }
  },
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
      "Source": {
        "$": "from",
        "Source": {
          "$": "table",
          "Type": "LiteOrm.Tests.Models.Order"
        }
      },
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
        "Asc": false
      },
      {
        "$": "orderbyitem",
        "Field": {"$": "property", "PropertyName": "CreatedTime"},
        "Asc": false
      }
    ]
  },
  "Skip": 0,
  "Take": 5
}
```

**说明：**

- 序列化时默认输出简洁模式。
- 反序列化时同时支持简洁模式和正常模式。

## 4. FunctionExpr 额外说明

`FunctionExpr` 是这页里最容易写错的一类，因为它的简洁模式不是 `$fn`，而是直接使用 `func` 标记，再把函数名作为属性名写出来。

### 简洁模式

```json
{
  "$": "func",
  "DateDiffDays": [
    {"#": "EndTime"},
    {"#": "StartTime"}
  ]
}
```

### 正常模式

```json
{
  "$": "func",
  "FunctionName": "DateDiffDays",
  "Args": [
    {"$": "property", "PropertyName": "EndTime"},
    {"$": "property", "PropertyName": "StartTime"}
  ]
}
```

### 阅读提示

- 简洁模式里，**函数名本身就是属性名**。
- 正常模式里，函数名放在 `FunctionName`，参数放在 `Args`。

## 相关链接

- [返回目录](../README.md)
- [表达式扩展](./01-expression-extension.md)
