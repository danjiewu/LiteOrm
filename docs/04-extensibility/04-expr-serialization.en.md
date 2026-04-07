# Expr JSON Serialization Format

LiteOrm's `ExprJsonConverter` supports two JSON serialization formats: **short format** and **normal format**. The short format uses compact markers to reduce JSON size, suitable for frontend transmission and storage. The normal format uses full type names for easier debugging and understanding.

## 1. Core Marker Reference

### 1.1 Short Format Markers

| Marker | Meaning | Example |
|-------|---------|---------|
| `#` | Property reference | `{"#": "Name"}` or `{"#": "u.Name"}` |
| `@` | Variable value | `{"@": 42}` or `{"@": "hello"}` |
| `$` | Type/operator identifier | `{"$": "table"}` or `{"$": "=="}` |
| `!` | Negation (Not) | `{"!": {"#": "IsActive"}}` |
| `$and` | Logical AND | `{"$and": [...]}` |
| `$or` | Logical OR | `{"$or": [...]}` |

### 1.2 SQL Operator Mapping

| SQL Operator | JSON Short Form |
|-------------|---------------|
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

### 1.3 ExprType Type Reference

| ExprType | Description | Short Marker | Normal Mode `$` Value |
|----------|-------------|--------------|---------------------|
| `Table` | Table segment, represents single table or subquery | `$table` | `"table"` |
| `TableJoin` | Table join segment, represents JOIN clause | `$join` | `"join"` |
| `From` | From segment, represents data source (table or view) | `$from` | `"from"` |
| `Select` | Select segment, represents SELECT query | `$select` | `"select"` |
| `SelectItem` | Select item, for SELECT column definition | - | `"selectitem"` |
| `OrderByItem` | ORDER BY field item in SELECT | - | `"orderbyitem"` |
| `Function` | Function call expression | `$fn` | `"function"` |
| `Foreign` | Foreign key EXISTS expression | `$foreign` | `"foreign"` |
| `Lambda` | Lambda wrapper expression (parsing only) | - | `"lambda"` |
| `LogicBinary` | Logic binary expression (comparison) | `$==`, `$!=`, `$>`, `$>=`, `$<`, `$<=` | `"logic"` |
| `And` | Logic AND expression group | `$and` | `"and"` |
| `Or` | Logic OR expression group | `$or` | `"or"` |
| `Not` | Logic NOT expression | `!` | `"not"` |
| `ValueBinary` | Value binary expression (arithmetic or concat) | `$+`, `$-`, `$*`, `$/`, `$%` | `"valuebinary"` |
| `ValueSet` | Value set expression (for IN or CONCAT) | - | `"valueset"` |
| `Unary` | Unary expression (e.g., DISTINCT, -a) | - | `"unary"` |
| `Property` | Property (column) reference expression | `#` | `"property"` |
| `Value` | Value expression | `@` (variable) or direct value (const) | `"value"` |
| `GenericSql` | SQL fragment generated via delegate or registration | - | `"genericsql"` |
| `Update` | Update segment, represents UPDATE statement | `$update` | `"update"` |
| `Delete` | Delete segment, represents DELETE statement | `$delete` | `"delete"` |
| `Where` | Filter segment, represents WHERE condition | `$where` | `"where"` |
| `GroupBy` | Group by segment, represents GROUP BY clause | `$groupby` | `"groupby"` |
| `OrderBy` | Order by segment, represents ORDER BY clause | `$orderby` | `"orderby"` |
| `Having` | Having segment, represents HAVING condition | `$having` | `"having"` |
| `Section` | Pagination segment, represents LIMIT/OFFSET | `$section` | `"section"` |

## 2. Short Format vs Normal Format Comparison

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

**Short Format - TableExpr:**
```json
{"$table": "LiteOrm.Tests.Models.TestUser"}
```

**Short Format - TableExpr with Parameters:**
```json
{
  "$table": {
    "$": "LiteOrm.Tests.Models.TestUser",
    "TableArgs": ["2024", "01"],
    "Alias": "u"
  }
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

**Note:** `FromExpr`'s short format uses `$from` as the marker, internally containing `TableExpr` (`$table`) and `Joins`.

**Short Format - FromExpr:**
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

### 2.9 Section Expression (SectionExpr)

**Short Format:**
```json
{
  "$section": {
    "$orderby": {...},
    "Skip": 0,
    "Take": 10
  }
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

### Normal Format

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

**Note:**
- Serialization only supports short format, cannot be customized
- Deserialization supports both short format and normal format

## Related Links

- [Back to docs hub](../README.md)
- [Expression Extension](./01-expression-extension.en.md)
- [Frontend Native Expr Query](../03-advanced-topics/08-frontend-native-expr.en.md)