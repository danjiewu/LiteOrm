# 示例索引

本文按“场景”而不是按“章节”汇总 LiteOrm 当前文档中的示例，方便快速定位可以直接照着理解或验证的入口。多数示例来自 `LiteOrm.Demo`（完整流程）和 `LiteOrm.Tests`（边界与可验证行为）。

## 1. 入门闭环

### 从配置到 CRUD 的最小闭环

- 文档入口：[第一个完整示例](../01-getting-started/04-first-example.md)
- 适合场景：第一次接入 LiteOrm，想先完成一个可运行的基线流程
- 重点内容：
  - 实体定义
  - 服务注册
  - 插入、查询、更新、统计、删除的完整闭环

## 2. 查询示例

### Lambda / `Expr` / `ExprString` 选型

- 文档入口：[查询指南](../02-core-usage/03-query-guide.md)
- 重点内容：
  - 三种查询方式的适用边界
  - 动态条件拼装
  - `ExprString` 的适用范围

### `EXISTS` / `Expr.ExistsRelated(...)` / 子查询

- 文档入口：[查询指南](../02-core-usage/03-query-guide.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs`
  - `LiteOrm.Tests\ExprEnhancedTests.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
  - `LiteOrm.Tests\LambdaQueryTests.cs`
- 重点内容：
  - `Expr.Exists<T>(...)`
  - `Expr.ExistsRelated<T>(...)`
  - `NOT ExistsRelated(...)`
  - `IN` 子查询
  - 关联过滤与普通字段条件组合

### 常见条件与集合运算

- 文档入口：[查询指南](../02-core-usage/03-query-guide.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\PracticalQueryDemo.cs`
  - `LiteOrm.Tests\PracticalQueryTests.cs`
- 重点内容：
  - `In`
  - `Between`
  - `Like`
  - 动态 DTO 转 `Expr`

## 3. 写入与批处理示例

### 批量插入、批量更新、批量删除

- 文档入口：[CRUD 指南](../02-core-usage/04-crud-guide.md)
- 代码来源：
  - `LiteOrm.Demo\Data\DbInitializer.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- 重点内容：
  - `BatchInsertAsync`
  - `BatchUpdateAsync`
  - `BatchDeleteAsync`
  - 批量写入闭环

### Upsert 与混合批处理

- 文档入口：[CRUD 指南](../02-core-usage/04-crud-guide.md)
- 代码来源：
  - `LiteOrm.Tests\ServiceTests.cs`
  - `LiteOrm.Demo\Demos\UpdateExprDemo.cs`
- 重点内容：
  - `BatchUpdateOrInsertAsync`
  - `EntityOperation<T>` 混合批处理
  - `UpdateExpr` 条件更新

## 4. 关联查询示例

### `ForeignType` + `ForeignColumn` 最小闭环

- 文档入口：[关联查询](../02-core-usage/05-associations.md)
- 适合场景：先理解一层外键关联如何映射到视图字段

### 多级关联与 `AutoExpand`

- 文档入口：[关联查询](../02-core-usage/05-associations.md)
- 代码来源：
  - `LiteOrm.Demo\Models\User.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- 重点内容：
  - `DeptName / ParentDeptName`
  - `AutoExpand = true` 的二级展开
  - 关联字段排序与分页

### `Expr.ExistsRelated(...)` 过滤型关联

- 文档入口：[关联查询](../02-core-usage/05-associations.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\ExistsRelatedDemo.cs`
  - `LiteOrm.Tests\ExprEnhancedTests.cs`
- 重点内容：
  - 正向过滤
  - 反向路径推断
  - 与普通条件组合
  - 何时优先选择 `Expr.ExistsRelated(...)`

## 5. 高级能力示例

### 事务

- 文档入口：[事务管理](../03-advanced-topics/01-transactions.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\TransactionDemo.cs`
- 重点内容：
  - 声明式事务
  - 手动事务
  - 失败回滚
  - 业务流程包裹

### 分表与 `TableArgs`

- 文档入口：[分表分库与 TableArgs](../03-advanced-topics/02-sharding-and-tableargs.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\ShardingQueryDemo.cs`
  - `LiteOrm.Demo\Models\SalesRecord.cs`
- 重点内容：
  - `IArged`
  - `TableArgs`
  - 查询时覆盖分表参数
  - 分月表读写流程

### 性能优化与批量 Provider

- 文档入口：[性能优化](../03-advanced-topics/03-performance.md)
- 代码来源：
  - `LiteOrm.Demo\Data\DbInitializer.cs`
  - `LiteOrm.Demo\Demos\MySqlBulkInsertProvider.cs`
  - `LiteOrm.Tests\ServiceTests.cs`
- 重点内容：
  - 批量初始化
  - `SearchAs<T>` 投影
  - `ExistsAsync` vs `CountAsync`
  - `IBulkProvider` 的实际实现

### 窗口函数

- 文档入口：[窗口函数](../03-advanced-topics/04-window-functions.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\WindowFunctionDemo.cs`
- 重点内容：
  - 注册窗口函数
  - 聚合窗口查询
  - 排名与统计结果映射

### 自定义分页

- 文档入口：[自定义分页](../03-advanced-topics/05-custom-paging.md)
- 适合场景：旧数据库分页语法不兼容，需要自定义方言分页逻辑

## 6. 扩展开发示例

### 表达式扩展

- 文档入口：[表达式扩展](../04-extensibility/01-expression-extension.md)
- 代码来源：
  - `LiteOrm.Demo\Demos\DateFormatDemo.cs`
- 重点内容：
  - 注册方法翻译
  - 日期格式化示例
  - Lambda 到 SQL 的扩展流程

### 函数验证器

- 文档入口：[函数验证器](../04-extensibility/02-function-validator.md)
- 重点内容：
  - 白名单策略
  - 安全边界
  - 与表达式扩展联动

### 自定义 `SqlBuilder`

- 文档入口：[自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
- 重点内容：
  - 方言扩展入口
  - 自定义分页的公共覆写点
  - 注册与接入流程

## 7. 推荐查阅顺序

如果想按“从简单到复杂”的顺序阅读示例，可以采用以下路径：

1. [第一个完整示例](../01-getting-started/04-first-example.md)
2. [查询指南](../02-core-usage/03-query-guide.md)
3. [CRUD 指南](../02-core-usage/04-crud-guide.md)
4. [关联查询](../02-core-usage/05-associations.md)
5. [事务管理](../03-advanced-topics/01-transactions.md)
6. [分表分库与 TableArgs](../03-advanced-topics/02-sharding-and-tableargs.md)
7. [性能优化](../03-advanced-topics/03-performance.md)
8. [表达式扩展](../04-extensibility/01-expression-extension.md)

## 相关链接

- [返回目录](../SUMMARY.md)
- [API 索引](./02-api-index.md)
- [迁移映射](./04-migration-map.md)
