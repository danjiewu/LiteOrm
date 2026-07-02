# 数据库差异与兼容性说明

本文总结 LiteOrm 在不同数据库上的常见差异点，帮助你在选型、排障和扩展方言时更快定位问题。接入新数据库、排查方言行为或评估是否需要扩展 `SqlBuilder` 时，都可以先从这里开始。

## 1. 支持的数据库

LiteOrm 内置 11 个数据库方言的 `SqlBuilder` 实现（含 6 个国产/兼容数据库），加上一个通用基类作为兜底：

### 主流数据库

| 数据库 | SqlBuilder 类 | 自动匹配关键词 | 最低版本 |
|--------|--------------|---------------|---------|
| SQL Server | `SqlServerBuilder` | `SQLCLIENT` | 2012+ |
| MySQL | `MySqlBuilder` | `MYSQL` | 8.0+ |
| Oracle | `OracleBuilder` | `ORACLE` | 12c+ |
| PostgreSQL | `PostgreSqlBuilder` | `NPGSQL` | — |
| SQLite | `SQLiteBuilder` | `SQLITE` | — |

### 国产 / 兼容数据库

| 数据库 | SqlBuilder 类 | 继承自 | 自动匹配关键词 |
|--------|--------------|--------|---------------|
| 达梦 DM | `DamengBuilder` | `OracleBuilder` | `DAMENG`、`DMNET`、`DM.DMCONNECTION` |
| 人大金仓 KingbaseES | `KingbaseESBuilder` | `PostgreSqlBuilder` | `KINGBASE`、`KDBNDP` |
| 华为 GaussDB / openGauss | `GaussDBBuilder` | `PostgreSqlBuilder` | `GAUSSDB`、`OPENGAUSS` |
| OceanBase（MySQL 兼容） | `OceanBaseBuilder` | `MySqlBuilder` | `OCEANBASE` |
| TiDB | `TiDBBuilder` | `MySqlBuilder` | `TIDB` |
| 万里 GreatDB | `GreatDBBuilder` | `MySqlBuilder` | `GREATDB` |

> 国产数据库 Builder 均为标记子类，继承父类的全部行为。`DamengBuilder` 仅重写了 `GetAutoIncrementSql(ColumnDefinition)`（返回 `IDENTITY(起始值, 增量)`），其余 5 个为空类体。

| 其他（通用兜底） | `SqlBuilder`（基类） | — | — |

### 方言自动检测机制

`SqlBuilderFactory.GetSqlBuilder` 根据连接类型的全名（`providerType.FullName` 大写）做子串匹配。**国产数据库优先检查**，避免被通用关键词误匹配：

```
 1. DAMENG / DMNET / DM.DMCONNECTION  → DamengBuilder.Instance
 2. KINGBASE / KDBNDP                  → KingbaseESBuilder.Instance
 3. GAUSSDB / OPENGAUSS                → GaussDBBuilder.Instance
 4. OCEANBASE                          → OceanBaseBuilder.Instance
 5. TIDB                               → TiDBBuilder.Instance
 6. GREATDB                            → GreatDBBuilder.Instance
 7. ORACLE                             → OracleBuilder.Instance
 8. MYSQL                              → MySqlBuilder.Instance
 9. SQLITE                             → SQLiteBuilder.Instance
10. SQLCLIENT                          → SqlServerBuilder.Instance（兼容 Microsoft.Data.SqlClient 和 System.Data.SqlClient）
11. NPGSQL                             → PostgreSqlBuilder.Instance
其他                                    → SqlBuilder.Instance（通用基类）
```

也可通过 `SqlBuilderFactory.RegisterSqlBuilder(Type, SqlBuilder)` 或 `RegisterSqlBuilder(string dataSourceName, SqlBuilder)` 手动注册。按数据源名注册的优先级最高，其次按类型注册，最后才走自动检测。

### 各数据库 Provider 配置

| 数据库 | NuGet 包 | Provider 配置值 |
|--------|----------|----------------|
| SQL Server | `Microsoft.Data.SqlClient` | `Microsoft.Data.SqlClient.SqlConnection, Microsoft.Data.SqlClient` |
| SQL Server (旧版) | `System.Data.SqlClient` | `System.Data.SqlClient.SqlConnection, System.Data.SqlClient` |
| MySQL | `MySqlConnector` | `MySqlConnector.MySqlConnection, MySqlConnector` |
| MySQL (旧版) | `MySql.Data` | `MySql.Data.MySqlClient.MySqlConnection, MySql.Data` |
| PostgreSQL | `Npgsql` | `Npgsql.NpgsqlConnection, Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core` | `Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess` |
| SQLite | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite` |

> 国产数据库需安装对应厂商驱动，Provider 填写厂商提供的 `DbConnection` 类型全名。

## 2. 各数据库方言差异详解

### 2.1 分页语法

分页是最容易暴露方言差异的部分。

| 数据库 | 分页方式 |
|--------|---------|
| SQL Server 2012+ / Oracle 12c+ / PostgreSQL | `OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`（基类默认） |
| MySQL / OceanBase / TiDB / GreatDB | `LIMIT [skip,] take` |
| SQLite | `LIMIT take OFFSET skip` |
| Oracle 11g 及更早 | 需 `ROW_NUMBER()` 嵌套子查询，须自定义 `SqlBuilder` |

> Demo 项目中的 [Oracle11gBuilder.cs](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/Oracle11gBuilder.cs) 继承 `OracleBuilder` 并重写 `BuildSelectSql`，演示了如何为 Oracle 11g 实现嵌套分页。

参考文档：
- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

### 2.2 类型映射差异

不同数据库对 .NET 类型的处理方式不同，`SqlBuilder` 子类在 `GetDbTypeInternal` / `ConvertToDbValue` 中做了针对性处理：

| 数据库 | 特殊处理 |
|--------|---------|
| Oracle / 达梦 | `bool` → `DbType.Byte`（Oracle 无原生布尔类型）；`DateTime` → `DbType.Date` |
| SQLite | `DateTime`/`TimeSpan`/`DateTimeOffset` → `DbType.String`；写入时 DateTime 格式化为 `yyyy-MM-dd HH:mm:ss.fff`，DateTimeOffset 格式化为 `yyyy-MM-dd HH:mm:ss.fff zzz`，TimeSpan 使用 `c` 格式 |
| SQL Server / MySQL / PostgreSQL / 其他 | 标准类型映射，无特殊转换 |

### 2.3 自增主键（Identity）

| 数据库 | Identity 方式 | `SupportBatchInsertWithIdentity` | 说明 |
|--------|--------------|--------------------------------|------|
| SQL Server | `SELECT @@IDENTITY`（单行）/ `SCOPE_IDENTITY()`（批量） | ✅ `true` | 基类单行用 `@@IDENTITY`，SqlServer 批量用 `SCOPE_IDENTITY()` |
| MySQL / OceanBase / TiDB / GreatDB | `LAST_INSERT_ID()` | ✅ `true` | — |
| SQLite | `LAST_INSERT_ROWID()` | ✅ `true` | — |
| PostgreSQL / 金仓 / GaussDB | `RETURNING` 子句 | ❌ `false` | 使用 SERIAL/BIGSERIAL 类型 |
| Oracle 12c+ / 达梦 | `GENERATED AS IDENTITY` + `RETURNING ... INTO :param` | ❌ `false` | `OracleIdentitySourceType.Identity`（默认） |
| Oracle 11g / 达梦 | 序列（Sequence） | ❌ `false` | `OracleIdentitySourceType.Sequence`，SQL 使用 `表名_seq.nextval` |
| Oracle / 达梦 | 自定义表达式 | ❌ `false` | `OracleIdentitySourceType.Expression`，使用 `IdentityExpression` |

> `OracleIdentitySourceType` 枚举有三个成员：`Identity`（默认，Oracle 12c+）、`Sequence`（序列）、`Expression`（自定义 SQL 表达式）。

#### 起始值与增量（`IdentityStart` / `IdentityIncreasement`）

通过 `[Column]` 特性的 `IdentityStart`（起始值，默认 `1`）与 `IdentityIncreasement`（增量，默认 `1`）可自定义自增序列，各数据库支持情况如下：

| 数据库 | 起始值 | 增量 | 生成语法示例 |
|--------|--------|------|-------------|
| SQL Server（基类） | ✅ 列级 `IDENTITY(n, m)` | ✅ 同左 | `IDENTITY(1000,5)` |
| 达梦 DM | ✅ 列级 `IDENTITY(n, m)` | ✅ 同左 | `IDENTITY(1000, 5)` |
| Oracle 12c+ | ✅ `START WITH n` | ✅ `INCREMENT BY m` | `GENERATED AS IDENTITY (START WITH 1000 INCREMENT BY 5)` |
| MySQL / OceanBase / TiDB / GreatDB | ⚠️ 表级 `AUTO_INCREMENT = n` | ❌ 需会话变量 `auto_increment_increment` | `CREATE TABLE ... ) AUTO_INCREMENT = 1000` |
| SQLite | ❌ 不支持自定义 | ❌ 不支持自定义 | `AUTOINCREMENT`（固定按 rowid 递增） |
| PostgreSQL / 金仓 / GaussDB | ❌ SERIAL 由序列控制 | ❌ 同左 | 列级无片段，需手动操作底层序列 |

> MySQL 的增量步长是连接级会话变量，无法写入建表语句；若需自定义增量，请在获取连接后执行 `SET @@SESSION.auto_increment_increment = n`。

### 2.4 字符串拼接

| 数据库 | 拼接方式 |
|--------|---------|
| SQL Server | `+` 运算符 |
| PostgreSQL / SQLite / Oracle / 达梦 | `||` 运算符 |
| MySQL / OceanBase / TiDB / GreatDB | `CONCAT(...)` 函数（基类默认） |

### 2.5 标识符引用与参数前缀

| 数据库 | 标识符引用 | 参数前缀 | 名称大小写处理 |
|--------|-----------|---------|---------------|
| SQL Server | `"name"`（双引号） | `@` | 不转换 |
| MySQL / OceanBase / TiDB / GreatDB | `` `name` ``（反引号） | `@` | 不转换 |
| Oracle / 达梦 | `"NAME"`（双引号） | `:` | 转大写 |
| PostgreSQL / 金仓 / GaussDB | `"name"`（双引号） | `@` | 转小写 |
| SQLite | `"name"`（双引号） | `@` | 不转换 |

### 2.6 集合操作

| 数据库 | EXCEPT 对应语法 |
|--------|----------------|
| Oracle / 达梦 | `MINUS` |
| 其他 | `EXCEPT`（基类默认） |

### 2.7 批量更新

| 数据库 | 批量更新方式 |
|--------|-------------|
| SQL Server | `UPDATE T SET ... FROM table T INNER JOIN (VALUES ...) AS S(...) ON ...` |
| MySQL / OceanBase / TiDB / GreatDB | `UPDATE table T INNER JOIN (SELECT ... UNION ALL ...) S ON ... SET ...` |
| Oracle / 达梦 | `MERGE INTO ... USING (SELECT ... FROM DUAL UNION ALL ...) ON (...) WHEN MATCHED THEN UPDATE SET ...` |
| PostgreSQL / 金仓 / GaussDB | `UPDATE table u SET ... FROM (VALUES ...) AS v(...) WHERE u.key = v.k0` |
| SQLite | `WITH batch_data(...) AS (VALUES (...)) UPDATE table SET col = (SELECT ... FROM batch_data WHERE ...) WHERE EXISTS (...)` |

### 2.8 批量插入

| 数据库 | 批量插入方式 |
|--------|-------------|
| Oracle / 达梦 | `INSERT INTO ... SELECT ... FROM DUAL UNION ALL SELECT ...` |
| 其他 | 标准 `INSERT INTO ... VALUES (...), (...), ...`（基类默认） |

## 3. 批量写入能力（IBulkProvider）

LiteOrm 通过 `IBulkProvider` 接口支持高性能批量写入，但**核心库不内置任何实现**——仅提供接口和工厂。

| 数据库 | 常见方案 | 内置实现 |
|--------|---------|---------|
| SQL Server | `SqlBulkCopy` | 无（需自行实现 `IBulkProvider`） |
| MySQL | `MySqlBulkCopy` | 无（Demo 中有 [MySqlBulkCopyProvider](https://github.com/danjiewu/LiteOrm/tree/master/LiteOrm.Demo/Demos/MySqlBulkInsertProvider.cs) 示例） |
| Oracle | 普通 `INSERT` 批量 | 无 |
| PostgreSQL | `COPY` 命令 | 无 |
| SQLite | 普通 `INSERT` 批量 | 无 |

`BulkProviderFactory` 通过 `[AutoRegister(Key = typeof(连接类型))]` 注册的 `IBulkProvider` 实例，按连接类型查找。未注册时返回 `null`，`BatchInsertAsync` 回退到逐条插入。

> 要启用高性能批量写入，需自行实现 `IBulkProvider` 并用 `[AutoRegister(Key = typeof(MySqlConnection))]` 标注注册。

## 4. 参数数量限制

不同数据库和驱动对单条 SQL 参数数量的容忍度不同，配置中的 `ParamCountLimit` 很重要。

需要特别留意的场景：
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

## 5. 测试覆盖情况

| 数据库 | SqlBuilder | 单元测试 | Demo 配置 | 驱动包引用 |
|--------|-----------|---------|----------|-----------|
| SQLite | ✅ | ✅ | ✅ | ✅ |
| MySQL | ✅ | ✅ | ✅ | ✅ |
| Oracle | ✅ | ✅ | ✅ | ✅ |
| PostgreSQL / 金仓 / GaussDB | ✅ | ❌ | ❌ | ❌ |
| SQL Server | ✅ | ❌ | ❌ | ❌ |
| 达梦 / OceanBase / TiDB / GreatDB | ✅ | ❌ | ❌ | ❌ |

> 测试项目、Demo 和 Benchmark 仅配置了 MySQL、SQLite、Oracle 三种数据库。其他数据库的 `SqlBuilder` 已在核心库中实现但未经自动化测试验证。接入这些数据库时建议优先验证分页和批量操作。

## 6. 文档能力与兼容性工作的对应关系

| 能力 | 兼容性敏感点 | 建议 |
|------|--------------|------|
| 分页 | SQL 方言差异最大 | 优先验证分页 SQL，必要时自定义 `SqlBuilder` |
| 窗口函数 | 老版本数据库可能不支持 | 先确认数据库版本，再决定是否启用 |
| 自定义函数 | 函数名和参数形式差异大 | 用表达式扩展做数据库定制 |
| 批量导入 | 依赖驱动和 provider | 尽量使用数据库原生批量能力，自行实现 `IBulkProvider` |
| 分表 | 主要取决于表名规则 | 尽早统一 `TableArgs` 约定 |

## 7. 实用验证建议

### 7.1 接入新数据库时先验证这些场景

1. 基础增删改查
2. 排序分页
3. 关联查询
4. 批量插入
5. 一个自定义函数或表达式扩展

这五类基本能覆盖大部分早期暴露出来的方言差异。

### 7.2 面对旧数据库时先看分页

如果目标环境是老版本 Oracle（11g 及更早），或其他分页语法较特殊的数据库，建议优先验证"排序 + 分页"的组合查询。

### 7.3 先看生成 SQL，再看 ORM 代码

排查兼容性问题时，推荐按这个顺序进行：

1. 确认目标数据库版本和驱动
2. 检查实际生成 SQL 是否符合方言
3. 再决定是否需要扩展表达式或替换 `SqlBuilder`

## 8. 什么时候需要自定义 `SqlBuilder`

出现以下情况时，通常应考虑自定义 `SqlBuilder`：

- 分页 SQL 与目标数据库版本不兼容（如 Oracle 11g）
- 函数翻译需要统一改写层
- 某些 SQL 片段必须按数据库专属方式生成
- 希望把方言差异统一收敛到基础设施层

参考入口：
- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)

## 9. 一个务实的兼容性策略

如果你需要在多个数据库之间迁移，或同时支持多种数据库，推荐采用以下策略：

- 教程层和服务层尽量保持统一写法
- 将兼容性差异集中到 `SqlBuilder` 和表达式扩展层
- 对数据库敏感能力使用 Demo 或集成测试做验证

这样可以让业务层代码长期保持更稳定。

## 相关链接

- [返回目录](../README.md)
- [示例索引](./06-example-index.md)
- [生成 SQL 示例](./07-sql-examples.md)
- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)
