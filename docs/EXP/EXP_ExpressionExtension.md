# 表达式扩展

LiteOrm 提供强大的表达式扩展机制，允许注册自定义方法处理器和成员处理器，将 C# 方法/属性转换为数据库 SQL 函数。

## 1. 核心概念

表达式扩展涉及两个关键组件的配合：

| 组件                    | 职责                                    |
| --------------------- | ------------------------------------- |
| `LambdaExprConverter` | 将 C# Lambda 表达式中的方法/属性调用转换为 `Expr` 对象 |
| `SqlBuilder`          | 将 `Expr` 对象转换为具体数据库的 SQL 字符串          |

### 1.1 处理流程

```plain
C# Lambda 表达式
    │
    ▼
LambdaExprConverter.RegisterMethodHandler()
    │  转换为 FunctionExpr / 其他 Expr
    ▼
SqlBuilder.RegisterFunctionSqlHandler()
    │  转换为具体 SQL 函数
    ▼
最终 SQL
```

## 2. LambdaExprConverter 方法

### 2.1 RegisterMethodHandler - 注册方法处理器

```csharp
// 注册全局方法处理器（所有类型）
LambdaExprConverter.RegisterMethodHandler("MethodName", handler);

// 注册特定类型的方法处理器
LambdaExprConverter.RegisterMethodHandler(typeof(DateTime), "Format", handler);
LambdaExprConverter.RegisterMethodHandler(typeof(string), null, handler);  // 处理所有方法
```

**参数说明**：

| 参数           | 类型                                                      | 说明   |
| ------------ | ------------------------------------------------------- | ---- |
| `methodName` | string                                                  | 方法名称 |
| `handler`    | `Func<MethodCallExpression, LambdaExprConverter, Expr>` | 处理逻辑 |

**handler 返回值**：

- 返回 `Expr` 子类对象：`FunctionExpr`、`LogicBinaryExpr` 等
- 返回 `null`：使用默认处理

### 2.2 RegisterMemberHandler - 注册成员处理器

```csharp
// 注册全局成员处理器
LambdaExprConverter.RegisterMemberHandler("PropertyName", handler);

// 注册特定类型的成员处理器
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", handler);
```

## 3. SqlBuilder 方法

### 3.1 RegisterFunctionSqlHandler - 注册函数 SQL 处理器

```csharp
// 为特定 Builder 注册函数处理器
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", (funcName, args) => {
    return $"DATE_FORMAT({args[0].Key}, {args[1].Key})";
});
```

**参数说明**：

| 参数             | 类型                                                         | 说明                                    |
| -------------- | ---------------------------------------------------------- | ------------------------------------- |
| `functionName` | string                                                     | 函数名称，与 `FunctionExpr.FunctionName` 对应 |
| `handler`      | `Func<string, List<KeyValuePair<string, string>>, string>` | SQL 构建逻辑                              |

**args 中的 Key**：

- 已转换为 SQL 片段的表达式字符串
- 如 `"DATE_FORMAT(t.CreateTime, '%Y-%m-%d')"`

## 4. 示例一：日期格式化

### 4.1 定义扩展方法

```csharp
public static class DateTimeExtensions
{
    public static string Format(this DateTime date, string format)
    {
        return date.ToString(format);
    }
}
```

### 4.2 注册方法处理器

```csharp
LambdaExprConverter.RegisterMethodHandler("Format", (node, converter) => {
    var dateExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    var formatExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("DATE_FORMAT", dateExpr, formatExpr);
});
```

### 4.3 注册 SQL 处理器

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", (funcName, args) => {
    if (args.Count != 2)
        throw new ArgumentException("DATE_FORMAT requires 2 arguments");
    return $"DATE_FORMAT({args[0].Key}, {args[1].Key})";
});
```

### 4.4 使用

```csharp
var users = await userService.SearchAsync(
    u => u.CreateTime.Format("yyyy-MM-dd") == "2026-03-31"
);
```

## 5. 示例二：计算属性

### 5.1 定义计算属性

```csharp
public class User
{
    public DateTime BirthDate { get; set; }

    // Age 是计算属性，不存储在数据库
    public int Age => DateTime.Now.Year - BirthDate.Year;
}
```

### 5.2 注册成员处理器

```csharp
LambdaExprConverter.RegisterMemberHandler(typeof(User), "Age", (node, converter) => {
    var userExpr = converter.ConvertInternal(node.Expression) as ValueTypeExpr;
    return new FunctionExpr("YEAR", new FunctionExpr("CURRENT_DATE")) -
           new FunctionExpr("YEAR", new PropertyExpr("BirthDate"));
});
```

### 5.3 注册 SQL 处理器

```csharp
SqlBuilder.Instance.RegisterFunctionSqlHandler("YEAR", (funcName, args) => {
    return $"YEAR({args[0].Key})";
});
```

### 5.4 使用

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
```

## 6. 示例三：自定义字符串函数

### 6.1 注册方法处理器

```csharp
LambdaExprConverter.RegisterMethodHandler("CustomProcess", (node, converter) => {
    var strExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    return new FunctionExpr("CUSTOM_PROCESS", strExpr);
});
```

### 6.2 注册 SQL 处理器

```csharp
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_PROCESS", (funcName, args) => {
    if (args.Count != 1)
        throw new ArgumentException("CUSTOM_PROCESS requires 1 argument");
    return $"dbo.CustomProcess({args[0].Key})";
});
```

### 6.3 扩展方法定义

```csharp
public static class StringExtensions
{
    public static string CustomProcess(this string value)
    {
        return value.ToUpper();  // 本地实现
    }
}
```

### 6.4 使用

```csharp
var users = await userService.SearchAsync(
    u => u.UserName.CustomProcess() == "ADMIN"
);
```

## 7. 示例四：多数据库适配

### 7.1 分别为不同数据库注册

```csharp
// MySQL
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (name, args) => {
    return $"MYSQL_CUSTOM({args[0].Key})";
});

// SQL Server
SqlServerBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (name, args) => {
    return $"dbo.CustomFunc({args[0].Key})";
});

// Oracle
OracleBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (name, args) => {
    return $"CUSTOM_FUNC({args[0].Key})";
});
```

### 7.2 全局注册（所有数据库相同）

```csharp
// 全局注册（SqlBuilder.Instance 对应默认数据库）
SqlBuilder.Instance.RegisterFunctionSqlHandler("CUSTOM_FUNC", (name, args) => {
    return $"CUSTOM_FUNC({args[0].Key})";
});
```

## 8. 高级用法

### 8.1 处理复杂参数

```csharp
LambdaExprConverter.RegisterMethodHandler("InRange", (node, converter) => {
    var valueExpr = converter.ConvertInternal(node.Arguments[0]) as ValueTypeExpr;
    var minExpr = converter.ConvertInternal(node.Arguments[1]) as ValueTypeExpr;
    var maxExpr = converter.ConvertInternal(node.Arguments[2]) as ValueTypeExpr;

    var greaterOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.GreaterThanOrEqual, minExpr);
    var lessOrEqual = new LogicBinaryExpr(valueExpr, LogicOperator.LessThanOrEqual, maxExpr);
    return greaterOrEqual.And(lessOrEqual);
});
```

### 8.2 返回逻辑表达式

```csharp
LambdaExprConverter.RegisterMethodHandler("IsValid", (node, converter) => {
    var propExpr = converter.ConvertInternal(node.Object) as ValueTypeExpr;
    return propExpr.IsNotNull() & (propExpr != "");
});
```

## 9. 默认注册的 Lambda 方法

LiteOrm 在启动时通过 `LiteOrmLambdaHandlerInitializer` 和 `LiteOrmSqlFunctionInitializer` 自动注册了大量默认方法：

| 类型 | 方法/成员 | 说明 | 对应 SqlFunction |
|------|----------|------|------------------|
| `DateTime` | `.Now` | 当前时间 | `CURRENT_TIMESTAMP` |
| `DateTime` | `.Today` | 当天日期 | `CURRENT_DATE` |
| `DateTime` | `.AddSeconds()` / `.AddMinutes()` 等 | 日期加减 | 数据库 DATE\_ADD 函数 |
| `string` | `.StartsWith()` | 前缀匹配 | SQL `LIKE 'xxx%'` |
| `string` | `.EndsWith()` | 后缀匹配 | SQL `LIKE '%xxx'` |
| `string` | `.Contains()` | 包含 | SQL `LIKE '%xxx%'` |
| `string` | `.Length` | 字符串长度 | 数据库 LENGTH 函数 |
| `string` | `.Concat()` | 字符串拼接 | 数据库 `+` 或 `\|\|` 或 CONCAT |
| `string` | `.IndexOf()` | 子串位置 | 数据库 INSTR / CHARINDEX |
| `string` | `.Substring()` | 子串截取 | 数据库 SUBSTR / SUBSTRING |
| `string` | `.Trim()` / `.TrimStart()` / `.TrimEnd()` | 去除空格 | SQL TRIM / LTRIM / RTRIM |
| `string` | `.Replace()` | 字符串替换 | SQL REPLACE |
| `string` | `.Insert()` | 插入字符串 | SQL INSERT |
| `string` | `.Remove()` | 删除字符 | SQL LEFT |
| `string` | `.ToString(format)` | 格式化 | SQL Format |
| `Math` | `.Abs()` / `.Max()` / `.Min()` 等 | 数学函数 | 直接转换为 SQL |
| `IList` | `.Contains()` | 集合包含 | SQL `IN` |
| `TimeSpan` | `.TotalSeconds` / `.TotalDays` 等 | 时间差计算 | 数据库 DateDiff 函数 |
| `Equals()` | 实例/静态 Equals | 相等比较 | SQL `=` |
| `ExprExtensions.To()` | 将对象转为 Expr | 类型转换 | - |

```csharp
// 以下 Lambda 表达式会自动转换为对应的 SQL 函数
var users = await userService.SearchAsync(u => u.CreateTime > DateTime.Now);
var users = await userService.SearchAsync(u => u.UserName.StartsWith("A"));
var users = await userService.SearchAsync(u => u.UserName.Contains("test"));
var users = await userService.SearchAsync(u => u.Tags.Contains(1));
var users = await userService.SearchAsync(u => u.CreateTime.AddDays(7) > DateTime.Now);
```

## 10. 默认注册的 SqlFunction（跨数据库）

LiteOrm 在启动时通过 `LiteOrmSqlFunctionInitializer` 自动注册了以下跨数据库 SqlFunction：

| SqlFunction | 说明 | 各数据库实现 |
|-------------|------|-------------|
| `Now` | 当前时间戳 | MySQL: `NOW()`, SQLite: `datetime('now')` |
| `Today` | 当前日期 | MySQL: `CURDATE()`, SQLite: `date('now')` |
| `CASE` | 条件表达式 | 标准 SQL CASE WHEN |
| `Over` | 窗口函数 OVER 子句 | 标准 SQL OVER |
| `RowsBetween` / `RangeBetween` | 窗口函数帧定义 | 标准 ROWS/RANGE BETWEEN |
| `IndexOf` | 字符串位置（0-based） | MySQL: `INSTR()-1`, SQL Server: `CHARINDEX()-1` |
| `Substring` | 字符串截取（0-based） | MySQL: `SUBSTR(..., pos+1, len)` |
| `Trim` | 去除首尾空格/字符 | `TRIM(str)` 或 `TRIM(BOTH char FROM str)` |
| `TrimStart` | 去除头部空格/字符 | `LTRIM(str)` |
| `TrimEnd` | 去除尾部空格/字符 | `RTRIM(str)` |
| `Remove` | 删除从位置到结尾的字符 | SQL `LEFT(str, count)` |
| `IfNull` | 空值替换 | MySQL: `IFNULL`, SQL Server: `ISNULL`, Oracle: `NVL` |
| `Format` | 日期格式化 | 各数据库原生 FORMAT 函数 |
| `AddSeconds` / `AddMinutes` 等 | 日期加减 | 各数据库 DATE\_ADD / DATEADD |
| `DateDiffSeconds` / `DateDiffDays` 等 | 日期间差计算 | 各数据库对应函数 |
| `TotalSeconds` / `TotalDays` 等 | 时间值转数值 | 各数据库对应函数 |

**各数据库特有函数**：

**MySQL**：`LENGTH` → `CHAR_LENGTH()`

**SQL Server**：`Length` → `LEN()`，`IndexOf` → `CHARINDEX(..., ...+1)-1`

**SQLite**：日期函数使用 `julianday()` 计算

**Oracle / PostgreSQL**：使用 `EXTRACT()` 处理时间间隔，`IfNull` → `NVL` / `COALESCE`

## 11. 下一步

- 关联查询：[关联查询](../05_Associations.md)
- 窗口函数：[窗口函数](./EXP_WindowFunctions.md)
- 函数验证器：[函数验证器](./EXP_FunctionExprValidator.md)
