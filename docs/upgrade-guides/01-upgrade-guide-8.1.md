# LiteOrm 8.1 升级指南

本指南说明从 **8.0.20 及以下版本** 升级到 v8.1.x 需要改动的具体内容，按版本号组织：每个版本下再分「破坏性变更」「新特性」「改进」。

## 版本概览

| 包 | 新版本 |
|---|---|
| `LiteOrm` | 8.1.1 |
| `LiteOrm.Common` | 8.1.1 |
| `LiteOrm.DependencyInjection` | 8.1.1（v8.1.0 新增） |

---

## v8.1.1

### 破坏性变更

#### 1. DAO 构造函数注入（`SessionManager`）

> 适用于从 **v8.1.0 及更低版本** 升级到 **v8.1.1** 的用户（旧版 DAO 构造函数均无参数）。

v8.1.1 起，`DAOBase` 及各 DAO 基类（`ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>`）构造函数改为接收 `SessionManager` 参数，DAO 内部不再依赖静态 `SessionManager.Current`；`Current` 仅保留为外部调用入口。

- **依赖注入场景**（`RegisterLiteOrm()` / `AddLiteOrm()`）：无需任何改动，DI 容器自动解析 `SessionManager`。
- **手动构造场景**：需将 `sessionManager` 传入 DAO 构造函数：

```csharp
// 旧（v8.1.0 及更低）
var objectDAO = new ObjectDAO<User>();
var objectViewDAO = new ObjectViewDAO<User>();
var userService = new EntityService<User>(objectDAO, objectViewDAO);

// 新（v8.1.1）
var objectDAO = new ObjectDAO<User>(sessionManager);
var objectViewDAO = new ObjectViewDAO<User>(sessionManager);
var userService = new EntityService<User>(objectDAO, objectViewDAO);
```

- 自定义 DAO 若继承自 DAO 基类，构造函数需改为 `public MyDAO(SessionManager sessionManager) : base(sessionManager) { }`。
- `AddLiteOrm()` 在注册 `SessionManager` 时自动绑定 `SessionManager.Current`；`RegisterLiteOrm()` 的作用域跟踪默认启用，二者均无需配置。

#### 2. `DbValueType` 非空化与 `ConvertToDbValue` 签名调整

##### 2.1 `DbValueType` 新增 `Default`，`Column.DbType` 改为非空

`ColumnAttribute.DbType` 与 `ColumnDefinition.DbType` 由 `DbValueType?` 改为非空 `DbValueType`，默认值为 `DbValueType.Default`（`-1`），表示“未显式指定、运行时按属性类型自动推断”。

- 原先 `DbType == null` 判定“未指定”的逻辑改为 `DbType == DbValueType.Default`。
- 集合类型属性（`int[]`、`string[]`、`List<T>` 等）未显式指定时自动推断为 `DbValueType.Array`（此前为 `Json`）。
- `DbValueType` 新增 `Jsonb`（PostgreSQL 二进制 JSON）与 `Array`。

##### 2.2 `ConvertToDbValue` 参数类型替换

`IDbConverter.ConvertToDbValue` 的参数由 `System.Data.DbType` 替换为 `DbValueType`（默认 `DbValueType.Object`）。自定义 `IDbConverter` / `SqlBuilder` 实现需同步修改签名。

##### 2.3 `Param.DbType` 类型替换

`Param.DbType` 类型由 `DbType?` 改为 `DbValueType`（默认 `DbValueType.Default`）；`DbParameter.DbType` 仍在 `DAOBase.SetupCommand` 内通过 `DbValueTypeMap.ToDbType` 派生，数组列不设置 `DbParameter.DbType`。

### 新特性

#### 数组 / JSON 类型支持

- `DbValueType` 新增 `Array` / `Json` / `Jsonb`；集合类型属性自动推断为 `Array`，PostgreSQL 生成原生数组列（`integer[]`、`text[]` 等），其余方言回退为文本 JSON 存储。
- 新增 `LiteOrm.Pgsql` 命名空间与 PgSQL 专用 `Expr` 扩展（`ArrayToString`、`ArrayAppend`、`Any`、`JsonbExtractPath` 等），`ANY` 支持数组单参数绑定。
- 新增 `JsonExprExtensions` 公共 JSON 函数扩展（`JsonExtract`、`JsonValue`、`JsonContains` 等），并为 MySQL / SQLite / SQL Server / Oracle / PostgreSQL 注册各自原生 JSON 函数。

#### Service `SearchAs` 投影扩展

Service 层新增 IQueryable Lambda 形式的 `SearchAs` / `SearchOneAs` / `SearchAsAsync` / `SearchOneAsAsync` 扩展，可将结果投影为自定义类或匿名类（详见 [Lambda 查询指南](../02-core-usage/05-lambda-guide.md#6-投影查询searchas--searchoneas)）。

#### 计算列（非实际列）

`ColumnAttribute.Expression` + `ColumnMode.Computed` 支持计算列：不生成物理列、不参与插入/更新，SELECT 按表达式返回结果，查询条件按表达式生成（详见 [实体映射与数据源](../02-core-usage/01-entity-mapping.md)）。

#### `AddLiteOrm()` 绑定 `SessionManager.Current`

8.1.1 起，`AddLiteOrm()` 在注册 `SessionManager` 时自动绑定 `SessionManager.Current`（每个作用域内解析到该作用域实例），无需手写中间件或手动调用 `SessionManager.SetCurrent(...)`。

### 改进

- 非 AOT 模式自动注册改用运行时程序集扫描（`LiteOrmAutoRegistration.Apply()`），不再生成源代码；AOT 模式仍由源生成器编译期生成，按 `RuntimeFeature.IsDynamicCodeSupported` 自动分流。
- `AutoRegisterGenerator` 的 AOT 判定与 `TableInfoGenerator` 统一。
- Autofac 自动注册中，实现类型或其接口带 `[Service]` 特性（`IsService = true`）时自动应用 `ServiceInvokeInterceptor` 拦截，无需显式声明 `[Intercept]`。
- `RegisterLiteOrm()` 移除 `LiteOrmOptions.RegisterScope` 选项，作用域跟踪始终默认自动启用。

---

## v8.1.0

### 破坏性变更

#### 1. `RegisterLiteOrm()` 迁移至 `LiteOrm.DependencyInjection` 包

`RegisterLiteOrm()` 扩展方法从 `LiteOrm` 基础包移至 `LiteOrm.DependencyInjection` 包（新增），命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`。

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.0" />
```

`LiteOrm.DependencyInjection` 传递引用 `LiteOrm` 和 `LiteOrm.Common`，无需重复声明。

更新 `using`：

```csharp
// 旧（8.0.20 及以下版本）
using LiteOrm;

// 新（8.1.0）
using LiteOrm.DependencyInjection;
```

`RegisterLiteOrm()` 方法签名不变，调用方式无需改动。

#### 2. `BulkProvider` 用法变更（如有自定义实现）

`BulkProviderFactory`、`BulkProviderAttribute` 与 `[AutoRegister(Key = ...)]` 标记方式均已移除。自定义 `IBulkProvider` 不再需要任何标记，实现后直接设置到对应的 `SqlBuilder.BulkProvider` 属性即可。`GetSqlBuilder(typeof(MySqlConnection))` 返回的就是 `MySqlBuilder.Instance`，直接对其设置：

```csharp
// 旧：通过工厂按连接类型查找（已移除）
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// 新：直接设置到 SqlBuilder.BulkProvider
MySqlBuilder.Instance.BulkProvider = new MySqlBulkCopyProvider();
```

`SqlBuilder.BulkProvider` 未设置时返回 `null`，`BatchInsert`/`BatchInsertAsync` 自动回退到多值 INSERT 或逐条插入。

### 新特性

#### 基础库新增 `AddLiteOrm()` —— 纯 MS DI 注册（无 Autofac）

不引入 `LiteOrm.DependencyInjection` / Autofac 时，可直接在 `IServiceCollection` 上注册核心服务：

```csharp
using LiteOrm;

builder.Services.AddLiteOrm(options =>
{
    options.AutoRegisterServices = true;   // 默认 true：应用 [AutoRegister] 源生成注册
    options.ConfigureServices = services => { /* 追加自定义注册 */ };
});
```

`AddLiteOrm()` 注册核心服务、泛型 DAO / Service（`IEntityService<T>`、`IEntityViewService<T>`、`IObjectDAO<T>` 等），并应用 `[AutoRegister]` 服务的编译期注册。

#### `[AutoRegister]` 机制增强

- `[AutoRegister]` 特性可标注在基类上，派生类自动继承注册行为。
- `LiteOrm.Generators` 源生成器在编译期扫描 `[AutoRegister]` 类型并生成注册代码（等价于运行时反射扫描，但无需 `Assembly.GetTypes()`，支持 NativeAOT 裁剪）。`RegisterLiteOrm()` 与 `AddLiteOrm()` 均自动应用。
- 注册范围由 `[AutoRegister]` 的 `ServiceTypes` 枚举 `AutoRegisterServiceTypes` 控制：`All`（默认，注册实现类型自身与接口）、`Self`（仅自身）、`Interface`（仅接口）。原 `ServiceTypes` 的 `Type[]` 写法已移除。
- Service 与 DAO 基类（`EntityService<T>`、`ObjectDAO<T>` 等）已标注 `[AutoRegister(AutoRegisterServiceTypes.All, Lifetime = Lifetime.Scoped)]`，派生类自动继承；需指定接口注入时用 `AutoRegisterServiceTypes.Interface`，仅自身时用 `Self`。

#### AOT / NativeAOT 支持

- **net8.0 / net10.0** 目标为 AOT 兼容（`IsAotCompatible`），库可在 NativeAOT 与完全裁剪下工作。
- 使用 `PublishAot=true` 或启用裁剪构建时，`LiteOrm.Generators` 在编译期生成实体类型、`SqlBuilder` / `DbConnection` 类型、DataReader 映射委托与属性访问器的注册代码，运行时不依赖 `Expression.Compile()` 或 `Assembly.GetTypes()`。
- `Expr` 表达式树通过源生成的 `ExprJsonSerializerContext` 序列化，无反射，NativeAOT 安全。
- 使用 `LiteOrm.DependencyInjection` 的 AOP 拦截时，发布 AOT 应用需为 Castle DynamicProxy 启用模拟模式（`ProxyGenerator.EnableDynamicProxyEmulation()`，Castle.Core 5.1+）。

### 改进

#### 依赖包版本调整

netstandard2.0 / 2.1 目标的依赖包版本降至最低，减少与宿主应用的版本冲突：

- `Microsoft.Extensions.*`（Configuration.Abstractions、Logging.Abstractions、DependencyInjection.Abstractions 等）→ `2.2.0`
- `System.Text.Json` → `8.0.5`

---

## 常见问题（FAQ）

### Q1: 升级后 `IEntityService<T>` 无法从 DI 解析？

确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.DependencyInjection`）。核心类型（`EntityService<T>`、`ObjectDAO<T>` 等）不再通过 `[AutoRegister]` 扫描注册，改为由 `RegisterCoreServices()` 显式注册。

### Q2: 我的业务 Service 未显式指定 `ServiceTypes`，还能通过接口解析吗？

可以。`[AutoRegister]` 的 `ServiceTypes` 默认值为 `AutoRegisterServiceTypes.All`，会自动注册实现类型自身及其非 System 命名空间接口，因此依赖接口注入的用户自定义服务无需显式声明 `ServiceTypes`。若只想注册接口，可写 `[AutoRegister(AutoRegisterServiceTypes.Interface, Lifetime = Lifetime.Scoped)]`。

### Q3: 原来用 MS DI 的 `IServiceCollection` 注册的服务还能用吗？

可以。`RegisterLiteOrm()` 内部使用 `AutofacServiceProviderFactory` 桥接 MS DI，已有的 `services.AddXxx()` 注册仍然有效。如果不需要 Autofac / AOP，也可改用新的 `services.AddLiteOrm()`。

### Q4: 升级后 `appsettings.json` 需要修改吗？

不需要。`RegisterLiteOrm()` 会自动从宿主 `IConfiguration` 的 `LiteOrm` 节点加载数据源配置，原有配置写法保持不变。

### Q5: 为什么 `SessionManager.Current` 为空？

使用 `RegisterLiteOrm()` 或 `AddLiteOrm` 时会启用作用域跟踪，无需配置；手动管理场景中需调用 `SessionManager.SetCurrent(...)` 来设置当前会话。
