# PracticalQueryDemo 演示说明

## 📋 演示概述

优化后的 PracticalQueryDemo 包含 4 个实践演示，展示 Lambda 查询、Expr 模型、序列化和等价性验证。

## 🎯 包含的 4 个演示

### 演示1：Lambda 链式查询（推荐方式）

**场景**：使用 Lambda 表达式进行链式查询，包括 WHERE、ORDER BY 和分页

**代码示例**：
```csharp
var results = await userSvc.SearchAsync(
    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
          .OrderByDescending(u => u.Id)
          .Skip(0).Take(10)
);
```

**特点**：
- ✅ 最接近 EF/LINQ 习惯
- ✅ 框架自动转换为 Expr 模型
- ✅ 推荐使用方式

---

### 演示2：Expr 模型序列化和反序列化

**场景**：将查询表达式序列化为 JSON，可用于跨服务传递或存储

**代码示例**：
```csharp
// 构建 Expr 模型
var expr = Expr.From<User>()
    .Where(Expr.Prop("Age") >= minAge)
    .Where(Expr.Prop("UserName").Like($"%{searchName}%"))
    .OrderBy(("Id", false))
    .Section(0, 5);

// 序列化为 JSON
var json = JsonSerializer.Serialize(expr);

// 反序列化
var deserializedExpr = JsonSerializer.Deserialize<SqlSegmentExpr>(json);
```

**特点**：
- ✅ 完整演示序列化过程
- ✅ 显示 JSON 内容（截断版本）
- ✅ 说明跨服务传递的可能性

---

### 演示3：Lambda 和 Expr 的等价性验证

**场景**：验证两种方式构建的查询是否生成相同的 SQL

**代码示例**：
```csharp
// 方式1：Lambda 表达式
var lambdaExpr = q => q.Where(u => u.Age > 25);

// 方式2：Expr 模型
var exprModel = Expr.From<User>()
    .Where(Expr.Prop("Age") > 25);

// 验证等价性
var lambdaExprConverted = LambdaExprConverter.ToSqlSegment(lambdaExpr);
bool isEquivalent = lambdaExprConverted.Equals(exprModel);
```

**特点**：
- ✅ 验证两种方式的等价性
- ✅ 使用 Equals 方法进行比较
- ✅ 帮助理解转换过程

---

### 演示4：复杂过滤条件组合

**场景**：组合多个过滤条件：年龄范围、名字包含、排序和分页

**代码示例**：
```csharp
var results = await userSvc.SearchAsync(
    q => q.Where(u => u.Age >= minAge && u.Age <= maxAge)
          .Where(u => u.UserName.Contains(searchName))
          .OrderBy(u => u.Age)
          .ThenBy(u => u.UserName)
          .Skip(0).Take(5)
);
```

**特点**：
- ✅ 演示多个 WHERE 条件的合并
- ✅ 展示多级排序（ThenBy）
- ✅ 体现实际查询场景

---

## ✨ 演示格式特性

### 统一的输出结构

每个演示按照以下结构进行输出：

```
【演示标题】
├─ 【📋 场景说明】    - 清晰描述演示内容
├─ 【📝 代码实现】    - 展示 C# 代码
├─ 【💾 序列化前的 Expr】 - 仅在演示2展示
├─ 【📄 序列化后的 JSON】 - 仅在演示2展示
├─ 【🔍 执行的 SQL】  - 从 SessionManager 获取
├─ 【🔍 等价性验证结果】 - 仅在演示3展示
└─ 【✅ 查询结果】    - 显示查询返回的数据
```

### 颜色化输出

- 部分标题使用青色（Cyan）便于区分
- 错误信息使用红色（Red）醒目提示

---

## 📊 优化说明

### 移除的内容

- ❌ 基础值与属性表达式演示
- ❌ 逻辑比较与组合演示
- ❌ 多个 Exists 子查询演示
- ❌ 分表查询演示
- ❌ 直接构建复杂 SQL 表达式演示

### 保留并强化的内容

- ✅ Lambda 链式查询（最常用）
- ✅ Expr 序列化（实用特性）
- ✅ 等价性验证（重要概念）
- ✅ 复杂过滤条件（实际场景）

---

## 🎓 使用建议

### 运行演示

```csharp
var factory = new DefaultServiceFactory();
await PracticalQueryDemo.RunAsync(factory);
```

### 查看 SQL 语句

演示会自动输出执行的 SQL，帮助理解框架如何将 Lambda 转换为 SQL。

### 学习路线

1. 从演示1开始，了解 Lambda 链式查询方式
2. 查看演示2的序列化过程，理解 Expr 模型的结构
3. 通过演示3验证两种方式的等价性
4. 参考演示4处理复杂的实际查询需求

---

## 📚 相关文档

- ShardingQueryDemo_Format.md - 分表演示的格式说明
- README.md - 项目主文档
