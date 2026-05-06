# Expr JSON Serialization Format

LiteOrm's `ExprJsonConverter` is easiest to understand when you think in two JSON shapes:

- **short format**: smaller payloads for frontend transport, caching, and persistence
- **normal format**: more explicit fields for reading, debugging, and learning

For learning, this order works well:

1. start with the core markers such as `$`, `#`, `@`, and `$where`
2. move to the side-by-side expression examples
3. finish with the complete query example so the whole chain becomes clear

## 1. Core Marker Reference

### 1.1 Short Format Markers

| Marker | Meaning | Example |
|-------|---------|---------|
| `!` | Negation (Not) | `{"!": {"#": "IsActive"}}` |
| `#` | Property reference | `{"#": "Name"}` or `{"#": "u.Name"}` |
| `$` | Type/operator identifier | `{"$": "table"}` or `{"$": "=="}` |
| `$and` | Logical AND | `{"$and": [...]}` |
| `$or` | Logical OR | `{"$or": [...]}` |
| `@` | Variable value | `{"@": 42}` or `{"@": "hello"}` |

### 1.2 Expr Comparison Operator Mapping

| Expr Comparison Operator | JSON Short Form |
|-------------------------|----------------|
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

### 1.3 ExprType Type Reference

> This table is aligned with the current implementation. Older notes may still show outdated names.

| ExprType | Description | Short Marker | Normal Mode `$` Value |
|----------|-------------|--------------|---------------------|
| `And` | Logic AND expression group | `$and` | `"and"` |
| `Delete` | Delete segment, represents DELETE | `$delete` | `"delete"` |
| `Foreign` | Foreign-key EXISTS expression | `$foreign` | `"foreign"` |
| `From` | From segment, represents a data source | `$from` | `"from"` |
| `Function` | Function call expression | `{"$":"func","ActualFunctionName":[...]}` | `"func"` |
| `GenericSql` | SQL fragment generated via delegate or registration | - | `"sql"` |
| `GroupBy` | Group-by segment, represents GROUP BY | `$group` | `"group"` |
| `Having` | Having segment, represents HAVING | `$having` | `"having"` |
| `Lambda` | Lambda will be parsed into internal expressions | - | - |
| `LogicBinary` | Logic binary expression (comparisons) |  `"$":"=="`, `"$":"!="`, `"$":">"`, `"$":">="`, `"$":"<"`, `"$":"<="`, `"$":"in"`...  | `"logic"` |
| `Not` | Logic NOT expression | `!` | `"not"` |
| `Or` | Logic OR expression group | `$or` | `"or"` |
| `OrderBy` | Order-by segment, represents ORDER BY | `$orderby` | `"orderby"` |
| `OrderByItem` | ORDER BY item | - | `"orderbyitem"` |
| `Property` | Property (column) reference expression | `#` | `"prop"` |
| `Section` | Pagination segment, represents LIMIT/OFFSET | `$section` | `"section"` |
| `Select` | Select segment, represents a SELECT query | `$select` | `"select"` |
| `SelectItem` | Select item, for SELECT column definition | - | `"selectitem"` |
| `Table` | Table segment, represents a single table or subquery | `$table` | `"table"` |
| `TableJoin` | Table join segment, represents a JOIN clause | `$join` | `"join"` |
| `Unary` | Unary expression (for example DISTINCT or -a) | - | `"unary"` |
| `Update` | Update segment, represents UPDATE | `$update` | `"update"` |
| `Value` | Value expression | `@` (variable) or direct value (const) | `"value"` |
| `ValueBinary` | Value binary expression (arithmetic or concat) |`"$":"+"`,`"$":"-"`,`"$":"*"`, `"$":"/`, `"$":"%"`, `"$":"||"` | `"bin"` |
| `ValueSet` | Value-set expression (for IN or CONCAT) | - | `"set"` |
| `Where` | Filter segment, represents WHERE | `$where` | `"where"` |

## 2. Short Format vs Normal Format Comparison

Use the examples below with one simple rule in mind:

- **short format** is the compact transport shape
- **normal format** is the expanded reading shape
- runtime serialization still defaults to the short format

### 2.1 Property Reference (PropertyExpr)

**Short Format:**

```json
{"#": "Name"}
{"#": "u.Name"}
```

**Normal Format:**

```json
{
  "$": "property",
  "PropertyName": "Name",
  "TableAlias": null
}
```

### 2.2 Value Expression (ValueExpr)

**Short Format - IsConst=true (Constant Value):**

```json
42
"hello"
```

**Short Format - IsConst=false (Variable Value):**

```json
{"@": 42}
{"@": "variableName"}
```

**Normal Format - IsConst=true (Constant Value):**

```json
{
  "$": "value",
  "Value": 42,
  "IsConst": true
}
```

**Normal Format - IsConst=false (Variable Value):**

```json
{
  "$": "value",
  "Value": 42,
  "IsConst": false
}
```

### 2.3 Logic Binary Expression (LogicBinaryExpr)

**Short Format:**

```json
{
  "$": "==",
  "Left": {"#": "Age"},
  "Right": {"@": 18}
}
```

**Normal Format:**

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

### 2.4 AND Expression (AndExpr)

**Short Format:**

```json
{
  "$and": [
    {"$": "==", "Left": {"#": "Status"}, "Right": {"@": "Pending"}},
    {"$": ">=", "Left": {"#": "TotalAmount"}, "Right": {"@": 300}}
  ]
}
```

**Normal Format:**

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

### 2.5 NOT Expression (NotExpr)

**Short Format:**

```json
{
  "!": {"$": "==", "Left": {"#": "IsActive"}, "Right": {"@": false}}
}
```

**Normal Format:**

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

### 2.6 SQL Segment (SqlSegment)

This is usually the hardest part to read because `From -> Where -> OrderBy -> Section` is nested layer by layer. Read the outer marker first, then inspect its `Source` and sibling properties.

**Short Format - TableExpr:**

```json
{"$table": "LiteOrm.Tests.Models.TestUser"}
```

**Short Format - TableExpr with Parameters:**

```json
{
  "$table": "LiteOrm.Tests.Models.TestUser",
  "TableArgs": ["2024", "01"],
  "Alias": "u"
}
```

**Normal Format - TableExpr:**

```json
{
  "$": "table",
  "Type": "LiteOrm.Tests.Models.TestUser",
  "TableArgs": ["2024", "01"],
  "Alias": "u"
}
```

**Note:** `FromExpr` uses `$from` in short format and directly contains `TableExpr` (`$table`) plus `Joins`.

**Short Format - FromExpr:**

```json
{
  "$from": {
    "$table": "LiteOrm.Tests.Models.TestUser",
    "TableArgs": ["2024", "01"],
    "Alias": "u"
  },
  "Joins": []
}
```

**Normal Format - FromExpr:**

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

### 2.7 WHERE Expression (WhereExpr)

**Short Format:**

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

**Normal Format:**

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

### 2.8 ORDER BY Expression (OrderByExpr)

**Short Format:**

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

**Normal Format:**

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

### 2.9 Section Expression (SectionExpr)

**Short Format:**

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

## 3. Complete Query Example

### Short Format

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

### Normal Format

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

**Note:**

- serialization defaults to the short format
- deserialization supports both short format and normal format

## 4. Extra note on `FunctionExpr`

`FunctionExpr` is easy to document incorrectly because its short form is not `$fn`. The current implementation uses the `func` marker, then stores the function name as the property name.

### Short Format

```json
{
  "$": "func",
  "DateDiffDays": [
    {"#": "EndTime"},
    {"#": "StartTime"}
  ]
}
```

### Normal Format

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

### Reading Tip

- in short format, the **function name itself becomes the property name**
- in normal format, the name lives in `FunctionName` and parameters live in `Args`

## Related Links

- [Back to docs hub](../README.md)
- [Expression Extension](./01-expression-extension.en.md)
