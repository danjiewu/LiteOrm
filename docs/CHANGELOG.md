# 变更日志 (Changelog)

## v8.1.4 (2026-08-24)

### 破坏性变更

- 复杂类型（数组/集合、`Json`/`Jsonb`、自定义类）属性**不再自动生成列为表列**，须显式标注 `[Column]`（并按需指定 `DbType = Array`/`Json`/`Jsonb`）；已知标量（数值、`string`/`char`、`byte[]`、`Guid`、日期、枚举）仍自动映射。
- `EntityService<T>` / `EntityService<T, TView>` / `EntityViewService<T>` 构造函数改为接收 `IServiceProvider`，由容器解析所需的 `ObjectDAO<T>` / `ObjectViewDAO<T>`；派生服务构造函数同步调整，依赖注入场景无需改动。

### 改进

- `ColumnAttribute` / `ForeignColumnAttribute` 均新增 `ConverterType` 用于声明列级转换器；`ForeignColumn` 读取外键投影列时**优先自身声明的转换器，否则回退目标列**。
- 数值类型互转现已默认注册（覆盖 `decimal`/`float`/`double` 与整型族互转，如 `Decimal→Int32`），跨数值类型读写无需手动注册。
- 值转换机制优化：转换统一收敛为「委托式转换器」，解析优先级固定为 列级转换器 → 按 (值类型, 数据库取值类型) 的注册表 → 直接赋值；`null`/`DBNull`/空字符串 等空值统一提前短路，未注册或无需转换时不再做通用兜底。
- 补全各数据库通用 SQL 函数的方言注册，C# 方法名经 `LambdaExprConverter` 转为 `FunctionExpr` 后不再因函数名不匹配或方言差异生成无效 SQL：

  | 函数 | 基类 (SQLite/MySQL/SQLServer) | Oracle | PostgreSQL | SQL Server |
  |------|------|--------|------------|------------|
  | `ToLower` | `LOWER` | — | — | — |
  | `ToUpper` | `UPPER` | — | — | — |
  | `Pow` | `POWER` | — | — | — |
  | `Char` | `CHAR` | `CHR` | `CHR` | — |
  | `Ceiling` | `CEILING` | `CEIL` | `CEIL` | — |
  | `Truncate` | `TRUNCATE(x,0)` | `TRUNC` | `TRUNC` | `ROUND(x,0,1)` |
  | `Concat` | `CONCAT` | `||` | — | — |
  | `Log` | `LOG` | `LN` | `LN` | — |
  | `Log10` | `LOG10` | `LOG(10,x)` | `LOG` | — |
  | `Atan2` | `ATAN2` | — | — | `ATN2` |
  | `Max` (标量) | `GREATEST` | — | — | — |
  | `Min` (标量) | `LEAST` | — | — | — |
  | `Max`/`Min` (SQLite) | — | — | — | `max`/`min` |

  - `Max`/`Min` 通过 `IsAggregate` 区分聚合（`MAX`/`MIN`）与标量（`GREATEST`/`LEAST`）；SQLite 的 `max`/`min` 同时支持标量与聚合。
  - `Abs`、`Round`、`Floor`、`Sqrt`、`Exp`、`Sin`/`Cos`/`Tan`/`Asin`/`Acos`/`Atan`、`Sign`、`Replace`、`Coalesce`、`Upper`/`Lower`（ExprExtensions 形式）等标准 SQL 同名函数无需注册，默认渲染即可跨数据库工作。
- `ISqlBuilder` 新增 `TryAppendSqlLiteral` 方法：字符串常量（`Expr.Const(string)`）渲染时，仅含常规字符（无反斜杠、控制字符）的字符串以 `'value'` 形式直接内联（单引号以 `''` 转义），含特殊字符的仍走参数化，兼顾安全与性能。计算列表达式中可使用 `Expr.Const(" ")` 等字符串常量。

### 修复

- 修正自定义查询 `SearchAs`/`SearchOneAs` 中，当自定义 `SelectItem` 的列名与结果属性名不一致且未显式指定别名时无法正确读取结果的问题（自动补充 `AS` 子句）。
- 修复部分调用点将实体对象整体当作裸值绑定到驱动导致的 `No mapping exists from object type ...` 错误。
- `SortProperty` 排除索引器属性，修复内置 `Item`（索引器）与自定义 `Item` 属性重名导致的循环依赖误判。
- `SearchAsAsync` / `SearchOneAsAsync` 对齐接口补充 `CancellationToken` 参数，修复 `EntityViewService<T>` / `RemoteViewServiceAsyncProxy<T>` 未实现接口成员的问题。

---

## v8.1.3 (2026-08-18)

### 破坏性变更

- 统一使用 `DbValueType` 替代 `DbType`，仅在数据库操作边界转换为 `System.Data.DbType`。合并 `DbTypeMap` 至 `DbValueTypeMap`。
- `IDbConverter.GetDbType(Type)` 改为 `GetDbValueType(Type)`；新增 `GetDefaultLength(DbValueType)`。
- `DbValueType.Array` 改为掩码（值 128），可与标量类型按位或组合（如 `DbValueType.Int32 | DbValueType.Array`）。
- `Expr.Cast` / `SqlBuilder.GetSqlTypeName` / `GetDefaultLength` 参数改为 `DbValueType`。

### 改进

- 计算列支持 `ValueTypeExpr` 形式表达式（`ColumnDefinition.ExpressionExpr`）。
- `DataReaderConverter` 在 `DbValueType.Default` 时通过当前 `SqlBuilder` 推断方言相关的读取器类型。

---

## v8.1.2 (2026-08-17)

### 优化 AOT 编译

- **修复 AutoRegisterGenerator 枚举/属性名错位**：生成器将 `AutoRegisterServiceTypes` 改为 `RegisterPolicy`，命名参数 `ServiceTypes` 改为 `Policy`。
- **移除全局裁剪告警抑制**（`SuppressTrimAnalysisWarnings`），添加 `DynamicallyAccessedMembers` 注解链与 `UnconditionalSuppressMessage`，使裁剪/AOT 告警在编译期可见。

### 新特性

- **计算列支持 `ValueTypeExpr` 形式表达式**：`ColumnDefinition.ExpressionExpr` 属性允许使用 `Expr.Prop("Price") * Expr.Prop("Quantity")` 等 Expr 树动态设置计算列表达式；渲染时通过 `ExprSqlConverter` 转为 SQL，仅允许不生成参数的固定 SQL（属性引用、常量、函数、算术运算），产生参数时抛 `NotSupportedException`。与字符串形式 `Expression` 同时设置时优先使用 Expr 树形式。

---

## v8.1.1 (2026-08-07)

### 破坏性变更
- `[AutoRegister]` 的 `ServiceTypes`（此前为 `Type[]`）已改为枚举 `RegisterPolicy`：`All`（默认，实现类型自身 + 接口）、`Self`（仅自身）、`Interface`（仅接口）。原 `[AutoRegister(Lifetime.Scoped, typeof(IFoo))]` 写法请改为 `[AutoRegister(RegisterPolicy.Interface, Lifetime = Lifetime.Scoped)]`。
- `DAOBase` 及派生 DAO（`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>`）构造函数需传入 `SessionManager`，不再依赖静态 `SessionManager.Current`。手动构造 DAO 时请传入 `sessionManager`；依赖注入场景由容器自动解析。`SessionManager.Current` 仅保留为外部使用入口，`AddLiteOrm()` 会自动将其绑定到当前作用域实例。
- `ColumnAttribute.DbType` 与 `ColumnDefinition.DbType` 由 `DbValueType?` 改为非空 `DbValueType`，默认值为新增的 `DbValueType.Default`（表示未显式指定、运行时按属性类型推断）。原 `DbType == null` 判空逻辑改为 `DbType == DbValueType.Default`。
- `IDbConverter.ConvertToDbValue` 的参数由 `DbType` 改为 `DbValueType`（默认 `DbValueType.Object`），不再接受 `DbType` 参数。
- `Param.DbType` 类型由 `DbType?` 改为 `DbValueType`（默认 `DbValueType.Default`）。
- `DbValueType` 枚举新增 `Default = -1`、`Jsonb = 29`、`Array = 30`；集合类型属性未显式指定类型时自动推断为 `Array`（此前推断为 `Json`）。

### 新特性

- `RegisterLiteOrm()` 的 `LiteOrmOptions` 新增 `AutoRegisterServices` 选项（默认 `true`），设为 `false` 可跳过自动扫描注册 (`009d2c3`)
- `EntityService<T>`、`EntityViewService<T>`、`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>` 基类新增 `[AutoRegister(RegisterPolicy.All, Lifetime = Lifetime.Scoped)]`，派生类自动继承注册行为。
- 数组类型支持：集合属性自动推断为 `DbValueType.Array`；PostgreSQL 生成原生数组列（`integer[]`、`text[]` 等），其余方言回退文本 JSON 存储。
- PostgreSQL 数组函数：新增 `array_to_string`、`array_append`、`ANY` 等函数的解析与 SQL 生成，`ANY` 支持数组作为单参数绑定。
- 新增 `LiteOrm.Pgsql` 命名空间，提供 `ValueTypeExpr` 的 PgSQL 专用扩展（`ArrayToString`、`ArrayAppend`、`Any`、`Contains`、`JsonbExtractPath`、`JsonbExtractPathText`、`JsonbContains`、`JsonbBuildObject`、`JsonbBuildArray`）。
- JSON/JSONB 类型：`DbValueType.Json`/`Jsonb` 支持，PostgreSQL 生成 `JSON`/`JSONB` 列，MySQL 生成 `JSON` 列。
- 新增 `JsonExprExtensions` 公共 JSON 函数扩展（`JsonExtract`、`JsonValue`、`JsonQuery`、`JsonContains`、`JsonObject`、`JsonArray`、`IsJson`），并为 MySQL / SQLite / SQL Server / Oracle / PostgreSQL 注册各自原生 JSON 函数。
- Service 新增 Lambda 方式的 `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync` 扩展（`Expression<Func<IQueryable<T>, IQueryable<TResult>>>` 投影形式）。
- 实体支持计算列（非实际列）：`ColumnAttribute.Expression` + `ColumnMode.Computed`，不生成物理列、以表达式返回结果并生成查询条件。

### 改进

- 非 AOT 模式自动注册改用运行时程序集扫描（`LiteOrmAutoRegistration.Apply()`），不再生成源代码；AOT 模式仍由源生成器编译期生成，二者按 `RuntimeFeature.IsDynamicCodeSupported` 自动分流 (`009d2c3`)
- `AutoRegisterGenerator` 的 AOT 判定与 `TableInfoGenerator` 统一，读取 `build_property.enableaotanalyzer` / `enabletrimanalyzer` 等分析器属性 (`009d2c3`)
- Autofac 自动注册（`RegisterLiteOrm()`）中，实现类型或其接口带 `[Service]` 特性（`IsService = true`）时会自动应用 `ServiceInvokeInterceptor` 拦截，无需显式声明 `[Intercept]`。
- `RegisterLiteOrm()` 移除 `LiteOrmOptions.RegisterScope` 选项，作用域跟踪始终默认自动启用（`ScopeExtensions.RegisterScope` 仍保留为内部调用）。
- `ConvertFromDbValue` 支持数组/集合值到 `List<T>` 等目标集合的转换。
- 源生成器 `TableInfoGenerator` 适配非空 `DbValueType` 生成。

---

## v8.1.0 (2026-08-02)

### 破坏性变更

本版本引入多项破坏性变更，详细迁移指南见 [8.1 升级指南](./upgrade-guides/01-upgrade-guide-8.1.md)。

- `RegisterLiteOrm()` 从 `LiteOrm` 基础包移至 `LiteOrm.DependencyInjection` 包（新增），命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`
- 自定义 `IBulkProvider` 不再使用任何特性标记，`BulkProviderFactory` 与 `BulkProviderAttribute` 已移除，改为直接设置 `SqlBuilder.BulkProvider` 属性 (`0f7fe25`)

### 新特性
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

### 新特性
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

### 新特性
- 取消 `ExceptionHook` 机制，新增 `ExceptionHandling` 全局事件进行异常处理 (`f552b91`)
- 新增 `OnTableSyncing` 钩子，可按 `Type` 设定是否同步表 (`5f17866`)
- 自增列建表支持起始值和增量配置 (`a0a7d93`)
- 新增 `Expression<Func<T, T>>` 形式的 Update 方法 (`6060360`)

---

## v8.0.18 (2026-06-30)

### 新特性
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

### 新特性
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

### 新特性
- 新增 `Expr.Reduce` 扩展 (`c206a6d`)
- 新增 `PropertyOrder` 属性排序功能 (`7f7dd7e`)

### 重构
- `FromExpr` 和 `TableJoinExpr` 重构，支持使用子查询作为源 (`8ec2c1d`)

### 修复
- 修复 Timestamp 列未生效 bug (`378759d`)

---

## v8.0.15 (2026-05-10)

### 新特性
- 增加 CTE 表达式支持 (`cc4f8c2`)

---

## v8.0.14 (2026-04-28)

### 新特性
- 新增 CodeGen 项目 (`c862ffd`)
- 新增 `StringExprConverter` 按实体类型的 `Parse`/`ParsePagedQuery` 方法 (`b4d422f`)

### 修复
- 修复 Insert 方法自增列非参数返回方式下的报错问题 (`073b4f7`)

---

## v8.0.13 (2026-04-10)

### 新特性
- 增加属性常量筛选机制 (`ad1148c`)
- `TableJoin` 支持指定外表主键 (`7cf1afc`)
- `ForeignType` 可声明多个 (`35f4e47`)

### 重构
- `LogicSet` 拆分为 `AndExpr` 和 `OrExpr` (`6dd1063`)

---

## v8.0.12 (2026-04-02)

### 新特性
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

### 新特性
- 自定义 `SqlBuilder` 的注册和配置支持 (`60041c8`)

---

## v8.0.8 / v8.0.9 (2026-03-06)

### 新特性
- 完成 `ExprSqlConverter` 的 ToSql 实现 (`a41196e`)
- 为 `ObjectViewDAO` 实现 ExprString 功能 (`fd0f746`)
- 完善 Expr API 合法性校验与测试 (`5c5ba35`)

---

## v8.0.0 ~ v8.0.7 (2026-02-11)

### 新特性
- 初始版本，完善 Expr API 合法性校验与测试 (`5c5ba35`, `2948732`)