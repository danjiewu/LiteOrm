# Query Guide

LiteOrm supports three main query styles: Lambda, `Expr`, and `ExprString`. This guide explains which style to use in which scenario, how to combine them, and how to avoid common pitfalls.

## 1. Which style should I choose?

| Style | Syntax | Best for | Type safety |
|-------|--------|----------|-------------|
| Lambda | `u => u.Age > 18` | Simple conditions, compile-time validation | ✅ Strong |
| `Expr` | `Expr.Prop("Age") > 18` | Complex conditions, dynamic building | ✅ Compile-time |
| `ExprString` | `$"WHERE {expr}"` | Custom SQL fragments | ❌ Runtime |

### 1.1 How to choose

- **Use Lambda by default**: Best for most everyday queries, most intuitive code.
- **Use `Expr` when you need dynamic conditions**: Ideal for admin search screens, multi-condition combinations, query builders.
- **Use `ExprString` only when necessary**: For example, complex SELECTs, database-specific syntax, or temporary SQL fragments.

### 1.2 Common scenarios

| Scenario | Recommended style | Reason |
|----------|-------------------|--------|
| Simple list filtering | Lambda | Best readability, low change cost |
| Admin multi-condition filtering | Expr | Dynamically accumulate conditions by parameters |
| Reports or special SQL fragments | ExprString | Allows partial handwritten SQL |
| Needs extension functions | Lambda + Expr | Keeps type safety while retaining extensibility |

> 💡 Want to learn more about combining Lambda and Expr: See: [Lambda + Expr Mixing](../03-advanced-topics/09-lambda-expr-mixing.md)

## 2. Lambda queries

Lambda queries are the most concise approach with compile-time type checking.

### 2.1 Basic queries

```csharp
// Equals
var users = await userService.SearchAsync(u => u.Age == 18);

// Not equals
var users = await userService.SearchAsync(u => u.Status != 0);

// Greater than
var users = await userService.SearchAsync(u => u.Age > 18);

// Less than or equal
var users = await userService.SearchAsync(u => u.Age <= 65);
```

### 2.2 String matching

```csharp
var users = await userService.SearchAsync(u => u.UserName.Contains("admin"));

var users = await userService.SearchAsync(u => u.UserName.StartsWith("admin"));

var users = await userService.SearchAsync(u => u.UserName.EndsWith("admin"));
```

### 2.3 IN and BETWEEN

```csharp
var users = await userService.SearchAsync(u => new[] {1, 2, 3}.Contains(u.Id));

var users = await userService.SearchAsync(u => u.Age >= 18 && u.Age <= 65);
```

### 2.4 EXISTS

```csharp
var users = await userService.SearchAsync(
    u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "R&D")
);
```

### 2.5 NULL checks

```csharp
var users = await userService.SearchAsync(u => u.DeletedAt == null);

var users = await userService.SearchAsync(u => u.DeletedAt != null);
```

### 2.6 Lambda variable capture

Variables defined outside a Lambda are parameterized into the SQL:

```csharp
var keyword = "admin";  // Defined outside Lambda
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
// Generated SQL: WHERE UserName LIKE @0 (parameterized)
```

> Note: `DateTime` has no constant expression. Writing `DateTime.Now` directly in a Lambda is parsed as the `NOW()` SQL function, not parameterized. If you want a DateTime value to be parameterized, define a variable first:

```csharp
var now = DateTime.Now;  // Defined outside Lambda
var users = await userService.SearchAsync(u => u.CreateTime > now);
```

### 2.7 Combining Lambda and Expr

`Expr` values can be combined with Lambda expressions, but need the `To<T>()` extension method to satisfy Lambda type checking:

```csharp
var condition = u => u.Age >= 18 && Expr.Prop("UserName").Contains("John").To<bool>();
var users = await userService.SearchAsync(condition);
// Generated SQL: SELECT * FROM Users WHERE Age >= 18 AND UserName LIKE @0 (parameterized)
```

> Tip: When you have a dynamically built `Expr.Prop(...)` and want to continue combining with Lambda on the outside, write it as `expr.To<bool>()`. This leverages Expr's dynamic building capability without getting stuck on Lambda's type checking.

For details, see: [Expression Extension - Default Registered Lambda Methods](../04-extensibility/01-expression-extension.md#default-registered-lambda-methods)

## 3. `Expr` queries

`Expr` queries provide more flexible dynamic query capabilities, suitable for complex condition building.

### 3.1 Creating property expressions

```csharp
// Basic property
var prop = Expr.Prop("Age");

// With alias (for multi-table queries)
var prop = Expr.Prop("U", "UserName");
```

### 3.2 Comparison operators

```csharp
var expr = Expr.Prop("Age") > 18;
var expr = Expr.Prop("DeptId") == 2;
var expr = Expr.Prop("UserName") != "admin";
```

### 3.3 String matching

```csharp
var expr = Expr.Prop("UserName").Contains("admin");
var expr = Expr.Prop("UserName").StartsWith("a");
var expr = Expr.Prop("UserName").EndsWith("z");
var expr = Expr.Prop("UserName").Like("%admin%");
```

### 3.4 IN and BETWEEN

```csharp
var expr = Expr.Prop("Id").In(1, 2, 3, 4, 5);

// Subquery
var subQuery = Expr.From<Department>()
    .Where(Expr.Prop("Name") == "IT")
    .Select(Expr.Prop("Id"));
var expr = Expr.Prop("DeptId").In(subQuery);

var expr = Expr.Prop("Age").Between(18, 30);
```

### 3.5 EXISTS subqueries

```csharp
var expr = Expr.Exists<Department>(
    Expr.Prop("Id") == Expr.Prop("T0", "DeptId")
);
```

### 3.6 Combining conditions

```csharp
// AND
var expr = Expr.Prop("Age") >= 18 & Expr.Prop("DeptId") == 2;

// OR
var expr = Expr.Prop("DeptId") == 2 | Expr.Prop("DeptId") == 3;

// NOT
var expr = !Expr.Prop("UserName").StartsWith("Temp");

// Complex combinations with parentheses
var expr = (Expr.Prop("Age") >= 18) & (Expr.Prop("DeptId") == 2 | Expr.Prop("DeptId") == 3);
```

### 3.7 Dynamic condition building

```csharp
LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Expr.Prop("Age") >= minAge.Value;

if (deptId.HasValue)
    condition &= Expr.Prop("DeptId") == deptId.Value;

if (!string.IsNullOrEmpty(name))
    condition &= Expr.Prop("UserName").Contains(name);

var users = await userService.SearchAsync(condition);
```

### 3.8 Complete query building with Expr.From<T>()

Use `Expr.From<T>()` as a starting point for chain-style complete query building:

```csharp
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Having(Expr.Prop("Id").Count() > 5)
    .Select(
        Expr.Prop("DeptId"),
        Expr.Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Expr.Prop("UserCount").Desc());

var result = await userService.SearchAsync(query);
```

### 3.9 Aggregate functions

```csharp
var expr = Expr.Prop("Id").Count();
var expr = Expr.Prop("Amount").Sum();
var expr = Expr.Prop("Amount").Avg();
var expr = Expr.Prop("Amount").Max();
var expr = Expr.Prop("Amount").Min();

// Distinct aggregate
var expr = Expr.Prop("DeptId").Count(isDistinct: true);
```

### 3.10 Expr Static Methods

The `Expr` class provides the following static methods for building expressions:

| Method | Description | Example |
|--------|-------------|---------|
| `Expr.Prop(name)` | Create property expression | `Expr.Prop("Age")` |
| `Expr.Prop(alias, name)` | Create property expression with alias | `Expr.Prop("U", "UserName")` |
| `Expr.Value(value)` | Create parameterized variable expression | `Expr.Value(18)` |
| `Expr.Const(value)` | Create constant expression (embedded directly in SQL) | `Expr.Const("test")` |
| `Expr.Null` | SQL NULL value | `Expr.Null` |
| `Expr.From<T>()` | Create FROM query starting point | `Expr.From<User>()` |
| `Expr.Update<T>()` | Create UPDATE expression | `Expr.Update<User>()` |
| `Expr.Delete<T>()` | Create DELETE expression | `Expr.Delete<User>()` |
| `Expr.Exists<T>(innerExpr)` | Create EXISTS subquery | `Expr.Exists<Department>(Expr.Prop("Id") == Expr.Prop("T0", "DeptId"))` |
| `Expr.ExistsRelated<T>(innerExpr)` | Auto-join EXISTS query | `Expr.ExistsRelated<DepartmentView>(...)` |
| `Expr.Lambda<T>(expr)` | Create LogicExpr from Lambda expression | `Expr.Lambda<User>(u => u.Age > 18)` |
| `Expr.Func(name, args)` | Create function call expression | `Expr.Func("COUNT", Expr.Prop("Id"))` |
| `Expr.If(condition, then, else)` | CASE WHEN expression | `Expr.If(Expr.Prop("Age") > 18, Expr.Value("adult"), Expr.Value("minor"))` |
| `Expr.Case(cases, elseExpr)` | CASE WHEN expression | `Expr.Case(new Dictionary<LogicExpr, ValueTypeExpr>{...}, defaultValue)` |
| `Expr.Aggregate(name, expr, isDistinct)` | Aggregate function wrapper | `Expr.Aggregate("COUNT", Expr.Prop("Id"), true)` |
| `Expr.Now()` | Current timestamp | `Expr.Now()` |
| `Expr.Today()` | Current date | `Expr.Today()` |
| `Expr.Sql(key, arg)` | Dynamic SQL fragment | `Expr.Sql("@0", value)` |
| `Expr.Query<T>(expression)` | Lambda query returning list | `Expr.Query<User>(q => q.Where(u => u.Age > 18))` |
| `Expr.Query<T, TResult>(expression)` | Lambda query returning scalar | `Expr.Query<User, int>(q => q.Select(u => u.Id.Count()))` |

### 3.11 ExprExtensions Methods

`ExprExtensions` provides chain-style extension methods for `ValueTypeExpr` and `LogicExpr`:

**Logic expression combination** (`LogicExpr`):

| Method | Description | Example |
|--------|-------------|---------|
| `&` or `.And(right)` | AND | `Expr.Prop("Age") > 18 & Expr.Prop("DeptId") == 2` |
| `|` or `.Or(right)` | OR | `condition1 | condition2` |
| `!` or `.Not()` | NOT | `!Expr.Prop("IsDeleted").Equal(true)` |

**Comparison operators**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Equal(right)` | Equals | `Expr.Prop("DeptId").Equal(2)` |
| `.NotEqual(right)` | Not equals | `Expr.Prop("Status").NotEqual(0)` |
| `.GreaterThan(right)` | Greater than | `Expr.Prop("Age").GreaterThan(18)` |
| `.LessThan(right)` | Less than | `Expr.Prop("Age").LessThan(65)` |
| `.GreaterThanOrEqual(right)` | Greater than or equal | `Expr.Prop("Age").GreaterThanOrEqual(18)` |
| `.LessThanOrEqual(right)` | Less than or equal | `Expr.Prop("Age").LessThanOrEqual(65)` |

**Collection operations**:

| Method | Description | Example |
|--------|-------------|---------|
| `.In(params object[])` | IN collection (params) | `Expr.Prop("Id").In(1, 2, 3)` |
| `.In(IEnumerable)` | IN collection (enumerable) | `Expr.Prop("Id").In(ids)` |
| `.In(Expr)` | IN subquery | `Expr.Prop("DeptId").In(subQuery)` |
| `.Between(low, high)` | BETWEEN range | `Expr.Prop("Age").Between(18, 65)` |

**String matching**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Like(pattern)` | LIKE pattern match | `Expr.Prop("Name").Like("J%")` |
| `.Contains(text)` | Contains | `Expr.Prop("UserName").Contains("admin")` |
| `.StartsWith(text)` | Starts with | `Expr.Prop("UserName").StartsWith("admin")` |
| `.EndsWith(text)` | Ends with | `Expr.Prop("UserName").EndsWith("admin")` |
| `.RegexpLike(pattern)` | Regex match | `Expr.Prop("Name").RegexpLike("^[A-Z]")` |

**Alias and conversion**:

| Method | Description | Example |
|--------|-------------|---------|
| `.As(name)` | Alias | `Expr.Prop("Id").As("UserId")` |
| `.Distinct()` | DISTINCT | `Expr.Prop("DeptId").Distinct()` |

**NULL checks**:

| Method | Description | Example |
|--------|-------------|---------|
| `.IsNull()` | IS NULL | `Expr.Prop("DeletedAt").IsNull()` |
| `.IsNotNull()` | IS NOT NULL | `Expr.Prop("DeptId").IsNotNull()` |
| `.IfNull(defaultValue)` | NULL replacement | `Expr.Prop("NickName").IfNull(Expr.Prop("UserName"))` |

**Aggregate functions**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Count()` | COUNT aggregate | `Expr.Prop("Id").Count()` |
| `.Count(isDistinct)` | COUNT distinct | `Expr.Prop("DeptId").Count(true)` |
| `.Sum()` | SUM aggregate | `Expr.Prop("Salary").Sum()` |
| `.Avg()` | AVG aggregate | `Expr.Prop("Score").Avg()` |
| `.Max()` | MAX aggregate | `Expr.Prop("Price").Max()` |
| `.Min()` | MIN aggregate | `Expr.Prop("Price").Min()` |

**Ordering**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Asc()` | Ascending | `Expr.Prop("CreateTime").Asc()` |
| `.Desc()` | Descending | `Expr.Prop("CreateTime").Desc()` |

**Window functions**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Over(partitionBy)` | OVER PARTITION BY | `Expr.Prop("Salary").Sum().Over(Expr.Prop("DeptId"))` |

**SQL building (chain-style)**:

| Method | Description | Example |
|--------|-------------|---------|
| `.Where(condition)` | WHERE clause | `fromExpr.Where(Expr.Prop("Age") > 18)` |
| `.GroupBy(props)` | GROUP BY clause | `fromExpr.GroupBy("DeptId")` |
| `.Having(condition)` | HAVING clause | `groupExpr.Having(Expr.Prop("Count").Count() > 5)` |
| `.Select(props)` | SELECT clause | `fromExpr.Select("Id", "UserName")` |
| `.OrderBy(props)` | ORDER BY clause | `fromExpr.OrderBy("CreateTime".Desc())` |
| `.Section(skip, take)` | Pagination clause | `fromExpr.Section(0, 20)` |
| `.Set(assignments)` | UPDATE SET clause | `updateExpr.Set(("Age", Expr.Value(18)))` |

## 4. `ExprString` Interpolated Strings

`ExprString` allows embedding `Expr` objects directly in strings, suitable for custom DAO scenarios.

### 4.1 Basic usage

```csharp
var expr = Expr.Prop("Age") > 18;
var result = dao.Search($"WHERE {expr}").ToListAsync();
```

### 4.2 Parameterized safety

```csharp
int minAge = 18;
var expr = Expr.Prop("Age") > 25;

// Auto-parameterized, prevents SQL injection
var result = dao.Search($"WHERE {expr} AND Age > {minAge}").ToListAsync();
```

### 4.3 DataViewDAO usage

```csharp
var dataTable = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > {minAge}"
).GetResultAsync();
```

### 4.4 Usage boundaries and best practices

- `ExprString` is better for "partial SQL customization". Don't stuff entire complex business SQL into interpolated strings.
- Conditions that can be expressed with `Expr.Prop(...)` and `Expr.Value(...)` should not be handwritten.
- If a certain SQL fragment is reused repeatedly, extract it to a DAO or extension method, rather than copying it throughout business code.

```csharp
// Recommended: Use ExprString only for necessary fragments
var condition = Expr.Prop("Age") >= 18;
var result = await userViewDAO.Search(
    $"WHERE {condition} ORDER BY CreateTime DESC"
).ToListAsync();
```

## 5. Service vs DAO Queries

### 5.1 Service queries

Service layer supports both Lambda and Expr query styles:

```csharp
// Lambda expression query
var users = await userService.SearchAsync(u => u.Age >= 18);

// Expr object query
var users = await userService.SearchAsync(Expr.Prop("Age") >= 18);
```

### 5.2 DAO queries

DAO layer supports all three query styles: Lambda, Expr, and ExprString:

```csharp
// Lambda expression query
var users = await userViewDAO.Search(u => u.Age >= 18).ToListAsync();

// Expr object query
var users = await userViewDAO.Search(Expr.Prop("Age") >= 18).ToListAsync();

// ExprString interpolated string query
var users = await userViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").ToListAsync();
```

## 6. Next steps

- [Back to documentation hub](../README.md)
- [Associations Guide](./05-associations.en.md)
- [CRUD Guide](./04-crud-guide.en.md)
- [Transactions](../03-advanced-topics/01-transactions.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
