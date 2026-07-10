# 变更日志 (Changelog)

## v8.0.20 (Unreleased)

### 新增功能
- ExprString 增加插入 RawSql 的方式 (`6f401b6`)

---

## v8.0.19 (2026-07-06)

### 新增功能
- 取消 `ExceptionHook` 机制，新增 `ExceptionHandling` 全局事件进行异常处理 (`f552b91`)
- `ServiceDescription` 扩展方法移至 `LiteOrm.Common`，只从定义的方法或类型加载 (`8c80c40`)
- 新增 `OnTableSyncing` 钩子，可按 `Type` 设定是否同步表 (`5f17866`)
- 自增列建表支持起始值和增量配置 (`a0a7d93`)
- 新增 `Expression<Func<T, T>>` 形式的 Update 方法 (`6060360`)

### 改进
- 优化读取失败时的异常信息 (`0230f96`)

---

## v8.0.18 (2026-06-30)

### 新增功能
- 新增国产数据库 SqlBuilder 支持 (`cd73fb7`)
- 新增 `JsonRemoteServiceTransport` 传输实现 (`d8cddca`)
- Remote/Server 统一支持 `AutoRegisterEntityServices`，默认为 `true` (`edc3ffb`)
- Remote 调用非 Service 方法抛 `NotSupportedException` (`e8c1ea2`)

### 改进
- 修改 `RemoteErrorInfo` 属性名 (`ba1e2e1`, `4d815e2`)
- 优化 Remote 序列化逻辑 (`8383686`)
- Expr 的 `Delete`、`Update` 改为 `DeleteAll`、`UpdateAll`，避免重名冲突 (`f71d27b`)
- 优化 Remote 服务名解析逻辑 (`affe6fd`)
- 更新 Remote 泛型注册逻辑 (`f1d7191`)
- 更新 `RemoteServiceRequest` 的序列化规则 (`4fbc273`)
- 修改 Method 匹配策略 (`8a9b9cf`)
- 修改 Remote 自动注册机制 (`517a013`)

### 修复
- 修复日志 `MethodName` 为空的 bug (`5159a82`)
- 修复 Server 端方法匹配失败问题 (`60b8e20`)
- 修复 Remote.Server 匹配泛型服务名称 bug (`2ea5e2c`)

---

## v8.0.17 (2026-06-18)

### 新增功能
- 新增 Remote 模块，支持远程代理模式 (`e01a660`)
  - 支持单独创建远程代理 (`e3daa0c`)
  - 增加 `.netstandard2.0/2.1` 支持 (`acc8f4c`)
- `SessionManager` 最大 SQL 历史记录条数可配置 (`75796e6`)
- 新增 `CycleDetector` 检测 Expr 循环引用 (`02df339`)
- Expr 新增 `Cast` 扩展 (`f09a309`)
- 新增 `SelectAll` 扩展方法 (`8e2073c`)
- Service 新增 `SearchAs` 方法 (`0578422`)
- 新增三目运算符 (`a ? b : c`) 解析为 `CASE` 语句 (`eb0def4`)

### 改进
- 优化 DAOContext 失效逻辑，缓存的 DAOContext 也会校验失效时间 (`9ec248e`)
- 删除 `PropEqual` 及比较运算符的 `Prop` 方法重载 (`a6dc0fe`)
- 更新 `Case` 扩展方法 (`c7a00ce`)
- 优化 Like 语句 escape 逻辑，非必要时不生成 escape 片段 (`804d85c`)
- 优化 TableView 关联表的排序；`IfNull` 统一使用 `COALESCE` 函数 (`ed2ebea`)
- 元数据的列集合类型由 `Array` 改为 `ReadOnlyCollection` (`233b578`)
- 取消 `AndIf`、`OrIf`、`WhereIf`、`SetIf` 扩展方法 (`a779a00`)

### 重构
- 重构 `ExprVisitor` 和 `ExprValidator`，支持多种遍历和验证方式 (`0c0499c`)
- 清除 CodeGen 和 WebDemo 项目 (`fee1155`)

### 修复
- 修复 Join 条件未指定优先级导致不能正确添加括号的 bug (`ebc87e6`)
- 修正默认 SqlBuilder 匹配方式，正确识别 PostgreSql 和 SqlServer 连接类型 (`e664272`)
- 修复视图扩展属性生成顺序错误 (`1dbe1f4`)

---

## v8.0.16 (2026-05-27)

### 新增功能
- 新增 `Expr.Reduce` 扩展 (`c206a6d`)
- 新增 `ServiceExceptionHook`，可全局或 Attribute 方式自定义异常处理 (`d61d72f`)
- 新增 `PropertyOrder` 属性排序功能 (`7f7dd0e`)

### 改进
- 表达式 SqlName 忽略大小写 (`63a9d60`)
- `ToSqlSelectSetType` 改为 `ToSelectSetTypeSql` (`5669984`)
- `AndExpr`/`OrExpr` 改用集合去重 (`cb466b7`)
- `ExistsRelated` 逻辑优化 (`07556af`)
- `TableDefinition` 的 `ConstFilter` 可更改 (`24c772d`)
- `AutoRegister` 默认生命周期改为 Singleton (`bd823ed`)
- `Update`、`GetObject`、`Delete`、`Exists` 改用数字做参数名 (`adcf2c4`)
- 非基础值类型序列化附带类型标记 (`c14c53e`)
- 修改 `SelectExpr` 的优先级及作用域逻辑 (`f427a62`)
- 修改 `TableExpr` 序列化格式，属性改为与类型同层 (`c5cc577`)

### 修复
- 修复非泛型方法 `ServiceDescription` 缓存错误 (`7bb3892`)
- 修复 Timestamp 列未生效 bug (`378759d`)
- Oracle 自增序列返回参数名使用固定值，避免关键字冲突 (`b005a9a`)
- 修正建表 SQL (`ddbc69c`)
- 修正 CTE 表达式生成 SQL (`8199a7e`)
- 修复 exists 方法 bug (`ab62ace`)

### 重构
- `FromExpr` 和 `TableJoinExpr` 重构，支持使用子查询作为源 (`8ec2c1d`)

---

## v8.0.15 (2026-05-10)

### 新增功能
- 增加 CTE 表达式支持 (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### 新增功能
- 新增 CodeGen 项目 (`c862ffd`)
- 新增 `StringExprConverter` 按实体类型的 `Parse`/`ParsePagedQuery` 方法 (`b4d422f`)

### 改进
- 优化泛型 Controller 与动态 Controller 生成代码 (`8a215f3`)

### 重构
- `UpdateExpr`、`DeleteExpr` 不再归为 `SqlSegment` (`fa0355d`)

### 修复
- 修复 Insert 方法自增列非参数返回方式下的报错问题 (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### 新增功能
- 增加属性常量筛选机制 (`ad1148c`)
- `TableJoin` 支持指定外表主键 (`7cf1afc`)
- `ForeignType` 可声明多个 (`35f4e47`)

### 改进
- 优化 `ForeignColumn` 匹配表机制，支持自动根据实体类型匹配 (`9649b2d`)
- 关闭自动扩展关联表，改为在关联时指定 (`9717515`, `dc71969`)
- 优化日志格式 (`34af777`)
- `And`、`Or` 序列化使用简洁模式 (`4090979`)
- 优化 SQL 格式生成逻辑 (`b904948`)
- Lambda 忽略隐式/显式转换运算符，避免求值报错 (`f7ff97b`)
- Lambda `Contains` 方法只根据名称解析，正确处理 `Span` 类型扩展 (`f6a5edd`)
- 优化 Lambda 解析值逻辑，忽略 `op_Implicit`、`op_Explicit` (`109f22e`)
- 数据库名引用字符默认为 `"` (`2c315cc`)
- `Update` 的 `Set` 元素类型改为 `SetItem` (`2c315cc`)
- 修改 Sql 缩进逻辑 (`b8a14b1`)
- `SqlBuilder.BuildConcatSql` 改为使用 `ValueStringBuilder` (`f8b0cea`)
- Lambda 表达式注册改用静态方法初始化 (`7858b14`)
- Oracle 标识列来源是序列或表达式时，批量插入自动将标识列添加到插入列和对应值中 (`34ba25f`)

### 重构
- `LogicSet` 拆分为 `AndExpr` 和 `OrExpr` (`6dd1063`)
- 优化 `ToSql` 实现代码 (`ab7fcbc`)

### 修复
- 修复 Oracle 的 `DateDiff` 函数 bug (`2c315cc`)
- 修复 `select union` 格式错误；带自增列的批量插入统一使用 `BuildBatchIdentityInsertSql` (`c5f3115`)
- 修复 Oracle 自增列插入错误 (`77e5518`)
- 修复 Expr 为空时扩展方法 `To` 不能正确解析的问题 (`b904948`)
- 修复通过外部列做外键关联时 `ExistsRelated` 生成别名不正确的问题 (`b8a14b1`)
- 修复序列化 bug (`a694a0f`)

---

## v8.0.12 (2026-04-02)

### 新增功能
- 新增 `ExprValidator` 验证机制 (`2c9245e`)
- 新增 `TableExpr` 和 `TableJoinExpr` 及其序列化 (`1ee64b3`, `5b2a116`)
- 新增 `IdentityIncreasement` 配置 (`894cc61`)
- `DAOContextPool` 增加 `ClearPool` 方法 (`8e6e0ed`)
- 新增 `netstandard2.1` 支持 (`8e6e0ed`)
- 新增日期 format 支持；新增 `TimeSpan` 类型支持 (`eef7074`)
- 新增 `DateTime` 减法支持 (`4768942`)
- 新增 `ExistsRelated` 方法，实现自动关联 (`6aa5ff2`)
- 新增 SqlGen 的 ExprString 解析 (`6eac5d5`)
- 新增 `IObjectViewDAO` 方法 (`c63b3a7`)
- 新增建列默认值支持 (`07b30b5`)
- 新增窗口函数支持 (`b7245d1`)
- 新增 `.net standard 2.0` ExprString 支持；主表默认别名统一使用 `T0` (`4aaa3bd`)
- 新增 `ExprVisitor.Visit` 遍历 Expr 方法 (`fc91353`)
- 新增 `DDLGenerator` 用于生成建表 SQL (`fc91353`)
- 新增预生成实体类 `DataReaderConverter` (`8ac1ca6`)
- BulkInsert 方式下增加自动建表 (`586ab62`)
- 新增 Lambda 分表方式 (`b94ca29`)
- 新增 `ExprInterpolatedStringHandler` 用于插值字符串 SQL 构建 (`bf0f85d`)
- 新增 `ForeignExists` 方法 (`2a5960b`)
- 完成 `ExprSqlConverter` 的 ToSql 实现（`OrderByExpr`、`SectionExpr`、`GroupByExpr`、`HavingExpr`、`WhereExpr`） (`a41196e`)
- 为 `ObjectViewDAO` 实现 ExprString 功能 (`fd0f746`)
- 新增 `ExprType` 枚举 (`c82e1ac`)
- 新增自定义方法处理器和 SQL 构造器 (`31be232`)

### 改进
- 连接保活时长默认为 10 分钟 (`214e399`)
- 优化 sql 合法名称校验逻辑 (`31be232`)
- `UpdateExpr` set 列表改为 `PropertyExpr` 类型，增加 `Set` 方法扩展 (`cf61373`)
- 优化 DataReader 读取，兜底使用 `SqlBuilder.ConvertFromDbValue` 转换类型 (`1be94b5`)
- 优化 Lambda 解析机制，尽量将表达式计算值 (`eef7074`)
- `DbCommandProxy` 优化 (`95f7274`)
- 优化会话上下文创建机制 (`be1436c`)
- 优化 SQL 生成逻辑，增加缩进 (`6776934`)
- 优化 Expr 静态和扩展方法 (`607b9b0`, `8d47446`)
- 优化默认表别名的生成机制 (`b2ef1f0`)
- `Expr.Prop("T0.Name")` 形式取消，改为 `Expr.Prop("T0", "Name")`；原有的 `Expr.Prop(string, object)` 改为 `Expr.PropEqual(string, object)` (`035c884`)
- `selectItemExpr` 默认增加别名 (`adcf3cd`)
- 数据读取优化效率，采用动态编译方法读取 (`207fbe2`)
- 取消 `ObjectViewDAO<T>` 的 `ToList` 和 `SearchOne` 方法，统一通过 `Search` 方法查询 (`d6b5f5d`)
- `SessionManager.Current` 改为缓加载 (`658b85a`)
- 增加 `DAOContextPool` 日志信息 (`dcb7b37`)
- 修改日志枚举，减少依赖库 (`46c8cb0`)
- `DAOContext` 释放时异步释放 Connection (`ef335d0`)
- 优化 Command 生成方式，支持异步创建 (`d559005`)
- 修改 `CommandResult` 逻辑 (`a568840`)
- 修改 `DAOContext` 释放逻辑 (`377cfe0`)
- 修改 `SelectExpr` 逻辑 (`f58e86e`)
- 增加 `SqlBuilder` 配置项说明及自定义 `SqlBuilder` 的注册和配置支持 (`60041c8`)
- 更新 `InterpolatedStringHandler` 条件为 `NET8_0_OR_GREATER || NET10_0_OR_GREATER` (`8f9819b`)
- 优化 `ReplaceParam` 实现，使用字典树和 `ValueStringBuilder` (`35be944`)
- 修改 `SelectItemExpr.Name` 为 `Alias` (`35be944`)
- 增加 `DataViewDAO` 和 `ObjectViewDAO` 的 lambda 扩展方法 (`35be944`)

### 重构
- 删除 `LiteOrm.ChartGenerator` 目录 (`c0b87f1`)
- 取消 `AggregateFunctionExpr` (`4ff4fa4`)
- 优化表创建机制 (`b567251`)
- 优化 Lambda converter (`8a97952`)
- `ObjectViewDAO.ConvertToObjectHandler` 改为静态 (`557c8dd`)
- 提取 `IDAOContext` 接口到 `LiteOrm.Common` (`3ebaf0c`)
- `ExprInterpolatedStringHandler` 移至 LiteOrm 项目 (`e292c23`)
- `SqlBuilder` 和 `CreateSqlBuildContext` 改为 internal (`e3f1eb8`)
- `ExprInterpolatedStringHandler` 重构为使用 `DAOBase` 参数 (`25db9d9`)
- Refactor Result types and update DAO implementations (`aafea93`)

### 修复
- 修复 `FromExpr` 序列化问题 (`4b4b90b`)
- 修复 Test 项目构建错误 (`7024826`)
- 完善 Exists 方法处理逻辑；修正 `ExistsRelated` 方法的错误消息 (`feffcc1`)
- 修复 Sqlite 的 `Now`、`Today` 时区问题 (`8e6e0ed`)
- 修复子查询生成 SQL bug (`b25e120`)
- 修复构建错误并配置 benchmark 使用 short 模式 (`ee100b6`)
- 修正 `SumOver` 方法实现 (`c26d2c7`)
- 修正 Oracle 自增列建表语句 (`96cbadf`)
- 修正单表模式，表名不再加别名 (`4afb92d`)

---

## v8.0.10 / v8.0.11 (2026-03-11)

### 新增功能
- 自定义 `SqlBuilder` 的注册和配置支持 (`60041c8`)

### 改进
- `ServiceInvokeInterceptor` 改为抛出原异常 (`e88a0ad`)
- `ObjectViewDAO.ConvertToObjectHandler` 改为静态 (`557c8dd`)
- 数据读取优化效率，采用动态编译方法读取 (`207fbe2`)
- 取消 `ObjectViewDAO<T>` 的 `ToList` 和 `SearchOne` 方法，统一通过 `Search` 方法查询 (`d6b5f5d`)
- `selectItemExpr` 默认增加别名 (`adcf3cd`)
- 主表默认别名改为 `T0` (`8aa2c31`)
- Benchmark 由 Service 方式改为 DAO 方式 (`e207344`)

### 修复
- 修复 Assembly 加载 bug (`05f8b3a`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### 新增功能
- 为 `ObjectViewDAO` 实现 ExprString 功能 (`fd0f746`)
- 完成 `ExprSqlConverter` 的 ToSql 实现 (`a41196e`)
- 新增 `ExprInterpolatedStringHandler` 用于插值字符串 SQL 构建 (`bf0f85d`)
- 新增 `ForeignExists` 方法 (`2a5960b`)
- 新增 Lambda 分表方式 (`b94ca29`)
- 完善 Expr API 合法性校验与测试 (`5c5ba35`)

### 改进
- 优化会话管理机制，`SessionManager` 生命周期完全由容器 Scope 维护 (`c3b52fc`)
- 优化 `ReplaceParam` 实现，使用字典树和 `ValueStringBuilder` (`35be944`)
- 增加 `DataViewDAO` 和 `ObjectViewDAO` 的 lambda 扩展方法 (`35be944`)
- 修改 `SelectItemExpr.Name` 为 `Alias` (`35be944`)
- 更新 `InterpolatedStringHandler` 条件为 `NET8_0_OR_GREATER || NET10_0_OR_GREATER` (`8f9819b`)
- `ExprInterpolatedStringHandler` 重构为使用 `DAOBase` 参数 (`25db9d9`)

### 重构
- 优化表创建机制 (`b567251`)
- 优化 Lambda converter (`8a97952`)
- 提取 `IDAOContext` 接口到 `LiteOrm.Common` (`3ebaf0c`)
- `ExprInterpolatedStringHandler` 移至 LiteOrm 项目 (`e292c23`)
- `SqlBuilder` 和 `CreateSqlBuildContext` 改为 internal (`e3f1eb8`)
- Refactor Result types and update DAO implementations (`aafea93`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### 新增功能
- 初始版本，完善 Expr API 合法性校验与测试 (`5c5ba35`, `2948732`)
