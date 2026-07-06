# Expr 使用指南

`Expr` 是 LiteOrm 的核心表达式对象模型，本文主要讲解如何构造、组合、复用和理解它的语义。
如果你关心 Lambda / `Expr` / `ExprString` 的选型，请先看[查询总览](./04-query-overview.md)；Lambda 写法见 [Lambda 查询指南](./05-lambda-guide.md)，DAO 层手写 SQL 见 [ExprString 使用指南](./07-exprstring-guide.md)。

## 1. 创建基础表达式

### 1.1 属性、值与常量

```csharp
using static LiteOrm.Common.Expr;

var age = Prop("Age");
var userName = Prop("U", "UserName");

var paramValue = Value(18);       // 参数化
var constValue = Const("Enabled"); // 直接内嵌
```

- `Prop(name)`：创建属性表达式
- `Prop(alias, name)`：创建带表别名的属性表达式
- `Value(obj)`：按参数传递，适合运行时值
- `Const(obj)`：直接内嵌到 SQL，适合真正的常量

### 1.2 比较、字符串与集合

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("Age") >= 18;
var expr2 = Prop("DeptId").In(1, 2, 3);
var expr3 = Prop("Age").Between(18, 30);
var expr4 = Prop("UserName").Contains("admin");
var expr5 = Prop("UserName").Like("%root%");
```

这些写法都返回 `LogicExpr`，可以继续组合。

### 1.3 函数、聚合与动态 SQL

```csharp
using static LiteOrm.Common.Expr;

var absAge = Func("ABS", Prop("Age"));
var countExpr = Aggregate("COUNT", Prop("Id"), isDistinct: true);
var currentUserFilter = Sql("CurrentUserFilter");
```

- `Func(name, args)`：普通函数
- `Aggregate(name, expr, isDistinct)`：聚合函数包装
- `Sql(key, arg)`：注册式动态 SQL 片段，适合运行时上下文过滤

### 1.4 类型转换与条件值

```csharp
using static LiteOrm.Common.Expr;
using System.Data;

var ageText = Prop("Age").Cast(DbType.String);
var levelExpr = If(Prop("Age") >= 18, Const("Adult"), Const("Minor"));
```

- `.Cast(DbType)`：把值表达式转换为目标数据库类型，对应 SQL `CAST(...)`
- `Expr.If(condition, then, else)`：构造简单条件表达式，等价于 `CASE WHEN condition THEN then ELSE else END`
- `Expr.Case(...)`：构造多条件 CASE 表达式，支持以下重载：
  - `Case((LogicExpr, ValueTypeExpr)[] cases, ValueTypeExpr elseExpr)` - 条件-结果元组数组 + ELSE
  - `Case(params (LogicExpr, ValueTypeExpr)[] cases)` - 条件-结果元组数组（无 ELSE）
  - `Case(params Expr[] cases)` - 交替传入条件和结果表达式，奇数个参数时最后一个为 ELSE
- 在 **Lambda** 查询中，三目运算符 `a ? b : c` 会自动解析成 `Expr.If(...)`，最终生成 `CASE WHEN ... THEN ... ELSE ... END`

示例：

```csharp
using static LiteOrm.Common.Expr;

// 使用元组数组构建 CASE 表达式（推荐）
var ageGroup = Case(
    new[] {
        (Prop("Age") < 18, (ValueTypeExpr)Const("Minor")),
        (Prop("Age") < 30, (ValueTypeExpr)Const("Young")),
        (Prop("Age") < 50, (ValueTypeExpr)Const("Adult"))
    },
    Const("Senior")  // ELSE 分支
);

// 无 ELSE 分支
var ageGroupNoElse = Case(
    (Prop("Age") < 18, Const("Minor")),
    (Prop("Age") < 30, Const("Young"))
);

// 使用交替参数形式
var levelExpr = Case(
    Prop("Score") >= 90, Const("A"),
    Prop("Score") >= 80, Const("B"),
    Prop("Score") >= 60, Const("C"),
    Const("D")  // ELSE 分支（参数为奇数个时最后一个为 ELSE）
);
```

## 2. 子查询与关联过滤

### 2.1 显式 `Exists`

Lambda 写法：

```csharp
var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "研发中心")
);
```

Expr 写法：

```csharp
using static LiteOrm.Common.Expr;

var expr = Exists<Department>(
    Prop("Id") == Prop("T0", "DeptId")
    & Prop("Name") == "研发中心"
);
```

这类写法适合你想**自己明确写出关联条件**的场景。

### 2.2 自动关联 `ExistsRelated`

Lambda 写法：

```csharp
var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "研发中心")
);
```

Expr 写法：

```csharp
using static LiteOrm.Common.Expr;

var expr = ExistsRelated<DepartmentView>(
    Prop("Name") == "研发中心"
);
```

`ExistsRelated` 会根据 `ForeignType` / `TableJoin` 等元数据自动补关联条件。  
详细匹配逻辑请看[关联查询](./08-associations.md)。

## 3. 动态拼装 Expr

### 3.1 按参数累加条件

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;

if (minAge.HasValue)
    condition &= Prop("Age") >= minAge.Value;

if (deptId.HasValue)
    condition &= Prop("DeptId") == deptId.Value;

if (!string.IsNullOrWhiteSpace(keyword))
    condition &= Prop("UserName").Contains(keyword);
```

`&` / `|` 对 `null` 友好，非常适合做后台筛选器。

> 旧版 `AndIf`、`OrIf`、`WhereIf`、`SetIf` 已移除。现在推荐直接使用 `if` 配合 `&` / `|` 的空值友好语义，或按条件控制链式调用是否追加。

### 3.2 从 QueryString / Dictionary 构造

```csharp
using static LiteOrm.Common.Expr;

public static LogicExpr BuildUserSearch(IReadOnlyDictionary<string, string?> query)
{
    LogicExpr condition = null;

    if (query.TryGetValue("minAge", out var minAgeText) && int.TryParse(minAgeText, out var minAge))
        condition &= Prop("Age") >= minAge;

    if (query.TryGetValue("keyword", out var keyword) && !string.IsNullOrWhiteSpace(keyword))
        condition &= Prop("UserName").Contains(keyword);

    return condition;
}
```

这类写法适合开放查询接口、网关转发和前端条件构造器。

补充说明：这类字符串入口里的属性名、`orderby` 字段名等，按模型属性匹配时通常**忽略大小写**。

### 3.3 和 Lambda 组合使用

```csharp
using static LiteOrm.Common.Expr;

LogicExpr extra = null;
extra &= Prop("UserName").Contains("John");

var users = await userService.SearchAsync(
    u => u.IsActive == true && extra.To<bool>()
);
```

如果你想保持 Lambda 的业务可读性，同时又想复用动态 Expr，请继续阅读：[Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)。

## 4. 用 `Expr.From<T>()` 链式构建查询

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .Where(Prop("Age") > 18)
    .GroupBy(Prop("DeptId"))
    .Having(Prop("Id").Count() > 5)
    .Select(
        Prop("DeptId"),
        Prop("Id").Count().As("UserCount")
    )
    .OrderBy(Prop("UserCount").Desc())
    .Section(0, 20);
```

这是 `Expr` 最完整的用法：从 `FROM` 起点一路构造 `WHERE / GROUP BY / HAVING / SELECT / ORDER BY / 分页`。

## 5. Expr 类型总览

可以把 LiteOrm 里的 `Expr` 大致分成四层：

| 层级 | 代表类型 | 说明 |
|------|----------|------|
| 根类型 | `Expr` | 所有表达式对象的共同基类 |
| 值表达式 | `ValueTypeExpr` | 能出现在列、函数、比较两侧、SELECT 项里的值 |
| 逻辑表达式 | `LogicExpr` | 能出现在 WHERE / HAVING / EXISTS 条件里的布尔表达式 |
| SQL 片段 | `SqlSegment` | `FROM / SELECT / WHERE / ORDER BY` 这一类链式 SQL 节点 |

### 5.1 ValueTypeExpr 体系

- `ValueExpr`：值或参数
- `PropertyExpr`：列引用
- `FunctionExpr`：函数调用
- `ValueBinaryExpr`：值运算，如 `a + b`
- `UnaryExpr`：一元运算，如 `-a`、`DISTINCT a`
- `ValueSet`：值集合，如 `IN (...)`、拼接参数集
- `SelectItemExpr`：`SELECT xxx AS Alias`
- `OrderByItemExpr`：`ORDER BY xxx ASC/DESC`

### 5.2 LogicExpr 体系

- `LogicBinaryExpr`：比较表达式，如 `Age >= 18`
- `AndExpr`：AND 组合
- `OrExpr`：OR 组合
- `NotExpr`：NOT 组合
- `ForeignExpr`：`Exists` / `ExistsRelated` 对应的 EXISTS 子查询表达式
- `LambdaExpr`：Lambda 转换过程中的包装表达式，通常不需要手写

### 5.3 SqlSegment 体系

- `SourceExpr`：可作为数据源的 SQL 片段抽象基类
- `TableExpr`：表
- `CommonTableExpr`：CTE
- `TableJoinExpr`：JOIN
- `FromExpr`：FROM
- `SelectExpr`：SELECT
- `WhereExpr`：WHERE
- `GroupByExpr`：GROUP BY
- `HavingExpr`：HAVING
- `OrderByExpr`：ORDER BY
- `SectionExpr`：分页

### 5.4 直接挂在 Expr 下的语句表达式

- `UpdateExpr`：UPDATE
- `DeleteExpr`：DELETE

如果你只是写业务查询，通常最常接触的是：

- `PropertyExpr` / `ValueExpr`
- `LogicBinaryExpr` / `AndExpr` / `OrExpr`
- `ForeignExpr`
- `SelectExpr` / `WhereExpr` / `OrderByExpr`

## 6. Expr 静态方法速查

| 方法 | 说明 | 示例 |
|------|------|------|
| `Expr.Prop(name)` | 创建属性表达式 | `Expr.Prop("Age")` |
| `Expr.Prop(alias, name)` | 创建带别名的属性表达式 | `Expr.Prop("U", "UserName")` |
| `Expr.Value(value)` | 创建参数化值 | `Expr.Value(18)` |
| `Expr.Const(value)` | 创建常量值 | `Expr.Const("Enabled")` |
| `Expr.Null` | SQL NULL | `Expr.Null` |
| `Expr.From<T>()` | 创建链式查询起点 | `Expr.From<User>()` |
| `Expr.Update<T>()` | 创建 UPDATE 表达式 | `Expr.Update<User>()` |
| `Expr.Delete<T>()` | 创建 DELETE 表达式 | `Expr.Delete<User>()` |
| `Expr.Exists<T>(innerExpr)` | 创建 EXISTS 子查询 | `Expr.Exists<Department>(...)` |
| `Expr.ExistsRelated<T>(innerExpr)` | 创建自动关联 EXISTS 子查询 | `Expr.ExistsRelated<DepartmentView>(...)` |
| `Expr.Lambda<T>(expr)` | 将 Lambda 转成 `LogicExpr` | `Expr.Lambda<User>(u => u.Age > 18)` |
| `Expr.Func(name, args)` | 创建函数表达式 | `Expr.Func("COUNT", Expr.Prop("Id"))` |
| `Expr.Aggregate(name, expr, isDistinct)` | 创建聚合函数表达式 | `Expr.Aggregate("COUNT", Expr.Prop("Id"), true)` |
| `Expr.If(condition, then, else)` | IF / CASE WHEN 形式 | `Expr.If(... )` |
| `Expr.Case(cases, elseExpr)` | CASE 表达式 | `Expr.Case(... )` |
| `Expr.Now()` | 当前时间戳 | `Expr.Now()` |
| `Expr.Today()` | 当前日期 | `Expr.Today()` |
| `Expr.Sql(key, arg)` | 动态 SQL 片段 | `Expr.Sql("CurrentUserFilter")` |
| `Expr.Query<T>(expression)` | IQueryable Lambda 转 Expr | `Expr.Query<User>(...)` |
| `Expr.Query<T, TResult>(expression)` | 带返回值的 IQueryable Lambda 转 Expr | `Expr.Query<User, int>(...)` |

## 7. 运算符重载与隐式类型转换

### 7.1 运算符重载速览

LiteOrm 为 `ValueTypeExpr` 和 `LogicExpr` 提供了常用 C# 运算符重载，因此很多写法看起来和普通表达式非常接近：

| 运算符 | 适用类型 | 返回类型 | 示例 |
|------|------|------|------|
| `==` `!=` `>` `<` `>=` `<=` | `ValueTypeExpr` | `LogicExpr` | `Prop("Age") >= 18` |
| `+` `-` `*` `/` `%` | `ValueTypeExpr` | `ValueTypeExpr` | `Prop("Amount") * 0.9m` |
| 一元 `-` / `~` | `ValueTypeExpr` | `ValueTypeExpr` | `-Prop("Balance")`、`~Prop("Flags")` |
| `&` `\|` | `LogicExpr` | `LogicExpr` | `(Prop("Age") >= 18) & (Prop("Status") == 1)` |
| `!` | `LogicExpr` | `LogicExpr` | `!(Prop("IsDeleted") == true)` |

例如：

```csharp
using static LiteOrm.Common.Expr;

var scoreExpr = (Prop("MathScore") + Prop("ExtraScore")) / 2;
var filter = (Prop("Age") >= 18 & Prop("Status") == 1)
           | Prop("UserName").Contains("admin");
```

#### 字符串拼接：不要用 `+`，用 `.Concat(...)`

在手写 Expr 时，`ValueTypeExpr` 的 `+` 是“加法”语义，最终 SQL 可能生成 `+`，对字符串拼接并不跨数据库可靠。

推荐显式使用 concat：

```csharp
using static LiteOrm.Common.Expr;

var fullName = Prop("FirstName")
    .Concat(" ")
    .Concat(Prop("LastName"));
```

`Concat(...)` 会走底层 `SqlBuilder.BuildConcatSql`，由不同数据库方言输出 `CONCAT(a,b,...)` 或 `a || b`。

> 注意：在 **Lambda** 场景下（例如 `SearchAsync(u => u.FirstName + " " + u.LastName == "..." )`），C# 的字符串 `+` 通常会在解析阶段被转换为 concat；但手写 Expr 时请显式使用 `.Concat(...)`。

### 7.2 `LogicExpr` 的空值友好组合

`LogicExpr` 的 `&` / `|` 特别适合做动态筛选器，因为它们对 `null` 友好：

```csharp
using static LiteOrm.Common.Expr;

LogicExpr condition = null;
condition &= Prop("Age") >= 18;
condition &= Prop("Status") == 1;
condition |= Prop("IsVip") == true;
```

规则是：

- `null & expr` => `expr`
- `expr & null` => `expr`
- `null | expr` => `expr`
- `expr | null` => `expr`

这样可以避免每次拼接条件时手动判断“当前条件是否为空”。

### 7.3 标量的隐式类型转换

`ValueTypeExpr` / `ValueExpr` 支持以下标量自动转换：

- `string`
- `int`
- `long`
- `bool`
- `DateTime`
- `double`
- `decimal`

因此你可以直接写：

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("Age") >= 18;
var expr2 = Prop("CreateTime") >= DateTime.Today;
var expr3 = Prop("IsEnabled") == true;
var expr4 = Prop("Amount") + 12.5m;
```

这些字面量会自动变成 `ValueExpr`，通常等价于显式写法：

```csharp
Prop("Age") >= Value(18)
Prop("CreateTime") >= Value(DateTime.Today)
```

### 7.4 这些隐式值默认是参数，不是 SQL 内嵌常量

运算符重载里出现的普通值默认会走参数化，也就是 `Expr.Value(...)` 语义，而不是 `Expr.Const(...)`：

```csharp
using static LiteOrm.Common.Expr;

var expr = Prop("Status") == 1;      // 参数化
var constExpr = Prop("Status") == Const(1); // 常量内嵌
```

选择建议：

- **运行时值**：优先直接写字面量，或显式用 `Value(...)`
- **确实要把值直接写进 SQL**：显式使用 `Const(...)`

### 7.5 与 `null` 比较

`ValueTypeExpr` 是引用类型，`null` 可以直接作为 `operator ==` / `!=` 的右操作数传入，因此 `Prop("DeletedTime") == null` 是合法的——它会构造出一个右操作数为 `null` 引用的 `LogicBinaryExpr`。

SQL 转换层对 `Equal`/`NotEqual` 与 `null` 的组合做了特殊处理：当某一侧为 `null` 引用，或是 `ValueExpr` 且其 `Value` 为 `null` 时，会输出 `IS NULL` / `IS NOT NULL`，而不会输出 `= NULL`（后者在 SQL 中恒为假）。因此下面三种写法在生成结果上等价：

```csharp
using static LiteOrm.Common.Expr;

var expr1 = Prop("DeletedTime") == null;        // 生成 [DeletedTime] IS NULL
var expr2 = Prop("DeletedTime") == Expr.Null;   // 同上
var expr3 = Prop("DeletedTime").IsNull();       // 同上，语义最清晰
```

> **建议**：空判断优先使用 `.IsNull()` / `.IsNotNull()`，意图最直白；`== null` / `== Expr.Null` 虽然能工作，但阅读时需要了解转换层的特殊处理。

### 7.6 其它常见隐式转换

除了标量值，LiteOrm 还提供了一些“为了链式 API 更顺手”的隐式转换：

```csharp
using static LiteOrm.Common.Expr;

var query = From<User>()
    .OrderBy(("Age", false)); // (string property, bool ascending) -> OrderByItemExpr

var update = Update<User>()
    .Set((Prop("Age"), Prop("Age") + 1)); // (PropertyExpr, ValueTypeExpr) -> SetItem
```

这类转换的价值在于减少样板代码，让 `OrderBy(...)`、`Set(...)` 等 API 更接近自然写法。

> **建议**：混合比较、算术和逻辑运算时，尽量加上括号，避免依赖 C# 运算符优先级去猜测最终表达式结构。

### 7.7 Lambda 三目运算符

在 Lambda 查询里，可以直接写 C# 三目表达式：

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

LiteOrm 会把它解析成 `Expr.If(...)`，并进一步生成 SQL `CASE` 表达式。

## 8. ExprExtensions 速查

### 8.1 逻辑组合

| 方法 | 说明 | 示例 |
|------|------|------|
| `&` / `.And(right)` | AND | `Prop("Age") > 18 & Prop("DeptId") == 2` |
| \| / `.Or(right)` | OR | `cond1.Or(cond2)` |
| `!` / `.Not()` | NOT | `!Prop("IsDeleted").Equal(true)` |

> 三种运算符均可写成符号形式或方法形式；逻辑组合建议优先使用 `.And(...)` / `.Or(...)` / `.Not()`，可读性更好，也避免在文档/字符串中转义 `|`。

### 8.2 比较与集合

| 方法 | 说明 |
|------|------|
| `.Equal(v)` `.NotEqual(v)` | 等于 / 不等于 |
| `.GreaterThan(v)` `.LessThan(v)` | 大于 / 小于 |
| `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` | 大于等于 / 小于等于 |
| `.In(params items)` `.In(IEnumerable)` `.In(Expr)` | IN 集合 / 子查询 |
| `.Between(low, high)` | BETWEEN |

### 8.3 字符串与 NULL

| 方法 | 说明 |
|------|------|
| `.Like(pattern)` | LIKE |
| `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` | 常见字符串匹配 |
| `.RegexpLike(pattern)` | 正则匹配 |
| `.IsNull()` `.IsNotNull()` | NULL 检查 |
| `.IfNull(defaultValue)` | 空值替换 |
| `.Cast(DbType)` | 转换为目标数据库类型 |

补充说明：`Contains` / `StartsWith` / `EndsWith` / `Like` 仍会做参数化与通配符转义，但只有在模式字符串确实包含需要转义的特殊字符时，才会生成 `ESCAPE` 片段。

### 8.4 别名、聚合、排序

| 方法 | 说明 |
|------|------|
| `.As(name)` | 生成 `SelectItemExpr` |
| `.Distinct()` | DISTINCT |
| `.Count()` `.Sum()` `.Avg()` `.Max()` `.Min()` | 聚合 |
| `.Asc()` `.Desc()` | 排序 |
| `.Over(partitionBy)` | 窗口函数 |

### 8.5 链式 SQL 构建

| 方法 | 说明 |
|------|------|
| `.Where(condition)` | WHERE |
| `.SelectAll()` | SELECT * |
| `.GroupBy(props)` | GROUP BY |
| `.Having(condition)` | HAVING |
| `.Select(props)` | SELECT |
| `.OrderBy(props)` | ORDER BY |
| `.Section(skip, take)` | 分页 |
| `.Set(assignments)` | UPDATE SET |

## 9. Equals 与组合语义

### 9.1 名称和别名比较忽略大小写

`PropertyExpr`、`TableExpr`、`ForeignExpr`、`FunctionExpr`、`SelectExpr`、`SelectItemExpr`、`CommonTableExpr`、`GenericSqlExpr` 等表达式，在做 `Equals` / `GetHashCode` 时，**名称与别名按忽略大小写处理**。

例如：

```csharp
Expr.Prop("User", "Name")
Expr.Prop("user", "name")
```

会被视为相等表达式。

### 9.2 `AndExpr` / `OrExpr` 采用 Set 语义

`AndExpr.Items` 与 `OrExpr.Items` 现在按 Set 语义处理：

- 重复条件会被去重
- `Equals` / `GetHashCode` 不再依赖重复分布
- 内部仍保留插入顺序用于遍历、输出和序列化

所以：

```csharp
new AndExpr(a, a, b)
new AndExpr(a, b)
```

在组合语义上等价。

## 10. 检测循环引用

在动态构建 `Expr` 树时，如果不小心将节点的 `Source` 属性指向自身或形成回环，会导致遍历/转换时出现栈溢出。`CycleDetector` 使用 `ExprVisitor` 检测此类循环引用。

### 10.1 基本用法

```csharp
using LiteOrm.Common;

var expr = Expr.Prop("Age") > 18;
bool hasCycle = CycleDetector.HasCycle(expr);   // false
```

### 10.2 API

| 方法 | 返回值 | 说明 |
|------|--------|------|
| `CycleDetector.HasCycle(Expr root)` | `bool` | 是否存在循环引用 |
| `CycleDetector.FindCycle(Expr root)` | `Expr` | 返回造成循环的节点，无循环返回 `null` |
| `CycleDetector.Detect(Expr root)` | `CycleResult` | 返回包含 `CycleNode` 和 `Path` 的详细结果 |

### 10.3 使用 Detect 获取详细路径

```csharp
var result = CycleDetector.Detect(someExpr);
if (result.HasCycle)
{
    Console.WriteLine($"检测到循环引用，触发节点: {result.CycleNode.ExprType}");
    Console.WriteLine("从根到循环节点的路径:");
    foreach (var node in result.Path)
    {
        Console.WriteLine($"  → {node.ExprType}");
    }
}
```

`CycleResult.Path` 记录了从根节点到循环节点（第二次出现）的完整路径，路径末尾为重复节点自身，可用于快速定位循环位置。

### 10.4 典型循环场景

```csharp
// 场景 1：直接自引用（Source 指向自身）
var where = new WhereExpr();
where.Source = where;                 // 自引用
where.Where = Expr.Prop("Age") > 18;
// CycleDetector.HasCycle(where) → true

// 场景 2：间接回环（A → B → A）
var whereA = new WhereExpr();
var whereB = new WhereExpr();
whereA.Source = whereB;
whereB.Source = whereA;
// CycleDetector.HasCycle(whereA) → true

// 场景 3：正常链式结构（无循环）
var query = Expr.From(typeof(User))
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("Name").Asc());
// CycleDetector.HasCycle(query) → false
```

### 10.5 实现原理

`CycleDetector` 实现 `IExprNodeVisitor` 接口，在 `BeginVisit` 时将节点加入路径集合（基于引用相等性），在 `EndVisit` 时从路径中移除。当同一节点在路径集合中再次出现时，判定为循环引用并通过 `CancellationTokenSource.Cancel()` 中断遍历。

> **注意**：检测使用引用相等性（`ReferenceEquals`）而非值相等性，即使两个节点内容相同但引用不同，也不会被误判为循环。

## 11. 相关链接

- [查询总览](./04-query-overview.md)
- [Lambda 查询指南](./05-lambda-guide.md)
- [ExprString 使用指南](./07-exprstring-guide.md)
- [增删改查](./03-crud-guide.md)
- [关联查询](./08-associations.md)
- [Lambda 与 Expr 组合使用](./09-lambda-expr-mixing.md)
- [CTE 指南](./10-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
