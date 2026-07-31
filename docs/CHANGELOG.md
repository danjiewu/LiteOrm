# 变更日志 (Changelog)

## v8.0.21 (Unreleased)

### 改进
- `PreparedSql` 移至 `LiteOrm.Common` 项目，参数类型由 `KeyValuePair` 改为自定义 `Param` (`f50c72e`)

---

## v8.0.20 (2026-07-28)

### 新增功能
- ExprString 新增 `RawSql` 标记类型 (`6f401b6`)
- 增加 CTE 递归关键字支持 (`81fade6`)
- 新增表级 `SyncTable` 配置 (`038e93b`)
- 新增 `ShortId` 工具类（数字加小写字母）(`18d70be`)
- `DAOContext` 新增 `Id` 属性及连续异常失效机制 (`18d70be`, `4831a82`)
- 新增 Remote/Server 身份认证机制，支持 `ClientId/Secret` 认证模式及多会话身份隔离 (`285de8b`, `37e0d2b`, `47eb3f1`, `b2e354b`)
- `RemoteInvoke` 新增 `RequestID` 用于请求追踪 (`e092218`)

### 改进
- `DatabaseSync` 补列时为非空值类型列追加默认值 (`8fd9662`)
- `SessionManager` 重构生命周期管理，`Current` 改为从当前 scope 实时解析 (`0698464`, `ce2435b`)
- `LiteOrmCoreInitializer` 注入 `IComponentContext` 替代 `SessionManager`，消除 captive dependency
- `HttpRemoteServiceTransport` 禁用 `HttpClient.UseCookies`，改由 `ICredentialsResolver` 管理票据 (`b456ab2`, `d322c04`, `37e0d2b`)

### 修复
- 修复 `ParamCountLimit` 配置无效 bug，默认值调整为 1000 (`e4fa04b`)

---

## v8.0.19 (2026-07-06)

### 新增功能
- 取消 `ExceptionHook` 机制，新增 `ExceptionHandling` 全局事件进行异常处理 (`f552b91`)
- 新增 `OnTableSyncing` 钩子，可按 `Type` 设定是否同步表 (`5f17866`)
- 自增列建表支持起始值和增量配置 (`a0a7d93`)
- 新增 `Expression<Func<T, T>>` 形式的 Update 方法 (`6060360`)

---

## v8.0.18 (2026-06-30)

### 新增功能
- 新增国产数据库 SqlBuilder 支持 (`cd73fb7`)
- 新增 `JsonRemoteServiceTransport` 传输实现 (`d8cddca`)
- Remote/Server 统一支持 `AutoRegisterEntityServices`，默认为 `true` (`edc3ffb`)

### 改进
- Expr 的 `Delete`、`Update` 改为 `DeleteAll`、`UpdateAll`，避免重名冲突 (`f71d27b`)

### 修复
- 修复 Server 端方法匹配失败问题 (`60b8e20`)
- 修复 Remote.Server 匹配泛型服务名称 bug (`2ea5e2c`)

---

## v8.0.17 (2026-06-18)

### 新增功能
- 新增 Remote 模块，支持远程代理模式 (`e01a660`)
- 新增 `CycleDetector` 检测 Expr 循环引用 (`02df339`)
- 新增三目运算符 (`a ? b : c`) 解析为 `CASE` 语句 (`eb0def4`)

### 重构
- 重构 `ExprVisitor` 和 `ExprValidator`，支持多种遍历和验证方式 (`0c0499c`)

### 修复
- 修复 Join 条件未指定优先级导致不能正确添加括号的 bug (`ebc87e6`)
- 修正默认 SqlBuilder 匹配方式，正确识别 PostgreSql 和 SqlServer (`e664272`)

---

## v8.0.16 (2026-05-27)

### 新增功能
- 新增 `Expr.Reduce` 扩展 (`c206a6d`)
- 新增 `PropertyOrder` 属性排序功能 (`7f7dd7e`)

### 重构
- `FromExpr` 和 `TableJoinExpr` 重构，支持使用子查询作为源 (`8ec2c1d`)

### 修复
- 修复 Timestamp 列未生效 bug (`378759d`)

---

## v8.0.15 (2026-05-10)

### 新增功能
- 增加 CTE 表达式支持 (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### 新增功能
- 新增 CodeGen 项目 (`c862ffd`)
- 新增 `StringExprConverter` 按实体类型的 `Parse`/`ParsePagedQuery` 方法 (`b4d422f`)

### 修复
- 修复 Insert 方法自增列非参数返回方式下的报错问题 (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### 新增功能
- 增加属性常量筛选机制 (`ad1148c`)
- `TableJoin` 支持指定外表主键 (`7cf1afc`)
- `ForeignType` 可声明多个 (`35f4e47`)

### 重构
- `LogicSet` 拆分为 `AndExpr` 和 `OrExpr` (`6dd1063`)

---

## v8.0.12 (2026-04-02)

### 新增功能
- 新增 `ExprValidator` 验证机制 (`2c9245e`)
- 新增 `TableExpr` 和 `TableJoinExpr` 及其序列化 (`1ee64b3`, `5b2a116`)
- 新增窗口函数支持 (`b7245d1`)
- 新增 `ExistsRelated` 方法，实现自动关联 (`6aa5ff2`)
- 新增 SqlGen ExprString 解析及 `ExprInterpolatedStringHandler` (`6eac5d5`, `bf0f85d`)
- 新增 `DDLGenerator` 用于生成建表 SQL (`fc91353`)
- 新增预生成实体类 `DataReaderConverter` (`8ac1ca6`)
- 新增 Lambda 分表方式 (`b94ca29`)
- 新增 `ForeignExists` 方法 (`2a5960b`)
- 新增自定义方法处理器和 SQL 构造器 (`31be232`)
- 新增 `IdentityIncreasement` 配置 (`894cc61`)
- 新增列默认值支持 (`07b30b5`)

### 改进
- 数据读取优化效率，采用动态编译方法读取 (`207fbe2`)
- 优化会话管理机制，`SessionManager` 生命周期完全由容器 Scope 维护 (`c3b52fc`)

### 修复
- 修复 Sqlite 的 `Now`、`Today` 时区问题 (`8e6e0ed`)
- 修复子查询生成 SQL bug (`b25e120`)

---

## v8.0.10 / v8.0.11 (2026-03-11)

### 新增功能
- 自定义 `SqlBuilder` 的注册和配置支持 (`60041c8`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### 新增功能
- 完成 `ExprSqlConverter` 的 ToSql 实现 (`a41196e`)
- 为 `ObjectViewDAO` 实现 ExprString 功能 (`fd0f746`)
- 完善 Expr API 合法性校验与测试 (`5c5ba35`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### 新增功能
- 初始版本，完善 Expr API 合法性校验与测试 (`5c5ba35`, `2948732`)