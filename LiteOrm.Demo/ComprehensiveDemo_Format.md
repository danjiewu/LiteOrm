# ComprehensiveDemo 演示说明

## 📋 演示概述

ComprehensiveDemo 是一个完整的数据操作演示，展示 LiteOrm 框架的核心功能：
- **数据插入** - 创建测试数据（User、Department、SalesRecord）
- **数据更新** - 更新发货日期
- **数据查询** - 三种查询方式（Lambda、Expr、ExprString）

## 🎯 演示流程

### 步骤1：准备测试数据

创建示例数据用于后续操作：

**创建部门**：
- 销售部、技术部、管理部

**创建用户**：
- 张三（45岁，经理）- 销售部
- 李四（38岁，经理）- 技术部
- 王五（32岁）- 销售部
- 赵六（28岁）- 技术部
- 孙七（52岁，经理）- 管理部

**创建销售记录**（2026年1月）：
- 电脑，5000元（王五，1月5日） - 发货日期为空
- 电脑，6000元（王五，1月10日） - 发货日期为空
- 平板，3000元（赵六，1月8日） - 已发货（1月9日）
- 电脑，5500元（赵六，1月15日） - 发货日期为空

---

### 步骤2：数据更新

**场景**：将所有发货日期为空且产品为电脑的销售记录的发货日期设为订购日期加10天

**更新代码**：
```csharp
var updateExpr = new UpdateExpr
{
    Source = Expr.From<SalesRecord>(),
    Sets = new List<(string, ValueTypeExpr)>
    {
        ("ShipTime", Expr.Const(shipDate))
    },
    Where = Expr.And(
        Expr.Prop("ShipTime").IsNull(),
        Expr.Prop("ProductName") == "电脑"
    )
};

var rowsAffected = await salesService.UpdateAsync(updateExpr);
```

**执行结果**：
- 演示输出实际执行的 SQL
- 显示更新影响的行数

---

### 步骤3：数据查询

#### 查询1：所有名字包含"经理"的用户按年龄从大到小排序

**三种查询方式**：

##### 方式1：Lambda 查询（推荐）
```csharp
var managers = await userService.SearchAsync(
    q => q.Where(u => u.UserName.Contains("经理"))
          .OrderByDescending(u => u.Age)
          .Skip(0).Take(10)
);
```

##### 方式2：Expr 查询
```csharp
var managerExpr = Expr.From<User>()
    .Where(Expr.Prop("UserName").Like("%经理%"))
    .OrderBy(("Age", false))
    .Section(0, 10);

var managers = await userService.SearchAsync(managerExpr);
```

##### 方式3：ExprString 形式
```
UserName like '%经理%' order by Age desc limit 0,10
```

**输出内容**：
- 📝 代码实现
- 💾 Expr 模型（方式2）
- 🔍 执行的 SQL
- ✅ 查询结果
- 🔍 等价性验证

---

#### 查询2：2026年1月销售记录按销售额排序前10条

**三种查询方式**：

##### 方式1：Lambda 查询
```csharp
var startDate = new DateTime(2026, 1, 1);
var endDate = new DateTime(2026, 1, 31);

var sales = await salesService.SearchAsync(
    q => q.Where(s => s.SaleTime >= startDate && s.SaleTime <= endDate)
          .OrderByDescending(s => s.Amount)
          .Skip(0).Take(10)
);
```

##### 方式2：Expr 查询
```csharp
var salesExpr = Expr.From<SalesRecord>()
    .Where(Expr.Prop("SaleTime") >= startDate)
    .Where(Expr.Prop("SaleTime") <= endDate)
    .OrderBy(("Amount", false))
    .Section(0, 10);

var sales = await salesService.SearchAsync(salesExpr);
```

##### 方式3：ExprString 形式
```
SaleTime>='2026-01-01' and SaleTime<='2026-01-31' order by Amount desc limit 0,10
```

---

## ✨ 演示特性

### 统一的输出格式

每个演示部分都包含：
- 📋 **场景说明** - 清晰的功能描述
- 📝 **代码实现** - 完整的 C# 代码
- 💾 **Expr 模型** - SQL 段的结构表示
- 🔍 **执行的 SQL** - 从 `SessionManager.Current.SqlStack.Last()` 获取
- ✅ **查询结果** - 最终返回的数据

### 颜色化输出

- 部分标题使用青色（Cyan）便于区分
- 错误信息使用红色（Red）醒目提示
- 成功消息使用默认颜色

### 验证等价性

三种查询方式在逻辑上是等价的，演示会验证：
- 结果行数是否一致
- 生成的 SQL 是否相同（结构）

---

## 🎓 关键学习点

### 1. Lambda vs Expr vs ExprString

| 方式 | 优点 | 缺点 |
|:---|:---|:---|
| Lambda | 最直观，接近 LINQ | 需要反射转换 |
| Expr | 性能好，明确的 SQL 结构 | 需要手动构建 |
| ExprString | 可序列化，便于跨服务 | 字符串容易出错 |

### 2. 更新操作

使用 `UpdateExpr` 执行复杂的更新操作，支持：
- 多列更新
- 条件更新（WHERE 子句）
- 表达式计算

### 3. SQL 追踪

通过 `SessionManager.Current.SqlStack` 可以：
- 查看生成的 SQL 语句
- 验证查询逻辑
- 性能分析

---

## 📊 代码结构

```
ComprehensiveDemo/
├── PrepareTestDataAsync()      // 步骤1：创建测试数据
├── DemoUpdateOperationAsync()  // 步骤2：演示更新操作
├── DemoQueryOperationsAsync()  // 步骤3：演示查询操作
│   ├── Query1_ManagersByAgeAsync()    // 查询1：按名字和年龄
│   └── Query2_TopSalesInJanuaryAsync()// 查询2：按日期和金额
└── PrintSection()              // 辅助：格式化输出
```

---

## 🚀 运行演示

```csharp
var factory = new DefaultServiceFactory();
await ComprehensiveDemo.RunAsync(factory);
```

---

## 📚 相关文档

- ShardingQueryDemo_Format.md - 分表演示说明
- PracticalQueryDemo_Format.md - 综合查询演示说明
- Demo.md - 演示格式规范
