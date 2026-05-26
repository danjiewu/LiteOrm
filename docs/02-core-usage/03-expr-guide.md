# Expr 使用指南

`Expr` 是 LiteOrm 的核心表达式对象模型，本文主要讲解如何构造、组合、复用和理解它的语义。
如果你关心 Lambda / `Expr` / `ExprString` 的选型，请继续查看[查询指南](./04-query-guide.md)。

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
详细匹配逻辑请看[关联查询](./06-associations.md)。

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

### 3.3 和 Lambda 组合使用

```csharp
using static LiteOrm.Common.Expr;

LogicExpr extra = null;
extra &= Prop("UserName").Contains("John");

var users = await userService.SearchAsync(
    u => u.IsActive == true && extra.To<bool>()
);
```

如果你想保持 Lambda 的业务可读性，同时又想复用动态 Expr，请继续阅读：[Lambda 与 Expr 组合使用](./07-lambda-expr-mixing.md)。

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

## 7. ExprExtensions 速查

### 7.1 逻辑组合

| 方法 | 说明 | 示例 |
|------|------|------|
| `&` / `.And(right)` | AND | `Prop("Age") > 18 & Prop("DeptId") == 2` |
| `|` / `.Or(right)` | OR | `condition1 | condition2` |
| `!` / `.Not()` | NOT | `!Prop("IsDeleted").Equal(true)` |

### 7.2 比较与集合

| 方法 | 说明 |
|------|------|
| `.Equal(v)` `.NotEqual(v)` | 等于 / 不等于 |
| `.GreaterThan(v)` `.LessThan(v)` | 大于 / 小于 |
| `.GreaterThanOrEqual(v)` `.LessThanOrEqual(v)` | 大于等于 / 小于等于 |
| `.In(params items)` `.In(IEnumerable)` `.In(Expr)` | IN 集合 / 子查询 |
| `.Between(low, high)` | BETWEEN |

### 7.3 字符串与 NULL

| 方法 | 说明 |
|------|------|
| `.Like(pattern)` | LIKE |
| `.Contains(text)` `.StartsWith(text)` `.EndsWith(text)` | 常见字符串匹配 |
| `.RegexpLike(pattern)` | 正则匹配 |
| `.IsNull()` `.IsNotNull()` | NULL 检查 |
| `.IfNull(defaultValue)` | 空值替换 |

### 7.4 别名、聚合、排序

| 方法 | 说明 |
|------|------|
| `.As(name)` | 生成 `SelectItemExpr` |
| `.Distinct()` | DISTINCT |
| `.Count()` `.Sum()` `.Avg()` `.Max()` `.Min()` | 聚合 |
| `.Asc()` `.Desc()` | 排序 |
| `.Over(partitionBy)` | 窗口函数 |

### 7.5 链式 SQL 构建

| 方法 | 说明 |
|------|------|
| `.Where(condition)` | WHERE |
| `.GroupBy(props)` | GROUP BY |
| `.Having(condition)` | HAVING |
| `.Select(props)` | SELECT |
| `.OrderBy(props)` | ORDER BY |
| `.Section(skip, take)` | 分页 |
| `.Set(assignments)` | UPDATE SET |

## 8. Equals 与组合语义

### 8.1 名称和别名比较忽略大小写

`PropertyExpr`、`TableExpr`、`ForeignExpr`、`FunctionExpr`、`SelectExpr`、`SelectItemExpr`、`CommonTableExpr`、`GenericSqlExpr` 等表达式，在做 `Equals` / `GetHashCode` 时，**名称与别名按忽略大小写处理**。

例如：

```csharp
Expr.Prop("User", "Name")
Expr.Prop("user", "name")
```

会被视为相等表达式。

### 8.2 `AndExpr` / `OrExpr` 采用 Set 语义

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

## 9. 相关链接

- [查询指南](./04-query-guide.md)
- [增删改查](./05-crud-guide.md)
- [关联查询](./06-associations.md)
- [Lambda 与 Expr 组合使用](./07-lambda-expr-mixing.md)
- [CTE 指南](./08-cte-guide.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
