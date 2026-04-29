# 安全性

LiteOrm 从架构设计层面内置了多层 SQL 注入防护机制。本文档全面介绍框架的安全策略、各个组件的原理、潜在风险点以及最佳实践。

## 1. 防御体系总览

LiteOrm 的 SQL 注入防护采用**多层纵深防御**策略：

| 层次 | 机制 | 说明 |
|------|------|------|
| 参数化 SQL | `outputParams` + 占位符 | 所有用户值以参数形式传递，零例外 |
| LIKE 转义 + 参数化 | 通配符转义 + `ESCAPE` 子句 | 双重保护防止 LIKE 注入 |
| ExprString 自动参数化 | 非 Expr 值自动转为命名参数 | 插值字符串中的用户值自动参数化 |
| 表达式类型白名单 | `ExprTypeValidator` | 控制允许的表达式类型 |
| 函数策略控制 | `FunctionExprValidator` | 控制可执行的 SQL 函数范围 |
| 自定义 SQL 预注册 | `GenericSqlExpr` | 禁止动态创建未注册的 SQL 片段 |

---

## 2. 参数化 SQL —— 核心防线

### 2.1 参数传递机制

LiteOrm 所有 SQL 值传递都通过 `outputParams` 集合完成：

```csharp
public static string ToSql(this Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams)
```

生成的 SQL 中使用参数占位符（如 `@0`、`@1`），值通过 `outputParams` 独立传递，**从不将用户输入直接拼接到 SQL 字符串中**。

**示例**：

```csharp
var users = await userService.SearchAsync(u => u.UserName == "John");
// 生成 SQL: SELECT * FROM Users WHERE UserName = @0
// 参数: @0 = "John"
```

### 2.2 值类型的分层处理

| 值类型 | 处理方式 | 安全分析 |
|-------|---------|---------|
| `null` | 输出 `NULL` 字面量 | 安全，不可注入 |
| `bool` | 输出 `1` / `0` | 安全 |
| 基本数值（int/long/float 等） | 直接内联 `.ToString()` | 安全，数值不可能包含 SQL 特殊字符 |
| 集合（IN 子句） | 每个元素单独参数化 | 安全 |
| 字符串、DateTime 等 | 参数化 | 安全 |

```csharp
// 数值直接内联（仅适用于 IsConst + 基本数值类型）
var users = await userService.SearchAsync(u => u.Age >= 18);
// SQL: SELECT * FROM Users WHERE Age >= 18

// 字符串始终参数化
var users = await userService.SearchAsync(u => u.UserName == "O'Brien");
// SQL: SELECT * FROM Users WHERE UserName = @0
// 参数: @0 = "O'Brien"  （单引号安全处理）
```

### 2.3 LIKE 查询的双重保护

LIKE 查询同时使用**参数化**和**通配符转义**：

```csharp
var users = await userService.SearchAsync(u => u.UserName.Contains("100%"));
// SQL: SELECT * FROM Users WHERE UserName LIKE @0 ESCAPE '/'
// 参数: @0 = "%100/%%"  （% 被转义为 /%）
```

转义规则：`_`、`%`、`[`、`]` 等通配符使用 `/` 前缀转义（通过 `ESCAPE '/'` 声明）：

```
用户输入: "100%_test"
转义后参数值: "100/%/_test"  → LIKE @0 ESCAPE '/'
```

### 2.4 NULL 安全处理

`Column = NULL` 自动转换为 `IS NULL`，避免标准 SQL 中 `= NULL` 的语义错误：

```csharp
var results = await viewService.SearchAsync(u => u.UserName == null);
// SQL: SELECT * FROM Users WHERE UserName IS NULL
```

### 2.5 不同数据库的参数占位符

| 数据库 | 占位符格式 | 示例 |
|--------|-----------|------|
| SQL Server / MySQL / PostgreSQL / SQLite | `@n` | `@0`, `@1` |
| Oracle | `:n` | `:0`, `:1` |

参数占位符的生成由 `ISqlBuilder` 接口抽象，数据库方言各自实现 `ToSqlParam(name)` 和 `ToParamName(name)`。

---

## 3. ExprString —— 插值字符串安全解析

### 3.1 设计原理

`ExprString` 是一个标记了 `[InterpolatedStringHandler]` 的 `ref struct`，借助 C# 编译器支持，在编译时解析插值字符串并自动区分 Expr 和普通值：

```csharp
dao.Search($"WHERE {Expr.Prop("Age")} > {minAge}");
// Expr.Prop("Age")  → 走 Expr.ToSql()，完整参数化
// minAge (int)      → 自动生成参数占位符 @0，值加入 outputParams
```

### 3.2 处理路径

```
插值字符串 $"..."
    │
    ├─ 格式化项是 Expr 对象 → expr.ToSql() → 完整表达式树处理
    │
    ├─ 格式化项是普通值    → 自动生成 @N 占位符 + 加入参数列表
    │
    └─ 字面量字符串        → 直接追加（开发者硬编码的 SQL 关键词/结构）
```

**代码示例**：

```csharp
string keyword = "John";
int minAge = 18;

// ExprString 自动处理：
// - "WHERE " 是字面量，直接追加
// - {keyword} 是普通值，自动参数化为 @0
// - {Expr.Prop("Age")} 是 Expr，走 ToSql 参数化路径
// - " >= " 是字面量
// - {minAge} 是普通值，自动参数化为 @1
dao.Search($"WHERE {Expr.Prop("UserName")} LIKE {'%' + keyword + '%'} AND {Expr.Prop("Age")} >= {minAge}");
```

### 3.3 关键说明

- **字面量字符串**（`AppendLiteral`）是开发者硬编码的 SQL 关键词和结构，**不可由用户控制**
- **格式化项中的普通值**（非 Expr）**自动参数化**，无需手动处理

---

## 4. ExprValidator —— 表达式验证器

### 4.1 架构设计

LiteOrm 采用**访问者模式** + **策略模式**实现表达式验证：

```
ExprValidator (抽象基类)
├── ExprTypeValidator      ── 基于表达式类型白名单
├── FunctionExprValidator  ── 基于函数策略
└── ExprValidatorGroup     ── 多验证器组合
```

### 4.2 类型白名单验证（ExprTypeValidator）

通过 `ExprType` 白名单控制允许的表达式类型：

```csharp
// Minimum：允许基本查询条件（12 种类型）
// Value, Property, Unary, ValueSet, LogicBinary, And, Or, Not, 
// Where, OrderBy, OrderByItem, Section 等
// 禁止：SelectItem, From, Table, Function, Update, Delete

// QueryOnly：允许完整 SELECT 查询（20 种类型）
// 包含 Minimum 的所有类型 + SelectItem, From, GroupBy, TableJoin 等
// 明确禁止：Update, Delete
```

```csharp
var validator = ExprValidator.CreateQueryOnly();

if (validator.VisitAll(expr))
{
    var results = await userService.SearchAsync(expr);
}
else
{
    // validator.FailedExpr 包含被拒绝的节点
    throw new UnauthorizedAccessException("Query contains disallowed expressions");
}
```

### 4.3 函数策略验证（FunctionExprValidator）

控制 `FunctionExpr` 的执行范围：

| 策略 | 值 | 说明 | 适用场景 |
|------|---|------|---------|
| `AllowAll` | 0 | 允许所有函数 | 本地开发 / 内部工具 |
| `AllowRegisted` | 1 | 仅允许已注册的函数 | **生产环境推荐** |
| `Disallow` | 2 | 禁止所有函数 | 完全受限环境 |

```csharp
// 生产环境推荐：只允许已注册的函数
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),
    FunctionExprValidator.AllowRegisted
);

// 在 Search 前验证
if (!validator.VisitAll(expr))
{
    throw new UnauthorizedAccessException(
        $"Blacklisted expression found: {validator.FailedExpr}"
    );
}
```

`AllowRegisted` 通过检查函数是否在 `SqlBuilder` 中注册过来判断是否允许：

```csharp
case FunctionPolicy.AllowRegisted:
    return SqlBuilder.Instance.TryGetFunctionSqlHandler<SqlBuilder>(
        funcExpr.FunctionName, out _);
```

### 4.4 多验证器组合

```csharp
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),      // 只允许查询类型
    FunctionExprValidator.AllowRegisted   // 只允许已注册的函数
);

if (!validator.VisitAll(expr))
{
    // validator.FailedExpr     — 失败节点
    // validator.FailedVisitor  — 失败的验证器
}
```

验证器组采用**短路求值**：任一验证器失败即停止，并记录失败的验证器。

---

## 5. GenericSqlExpr —— 自定义 SQL 片段

### 5.1 设计目的

`GenericSqlExpr` 提供了一种安全的机制来嵌入自定义 SQL 片段，通过**预注册 + 回调委托**的方式控制 SQL 生成：

```csharp
public delegate string SqlGenerateHandler(
    SqlBuildContext context, ISqlBuilder sqlBuilder,
    ICollection<KeyValuePair<string, object>> outputParams, object arg);

public sealed class GenericSqlExpr : LogicExpr
{
    public string Key { get; set; }   // 注册表中查找的唯一键
    public object Arg { get; set; }   // 传递给回调的额外参数
}
```

### 5.2 注册机制

```csharp
// 注册自定义 SQL 生成器
GenericSqlExpr.Register("CustomCheck", (context, sqlBuilder, outputParams, arg) =>
{
    // 参数化：使用 outputParams 传递用户值
    string paramName = outputParams.Count.ToString();
    outputParams.Add(new(sqlBuilder.ToParamName(paramName), arg));
    return $"dbo.CustomCheck({sqlBuilder.ToSqlParam(paramName)})";
});

// 在查询中使用
var expr = Expr.Prop("IsActive") == true 
    & new GenericSqlExpr("CustomCheck") { Arg = "someValue" };
var users = await userService.SearchAsync(expr);
```

### 5.3 安全特性

1. **必须预注册**：通过 `ConcurrentDictionary` 维护全局注册表，未注册的 key 会抛出异常
2. **支持参数化**：委托签名包含 `outputParams`，可以安全地传递用户值
3. **参数传递**：通过 `Arg` 属性传递业务参数，不拼接到 SQL 中

---

## 6. Expr 的风险点与注意事项

### 6.1 ExprString 的使用限制

`ExprString` 是一个标记了 `[InterpolatedStringHandler]` 的 `ref struct`，**只有在调用接受 `ExprString` 类型参数的方法时**（如 `dao.Search(...)`、`SqlGen.ToSql(...)` 等），编译器才会生成 `ExprString` 实例。**普通的插值字符串生成的是普通 `string`，不会自动参数化**：

```csharp
string userInput = request.Query["name"];

// ❌ 错误：普通插值字符串生成普通 string，不会变为 ExprString，无自动参数化
var badSql1 = $"SELECT * FROM Users WHERE Name = '{userInput}'";  // 危险：值直接拼入字面量
var badSql2 = $"SELECT * FROM Users WHERE Name = {userInput}";    // 错误：仍是普通 string，未参数化

// ✅ 正确：在 DAO 方法中使用 ExprString（方法接受 ExprString 类型参数）
var result = await dao.Search($"WHERE {Expr.Prop("Name")} == {userInput}").ToListAsync();
// 生成的 SQL: WHERE Name = @0
// 参数: @0 = userInput 的值

// ✅ 正确：使用 Expr 表达式构建查询
var expr = Expr.Prop("Name") == userInput;
var users = await userService.SearchAsync(expr);
```

在 `ExprString` 内部，**字面量字符串**（`AppendLiteral`）是开发者硬编码的 SQL 关键词和结构，**不可由用户控制**；格式化项中的普通值（非 `Expr`）会自动参数化。

### 6.2 GenericSqlExpr 的自由度

`SqlGenerateHandler` 委托可以返回任意字符串。如果回调中不谨慎使用 `outputParams`，可能在自定义 SQL 中引入注入点：

```csharp
// ❌ 危险：直接在委托中拼接用户输入
GenericSqlExpr.Register("UnsafeLookup", (ctx, sb, params, arg) =>
{
    return $"SELECT * FROM Users WHERE Code = '{arg}'";
});

// ✅ 安全：使用 outputParams 参数化
GenericSqlExpr.Register("SafeLookup", (ctx, sb, params, arg) =>
{
    string paramName = params.Count.ToString();
    params.Add(new(sb.ToParamName(paramName), arg));
    return $"SELECT * FROM Users WHERE Code = {sb.ToSqlParam(paramName)}";
});
```

### 6.3 Expr.Prop 属性名来源

`Expr.Prop` 内部已对属性名和表别名做了合法名称校验（如拒绝 `@`、`-`、空格等特殊字符），传入非法名称会直接抛出 `ArgumentException`，一般情况下无需额外校验：

```csharp
// ✅ 安全：Prop 内部已做合法名称校验
var propName = request.Query["field"];  // 用户可控
// new PropertyExpr("Name@123")  → 抛出 ArgumentException
// new PropertyExpr("Name-Column") → 抛出 ArgumentException
var expr = Expr.Prop(propName) == "value";
```

如果需要限制允许的字段范围（如只允许查询特定列），可使用白名单做**业务层面的限制**：

```csharp
// ✅ 推荐：当需要限制允许的字段范围时，使用白名单
var allowedFields = new HashSet<string> { "UserName", "Age", "Email" };
if (!allowedFields.Contains(propName))
    throw new ArgumentException("Invalid field");
var expr = Expr.Prop(propName) == "value";
```

### 6.4 前端提交 Expr JSON 的风险

当允许前端通过 JSON 构造 Expr 时（[前端原生 Expr 查询](../04-extensibility/06-frontend-native-expr.md)），务必配合验证器使用：

```csharp
var expr = ExprJsonConvert.Deserialize(json);
var validator = ExprValidatorGroup.Create(
    ExprValidator.CreateQueryOnly(),
    FunctionExprValidator.AllowRegisted
);

if (!validator.VisitAll(expr))
{
    throw new UnauthorizedAccessException("Query rejected by security validator");
}

// 建议额外限制：只允许特定表和列
var propValidator = new PropertyNameValidator(new[] { "UserName", "Age", "CreateTime" });
if (!propValidator.VisitAll(expr))
{
    throw new UnauthorizedAccessException("Field access denied");
}
```

### 6.5 权限过滤的配合

安全过滤应与[权限过滤](./06-permission-filtering.md)配合使用：

```csharp
// 在进入 Search 之前，先拼上用户范围条件
LogicExpr permissionFilter = GetCurrentUserPermissionExpr();
LogicExpr finalExpr = expr & permissionFilter;

// 再通过安全验证器
if (!securityValidator.VisitAll(finalExpr))
    throw new UnauthorizedAccessException();

var results = await userService.SearchAsync(finalExpr);
```

### 6.6 Expr 的灵活性与注意事项

Expr 表达式体系虽然可以从架构层面杜绝 SQL 注入，但其功能非常强大灵活，使用时需注意：

- **表达式能力强大**：Expr 支持子查询、函数调用、跨表关联等复杂操作，不当使用可能导致性能问题或意外行为
- **验证器并非默认启用**：`ExprValidator` 是可选的，如果不配置验证器，Expr 可以生成任意 SQL 结构（包括 UPDATE、DELETE 等）
- **生产环境务必配置验证器**：使用 `ExprValidator.CreateQueryOnly()` + `FunctionExprValidator.AllowRegisted` 限制表达式能力范围
- **前端提交 Expr 必须验证**：如果允许前端构造 Expr JSON，务必配合 `ExprValidator` + `PropertyNameValidator` 双重验证
- **避免过度动态化**：尽量避免根据用户输入动态构造过于复杂的表达式树，保持业务逻辑的可预测性

---

## 7. 安全检查清单

在生产环境中使用 LiteOrm 时，建议逐一确认以下事项：

| 检查项 | 说明 |
|--------|------|
| ✅ 启用 `AllowRegisted` 函数策略 | 防止执行未注册的 SQL 函数 |
| ✅ 前端 Expr 查询前使用验证器 | 限制表达式类型和字段访问范围 |
| ✅ 自定义 SQL 使用 `outputParams` | GenericSqlExpr 回调中使用参数化 |
| ✅ Expr.Prop 已内置名称校验 | 非法名称会直接抛异常；如需限制字段范围，额外使用白名单 |
| ✅ 通过 DAO 方法使用 ExprString | 普通插值字符串不生成 ExprString，需通过 `dao.Search(...)` 等方法触发 |
| ✅ 配合权限过滤使用 | 在验证器之外叠加用户范围过滤 |
| ✅ LIKE 查询不接受裸通配符 | 对于前端传入的 LIKE 值，考虑是否需要转义/禁止通配符 |
| ✅ 认识 Expr 的灵活性 | Expr 功能强大，生产环境务必配置验证器限制能力范围 |

---

## 8. 相关链接

- [返回目录](../README.md)
- [函数验证器](../04-extensibility/02-function-validator.md)
- [权限过滤](./06-permission-filtering.md)
- [前端原生 Expr 查询](../04-extensibility/06-frontend-native-expr.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)
