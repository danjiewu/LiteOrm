# 变更日志 (Changelog)

## v8.1.1 (2026-08-07)

### Breaking Changes
- `[AutoRegister]` 的 `ServiceTypes`（此前为 `Type[]`）已改为枚举 `AutoRegisterServiceTypes`：`All`（默认，实现类型自身 + 接口）、`Self`（仅自身）、`Interface`（仅接口）。原 `[AutoRegister(Lifetime.Scoped, typeof(IFoo))]` 写法请改为 `[AutoRegister(AutoRegisterServiceTypes.Interface, Lifetime = Lifetime.Scoped)]`。
- `DAOBase` 及派生 DAO（`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>`）构造函数需传入 `SessionManager`，不再依赖静态 `SessionManager.Current`。手动构造 DAO 时请传入 `sessionManager`；依赖注入场景由容器自动解析。`SessionManager.Current` 仅保留为外部使用入口，`AddLiteOrm()` 会自动将其绑定到当前作用域实例。

### 新增功能

- `RegisterLiteOrm()` 的 `LiteOrmOptions` 新增 `AutoRegisterServices` 选项（默认 `true`），设为 `false` 可跳过自动扫描注册 (`009d2c3`)
- `EntityService<T>`、`EntityViewService<T>`、`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>` 基类新增 `[AutoRegister(AutoRegisterServiceTypes.All, Lifetime = Lifetime.Scoped)]`，派生类自动继承注册行为。

### 改进

- 非 AOT 模式自动注册改用运行时程序集扫描（`LiteOrmAutoRegistration.Apply()`），不再生成源代码；AOT 模式仍由源生成器编译期生成，二者按 `RuntimeFeature.IsDynamicCodeSupported` 自动分流 (`009d2c3`)
- `AutoRegisterGenerator` 的 AOT 判定与 `TableInfoGenerator` 统一，读取 `build_property.enableaotanalyzer` / `enabletrimanalyzer` 等分析器属性 (`009d2c3`)
- Autofac 自动注册（`RegisterLiteOrm()`）中，实现类型或其接口带 `[Service]` 特性（`IsService = true`）时会自动应用 `ServiceInvokeInterceptor` 拦截，无需显式声明 `[Intercept]`。
- `RegisterLiteOrm()` 移除 `LiteOrmOptions.RegisterScope` 选项，作用域跟踪始终默认自动启用（`ScopeExtensions.RegisterScope` 仍保留为内部调用）。

---

## v8.1.0 (2026-08-02)

### Breaking Changes

本版本引入多项破坏性变更，详细迁移指南见 [8.1 升级指南](./upgrade-guides/01-upgrade-guide-8.1.md)。

- `RegisterLiteOrm()` 从 `LiteOrm` 基础包移至 `LiteOrm.DependencyInjection` 包（新增），命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`
- 自定义 `IBulkProvider` 不再使用任何特性标记，`BulkProviderFactory` 与 `BulkProviderAttribute` 已移除，改为直接设置 `SqlBuilder.BulkProvider` 属性 (`0f7fe25`)

### 新增功能
- 基础库新增 `AddLiteOrm()`：纯 MS DI 注册（无 Autofac / AOP），自动应用 `[AutoRegister]` 源生成注册 (`f1b2ef1`, `464b044`, `afecea3`)
- 新增 AOT / NativeAOT 支持：`LiteOrm.Generators` 源生成器在编译期生成实体 / DAO / Service / 类型注册代码，`ExprJsonConverter`、`LambdaExprConverter`、`DAOContextPoolFactory`、`SqlBuilderFactory` 等改为 AOT 安全实现 (`90d75f1`, `1205f4f`, `1eb9dc0`, `0058f05`, `3ca894c`, `a5cfa31`)
- 新增 `LiteOrm.DependencyInjection` 包（原宿主集成项目更名），DI 能力从基础库拆分独立 (`b45aeeb`, `0322465`, `b0b4177`)


### 改进
- `PreparedSql` 移至 `LiteOrm.Common` 项目，参数类型由 `KeyValuePair` 改为自定义 `Param` (`f50c72e`)
- 目标依赖包版本降低，减少版本冲突 (`ad695e6`)
- 宿主集成 / Remote 使用单例 `ProxyGenerator` 优化性能 (`8f8753d`)
- `AttributeTableInfoProvider` 不再依赖 `SqlBuilderFactory`、`DataSourceProvider` (`b50b49a`)
- 优化建表加锁机制，避免发生死锁 (`148f2ac`)
- DAO、Service 增加 AOT 相关特性标注 (`36641fa`, `0599305`, `1737234`, `e68ded4`)
- `ColumnDefinition.DbType` 可为空，运行时自动判定 DbType 类型 (`09bd95d`)

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