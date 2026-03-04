# Lambda 方式分表查询演示指南

## 概述

`LambdaShardingDemo` 演示了如何使用 Lambda 表达式的方式设置分表参数（TableArgs），展示了 LiteOrm 新增的类型安全的分表查询功能。

## 演示内容

### 演示 1：基础分表查询
展示如何使用 Lambda 表达式进行简单的分表查询。
```csharp
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == new[] { "202401" } &&
    u.Id > 100;
```

### 演示 2：多月份分表查询
展示如何在单个查询中指定多个分表参数。
```csharp
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == new[] { "202401", "202402", "202403" } &&
    u.Name.Contains("admin");
```

### 演示 3：复杂条件组合
展示分表条件与多个业务条件的组合。
```csharp
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == new[] { "202401" } &&
    u.Name.Contains("user") &&
    u.Age > 18 &&
    u.IsActive == true;
```

### 演示 4：Exists 子查询中的分表
展示在关联查询中对子表进行分表。
```csharp
Expression<Func<User, bool>> lambda = u =>
    u.IsActive == true &&
    Expr.Exists<Order>(o =>
        ((IArged)o).TableArgs == new[] { "202401" } &&
        o.UserId == u.Id);
```

### 演示 5：使用变量引用分表参数
展示如何动态使用变量或方法返回值作为分表参数。
```csharp
var tableArgs = new[] { "202401", "202402" };
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == tableArgs &&
    u.Id > 0;
```

### 演示 6：Lambda 方式 vs Expr API 方式对比
对比两种实现分表查询的方式。
- **Lambda 方式**（新）：更直观、类型安全
- **Expr API 方式**（传统）：向后兼容、灵活

## 如何运行

### 方式 1：在 Demo 程序中运行

修改 `LiteOrm.Demo/Program.cs`，添加以下代码：

```csharp
// 创建 Demo 实例
var shardingDemo = serviceProvider.GetRequiredService<LambdaShardingDemo>();

// 运行 Demo
await shardingDemo.RunAsync();
```

### 方式 2：直接在代码中使用

在你的项目中引入以下代码：

```csharp
using System;
using System.Linq.Expressions;
using LiteOrm.Common;

// 基础用法
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == new[] { "202401" } &&
    u.Id > 100;

var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);
var fromExpr = Expr.From<User>().Where(logicExpr);
```

## 关键概念

### IArged 接口
```csharp
public interface IArged
{
    /// <summary>
    /// 获取表参数数组
    /// </summary>
    string[] TableArgs { get; }
}
```

所有需要分表的数据模型都应该隐式实现 `IArged` 接口（通过类型转换）。

### TableArgs 属性
- 类型：`string[]`
- 作用：指定分表参数，如月份分表 `["202401", "202402"]`
- 使用：在 Lambda 表达式中通过 `((IArged)parameter).TableArgs` 访问

### 表达式转换
```csharp
// Lambda 表达式转换为逻辑表达式
var logicExpr = LambdaExprConverter.ToLogicExpr(lambda);

// 逻辑表达式转换为 SQL 片段
var sqlSegment = Expr.From<User>().Where(logicExpr).ToSqlSegment();
```

## 最佳实践

### ✅ 推荐做法

1. **统一表达**：将分表条件和业务条件统一在 Lambda 表达式中
   ```csharp
   Expression<Func<User, bool>> lambda = u =>
       ((IArged)u).TableArgs == new[] { "202401" } &&
       u.Age > 18;
   ```

2. **使用变量**：对于动态分表参数，使用变量引用
   ```csharp
   var months = GetCurrentMonths();
   Expression<Func<User, bool>> lambda = u =>
       ((IArged)u).TableArgs == months &&
       u.Id > 0;
   ```

3. **子查询分表**：在 Exists 子查询中独立设置分表参数
   ```csharp
   Expr.Exists<Order>(o =>
       ((IArged)o).TableArgs == new[] { "202401" } &&
       o.UserId == u.Id)
   ```

### ❌ 避免做法

1. **错误的类型转换**：
   ```csharp
   // ❌ 错误
   Expression<Func<User, bool>> lambda = u =>
       u.TableArgs == new[] { "202401" };  // User 不实现 IArged
   
   // ✅ 正确
   Expression<Func<User, bool>> lambda = u =>
       ((IArged)u).TableArgs == new[] { "202401" };
   ```

2. **不支持的运算符**：
   ```csharp
   // ❌ 错误
   ((IArged)u).TableArgs != new[] { "202401" }
   ((IArged)u).TableArgs > new[] { "202401" }
   
   // ✅ 正确
   ((IArged)u).TableArgs == new[] { "202401" }
   ```

3. **单独使用分表条件**：
   ```csharp
   // ❌ 通常没有意义
   Expression<Func<User, bool>> lambda = u =>
       ((IArged)u).TableArgs == new[] { "202401" };
   
   // ✅ 通常与业务条件结合
   Expression<Func<User, bool>> lambda = u =>
       ((IArged)u).TableArgs == new[] { "202401" } &&
       u.Age > 18;
   ```

## 性能考虑

- **编译时检查**：Lambda 表达式在编译时进行检查，避免运行时错误
- **表达式缓存**：可以将转换后的表达式缓存以提高性能
- **SQL 生成**：分表参数不产生额外的 WHERE 子句，不影响 SQL 性能

## 兼容性

此功能完全向后兼容：
- ✅ 不影响现有的 Expr API 方式
- ✅ 可以在同一项目中混用两种方式
- ✅ 现有代码无需修改

## 测试

完整的测试套件位于 `LiteOrm.Tests\LambdaShardingTests.cs`：

```bash
dotnet test LiteOrm.Tests\LiteOrm.Tests.csproj --filter "FullyQualifiedName~LambdaShardingTests"
```

所有 5 个测试用例全部通过：
- ✅ TestTableArgsAssignment_SingleParameter
- ✅ TestTableArgsAssignment_WithConditions
- ✅ TestTableArgsAssignment_Exists
- ✅ TestTableArgsAssignment_ImplicitArray
- ✅ TestTableArgsAssignment_VerifyFromExpr

## 常见问题

### Q1：如何设置多个分表？
```csharp
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == new[] { "202401", "202402", "202403" } &&
    u.Id > 0;
```

### Q2：如何动态指定分表参数？
```csharp
var months = GetMonthsFromDatabase();
Expression<Func<User, bool>> lambda = u =>
    ((IArged)u).TableArgs == months &&
    u.Id > 0;
```

### Q3：Lambda 方式和 Expr API 方式有什么区别？
| 特性 | Lambda 方式 | Expr API 方式 |
|------|-----------|-------------|
| 类型安全 | ✅ | ❌ |
| 编译检查 | ✅ | ❌ |
| 代码直观 | ✅ | ❌ |
| 向后兼容 | ✅ | ✅ |
| 灵活性 | ✅ | ✅ |

### Q4：如何在子查询中使用分表？
```csharp
Expression<Func<User, bool>> lambda = u =>
    u.IsActive == true &&
    Expr.Exists<Order>(o =>
        ((IArged)o).TableArgs == new[] { "202401" } &&
        o.UserId == u.Id);
```

## 相关文档

- [Lambda 分表功能说明](../LAMBDA_SHARDING.md)
- [LiteOrm API 参考](../LITEORM_API_REFERENCE.md)
- [表达式演示](ExprTypeDemo.cs)

## 反馈和建议

如有任何问题或建议，欢迎提交 Issue 或 Pull Request。
