# LiteOrm 8.1 升级指南

本指南说明从 **8.0.20 及以下版本** 升级到 v8.1.0 需要改动的具体内容。

## 版本概览

| 包 | 新版本 |
|---|---|
| `LiteOrm` | 8.1.0 |
| `LiteOrm.Common` | 8.1.0 |
| `LiteOrm.DependencyInjection` | 8.1.0（新增） |

---

## 迁移步骤

### 第 1 步：引用 `LiteOrm.DependencyInjection` 包

`RegisterLiteOrm()` 扩展方法从 `LiteOrm` 基础包移至 `LiteOrm.DependencyInjection` 包，命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`。

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

### 第 2 步：更新 `BulkProvider` 使用方式（如有自定义实现）

`BulkProviderFactory`、`BulkProviderAttribute` 与 `[AutoRegister(Key = ...)]` 标记方式均已移除。自定义 `IBulkProvider` 不再需要任何标记，实现后直接设置到对应的 `SqlBuilder.BulkProvider` 属性即可。`GetSqlBuilder(typeof(MySqlConnection))` 返回的就是 `MySqlBuilder.Instance`，直接对其设置：

```csharp
// 旧：通过工厂按连接类型查找（已移除）
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// 新：直接设置到 SqlBuilder.BulkProvider
MySqlBuilder.Instance.BulkProvider = new MySqlBulkCopyProvider();
```

`SqlBuilder.BulkProvider` 未设置时返回 `null`，`BatchInsert`/`BatchInsertAsync` 自动回退到多值 INSERT 或逐条插入。

### 第 3 步：`DataSourceProvider` 改为显式配置（仅直接使用基础库时）

`DataSourceProvider` 不再通过 `[AutoRegister]` 注册，也不再从构造函数读取 `IConfiguration`。直接使用基础库（不使用 DI 包）时，需通过 `AddDataSource` 显式添加，或通过 `LoadConfiguration` 从 `IConfiguration` 加载：

```csharp
var provider = new DataSourceProvider();
provider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=myapp.db",
    Provider = typeof(Microsoft.Data.Sqlite.SqliteConnection).AssemblyQualifiedName,
    SyncTable = true
});
provider.SetDefaultDataSource("DefaultConnection");
```

使用 `RegisterLiteOrm()`（DI 场景）时无需改动，`DataSourceProviderExtensions.LoadConfiguration` 会自动从宿主 `IConfiguration` 的 `LiteOrm` 节点加载。

---

## 新增功能

### 基础库新增 `AddLiteOrm()` —— 纯 MS DI 注册（无 Autofac）

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

### `[AutoRegister]` 机制增强

- `[AutoRegister]` 特性可标注在基类上，派生类自动继承注册行为。
- `LiteOrm.Generators` 源生成器在编译期扫描 `[AutoRegister]` 类型并生成注册代码（等价于运行时反射扫描，但无需 `Assembly.GetTypes()`，支持 NativeAOT 裁剪）。`RegisterLiteOrm()` 与 `AddLiteOrm()` 均自动应用。

### AOT / NativeAOT 支持

- **net8.0 / net10.0** 目标为 AOT 兼容（`IsAotCompatible`），库可在 NativeAOT 与完全裁剪下工作。
- 使用 `PublishAot=true` 或启用裁剪构建时，`LiteOrm.Generators` 在编译期生成实体类型、`SqlBuilder` / `DbConnection` 类型、DataReader 映射委托与属性访问器的注册代码，运行时不依赖 `Expression.Compile()` 或 `Assembly.GetTypes()`。
- `Expr` 表达式树通过源生成的 `ExprJsonSerializerContext` 序列化，无反射，NativeAOT 安全。
- 使用 `LiteOrm.DependencyInjection` 的 AOP 拦截时，发布 AOT 应用需为 Castle DynamicProxy 启用模拟模式（`ProxyGenerator.EnableDynamicProxyEmulation()`，Castle.Core 5.1+）。

### 依赖包版本调整

netstandard2.0 / 2.1 目标的依赖包版本降至最低，减少与宿主应用的版本冲突：

- `Microsoft.Extensions.*`（Configuration.Abstractions、Logging.Abstractions、DependencyInjection.Abstractions 等）→ `2.2.0`
- `System.Text.Json` → `8.0.5`

---

## 常见问题（FAQ）

### Q1: 升级后 `IEntityService<T>` 无法从 DI 解析？

确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.DependencyInjection`）。核心类型（`EntityService<T>`、`ObjectDAO<T>` 等）不再通过 `[AutoRegister]` 扫描注册，改为由 `RegisterCoreServices()` 显式注册。

### Q2: 我的业务 Service 未显式指定 `ServiceTypes`，还能通过接口解析吗？

可以。未显式指定 `ServiceTypes` 时，会自动推断实现类型的非系统命名空间接口作为服务类型。依赖接口注入的用户自定义服务无需显式声明 `ServiceTypes`。

### Q3: 原来用 MS DI 的 `IServiceCollection` 注册的服务还能用吗？

可以。`RegisterLiteOrm()` 内部使用 `AutofacServiceProviderFactory` 桥接 MS DI，已有的 `services.AddXxx()` 注册仍然有效。如果不需要 Autofac / AOP，也可改用新的 `services.AddLiteOrm()`。

### Q4: 升级后 `appsettings.json` 需要修改吗？

不需要。`RegisterLiteOrm()` 会自动从宿主 `IConfiguration` 的 `LiteOrm` 节点加载数据源配置，原有配置写法保持不变。

---

## 验证

升级后请确保：

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

完整测试套件全部通过是本版本验证基线。
