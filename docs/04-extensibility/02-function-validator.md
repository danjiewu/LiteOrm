# 函数表达式验证器

`FunctionExprValidator` 用于验证查询中函数表达式的使用是否符合既定的安全策略。

## 1. 概述

当使用表达式扩展功能注册自定义函数时，可能需要限制这些函数的使用范围，防止滥用或安全风险。`FunctionExprValidator` 提供了一种机制来控制哪些函数表达式可以在查询中使用。

## 2. FunctionPolicy 枚举

```csharp
public enum FunctionPolicy
{
    AllowAll,       // 允许所有函数表达式
    AllowRegisted, // 仅允许预注册的函数表达式
    Disallow        // 不允许任何函数表达式
}
```

### 2.1 策略说明

| 策略 | 说明 |
|------|------|
| `AllowAll` | 允许任何函数表达式，包括未注册的 |
| `AllowRegisted` | 仅允许通过 `SqlBuilder.RegisterFunctionSqlHandler` 注册过的函数 |
| `Disallow` | 不允许任何函数表达式 |

## 3. 使用方式

### 3.1 预配置验证器

`FunctionExprValidator` 提供了三个预配置的静态实例：

```csharp
// 允许所有函数
var validatorAllowAll = FunctionExprValidator.AllowAll;

// 仅允许注册的函数
var validatorAllowRegisted = FunctionExprValidator.AllowRegisted;

// 不允许任何函数
var validatorDisallow = FunctionExprValidator.Disallow;
```

### 3.2 自定义验证器

```csharp
var validator = new FunctionExprValidator(FunctionPolicy.AllowRegisted);
```

### 3.3 推荐策略模板

| 环境或角色 | 推荐策略 |
|------------|----------|
| 本地开发 / 内部工具 | `AllowAll` |
| 生产环境普通业务查询 | `AllowRegisted` |
| 完全禁止自定义函数的受限场景 | `Disallow` |

## 4. 验证接口

`FunctionExprValidator` 实现 `IExprNodeVisitor` 接口：

```csharp
public bool Validate(Expr node)
{
    if (node == null) return true;
    if (node is FunctionExpr funcExpr)
    {
        switch (FunctionPolicy)
        {
            case FunctionPolicy.AllowAll:
                return true;
            case FunctionPolicy.AllowRegisted:
                return SqlBuilder.Instance.TryGetFunctionSqlHandler<SqlBuilder>(
                    funcExpr.FunctionName, out _);
            case FunctionPolicy.Disallow:
                return false;
        }
    }
    return true;
}
```

## 5. 应用场景

### 5.1 限制用户查询

在多租户场景下，限制不同租户可以使用函数：

```csharp
public class UserQueryService
{
    private readonly FunctionExprValidator _validator;

    public UserQueryService(UserRole role)
    {
        _validator = role == UserRole.Admin
            ? FunctionExprValidator.AllowAll
            : FunctionExprValidator.AllowRegisted;
    }

    public async Task<List<User>> SearchAsync(Expr query)
    {
        // 验证查询表达式
        if (!_validator.Validate(query))
            throw new InvalidOperationException("查询包含不允许的函数表达式");

        return await _userViewDAO.Search(query).ToListAsync();
    }
}
```

### 5.2 自定义 DAO 验证

```csharp
public class SafeUserDAO : ObjectViewDAO<User>
{
    private static readonly FunctionExprValidator Validator =
        FunctionExprValidator.AllowRegisted;

    public async Task<List<User>> SafeSearchAsync(Expr expr)
    {
        if (!Validator.Validate(expr))
            throw new SecurityException("表达式验证失败");

        return await Search(expr).ToListAsync();
    }
}
```

### 5.3 全局查询拦截

在查询执行前进行全局验证：

```csharp
public class QueryInterceptor
{
    private readonly FunctionExprValidator _validator;

    public QueryInterceptor(FunctionPolicy policy)
    {
        _validator = new FunctionExprValidator(policy);
    }

    public void Intercept(Expr query)
    {
        if (!_validator.Validate(query))
        {
            throw new UnauthorizedAccessException(
                "查询包含未授权的函数表达式");
        }
    }
}
```

### 5.4 与表达式扩展联动

一个典型流程是：先注册允许使用的扩展函数，再在执行查询前用验证器做兜底校验。

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", ...);

var validator = FunctionExprValidator.AllowRegisted;
var expr = new FunctionExpr("DATE_FORMAT", ...);

if (!validator.Validate(expr))
    throw new InvalidOperationException("函数未注册，禁止执行");
```

## 6. 与 SqlBuilder 配合

`FunctionExprValidator.AllowRegisted` 依赖于 `SqlBuilder.RegisterFunctionSqlHandler` 注册的函数：

```csharp
// 注册函数处理器
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", ...);
MySqlBuilder.Instance.RegisterFunctionSqlHandler("CONCAT", ...);

// AllowRegisted 验证器只允许这些注册的函数
var validator = FunctionExprValidator.AllowRegisted;

// DATE_FORMAT - 允许（已注册）
var expr1 = new FunctionExpr("DATE_FORMAT", ...);
validator.Validate(expr1);  // true

// CUSTOM_UNREGISTERED - 拒绝（未注册）
var expr2 = new FunctionExpr("CUSTOM_UNREGISTERED", ...);
validator.Validate(expr2);  // false
```

## 7. 注意事项

1. **验证时机**：建议在查询执行前进行验证，而非查询构建时
2. **性能影响**：验证过程会遍历表达式树，对性能有一定影响
3. **安全考虑**：生产环境中建议使用 `AllowRegisted` 策略

## 8. 下一步

- [返回目录](../README.md)
- [关联查询](../02-core-usage/05-associations.md)
- [表达式扩展](./01-expression-extension.md)
- [窗口函数](../03-advanced-topics/04-window-functions.md)

