# 数据库差异与兼容性说明

本文总结 LiteOrm 在不同数据库上的常见差异点，帮助你在选型、排障和扩展方言时更快定位问题。接入新数据库、排查方言行为或评估是否需要扩展 `SqlBuilder` 时，都可以先从这里开始。

## 1. 当前支持范围

当前文档和 README 中明确提到的主要数据库包括：

- SQL Server
- MySQL
- Oracle
- PostgreSQL
- SQLite

## 2. 最常见的兼容性敏感点

### 2.1 分页语法

分页通常是最容易暴露方言差异的部分。

- 新版 SQL Server 通常使用 `OFFSET ... FETCH`
- 旧版 Oracle 往往需要 `ROW_NUMBER()` 或嵌套分页
- MySQL / PostgreSQL / SQLite 更常见 `LIMIT/OFFSET`

如果目标数据库的分页规则比较特殊，优先参考：

- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

### 2.2 字符串函数与日期函数

同一个业务意图，在不同数据库上的函数名和参数形式可能不同，常见差异包括：

- 日期格式化
- 字符串拼接
- 日期截断或日期运算
- 当前时间函数

如果你注册了自定义 Lambda 翻译，一定要确认最终生成的 SQL 是否适配目标数据库方言。

建议参考：

- [表达式扩展](../04-extensibility/01-expression-extension.md)
- `LiteOrm.Demo\Demos\DateFormatDemo.cs`

### 2.3 批量写入能力

批量写入通常最依赖数据库驱动和 provider 的实现方式。

- SQL Server 常见方案：`SqlBulkCopy`
- MySQL 常见方案：`MySqlBulkCopy` 或等效高效导入能力
- 其他数据库可能更依赖普通批量 SQL 或自定义 `IBulkProvider`

因此，`BatchInsertAsync` 的最终性能不仅取决于 LiteOrm，也取决于你是否接入了合适的 `IBulkProvider`。

### 2.4 参数数量限制

不同数据库和驱动对单条 SQL 参数数量的容忍度不同，因此配置中的 `ParamCountLimit` 很重要。

需要特别留意的场景包括：

- 大批量 `IN (...)`
- 一次性插入过多行
- 复杂更新语句生成了过多参数

常见处理方式：

- 调整批次大小
- 分批提交
- 调整 `ParamCountLimit`

参考文档：

- [配置项速查](./01-configuration-reference.md)
- [性能优化](../03-advanced-topics/03-performance.md)

## 3. 文档能力与兼容性工作的对应关系

| 能力 | 兼容性敏感点 | 建议 |
|------|--------------|------|
| 分页 | SQL 方言差异最大 | 优先验证分页 SQL，必要时自定义 `SqlBuilder` |
| 窗口函数 | 老版本数据库可能不支持 | 先确认数据库版本，再决定是否启用 |
| 自定义函数 | 函数名和参数形式差异大 | 用表达式扩展做数据库定制 |
| 批量导入 | 依赖驱动和 provider | 尽量使用数据库原生批量能力 |
| 分表 | 主要取决于表名规则 | 尽早统一 `TableArgs` 约定 |

## 4. 实用验证建议

### 4.1 接入新数据库时先验证这些场景

1. 基础增删改查
2. 排序分页
3. 关联查询
4. 批量插入
5. 一个自定义函数或表达式扩展

这五类基本能覆盖大部分早期暴露出来的方言差异。

### 4.2 面对旧数据库时先看分页

如果目标环境是老版本 Oracle，或其他分页语法较特殊的数据库，建议优先验证“排序 + 分页”的组合查询。

### 4.3 先看生成 SQL，再看 ORM 代码

排查兼容性问题时，推荐按这个顺序进行：

1. 确认目标数据库版本和驱动
2. 检查实际生成 SQL 是否符合方言
3. 再决定是否需要扩展表达式或替换 `SqlBuilder`

## 5. 什么时候需要自定义 `SqlBuilder`

出现以下情况时，通常应考虑自定义 `SqlBuilder`：

- 分页 SQL 与目标数据库版本不兼容
- 函数翻译需要统一改写层
- 某些 SQL 片段必须按数据库专属方式生成
- 希望把方言差异统一收敛到基础设施层

参考入口：

- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

## 6. 一个务实的兼容性策略

如果你需要在多个数据库之间迁移，或同时支持多种数据库，推荐采用以下策略：

- 教程层和服务层尽量保持统一写法
- 将兼容性差异集中到 `SqlBuilder` 和表达式扩展层
- 对数据库敏感能力使用 Demo 或集成测试做验证

这样可以让业务层代码长期保持更稳定。

## 相关链接

- [返回目录](../SUMMARY.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
