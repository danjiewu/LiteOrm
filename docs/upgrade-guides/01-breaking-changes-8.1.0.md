# LiteOrm 8.1.0 升级指南与 Breaking Changes

本指南面向从 `v8.0.x` 升级到 `v8.1.0` 的用户，详细说明本次版本引入的破坏性变更、影响范围与迁移方法。

## 版本概览

| 包 | 旧版本 | 新版本 |
|---|---|---|
| `LiteOrm` | 8.0.21 | 8.1.0 |
| `LiteOrm.Common` | 8.0.21 | 8.1.0 |
| `LiteOrm.Framework` | 8.0.22 | 8.1.0 |

> 说明：`LiteOrm.Remote`（8.0.3）与 `LiteOrm.Remote.Server`（8.0.3）版本号保持不变。

---

## 变更一览

| # | 变更 | 破坏性程度 |
|---|---|---|
| 1 | `AutoRegisterAttribute` 从 `LiteOrm.Common` 迁移到 `LiteOrm.Framework`，命名空间改为 `LiteOrm.Framework` | 高 |
| 2 | DI 注册入口合并为唯一的 `RegisterLiteOrm()`（Autofac + Castle），`RegisterLiteOrmFramework()` 与 MS DI 版 `RegisterLiteOrm()` 移除 | 高 |
| 3 | `AutoRegisterAttribute.Lifetime` 改为内置 `ServiceLifetime`，默认值由 `Singleton` 改为 `Scoped` | 中 |
| 4 | `LiteOrm` 核心 DAO/Service 不再使用 `[AutoRegister]` 自动注册，改为 `LiteOrm.Framework` 显式注册 | 高 |
| 5 | `LiteOrm` 核心项目移除 `Microsoft.Extensions.Hosting` 依赖，DI 集成仅由 `LiteOrm.Framework` 提供 | 高 |
| 6 | `DataSourceProvider` 改为显式配置 API，不再接受 `IConfiguration` | 中 |
| 7 | `BulkProvider` 改用 `BulkProviderAttribute` 标记数据库连接类型 | 中 |
| 8 | 核心接口上用于标记排除的 `[AutoRegister(false)]` 被移除 | 低 |

---

## 1. `AutoRegisterAttribute` 迁移到 `LiteOrm.Framework`

### 变更内容

`AutoRegisterAttribute` 从 `LiteOrm.Common` 程序集迁移到 `LiteOrm.Framework` 程序集。

- **命名空间改变**：由 `LiteOrm.Common` 改为 `LiteOrm.Framework`。源码中所有使用 `[AutoRegister]` 的文件需要增加 `using LiteOrm.Framework;`。
- **程序集归属改变**：特性类型现位于 `LiteOrm.Framework.dll`。

### 影响范围

- 仅引用 `LiteOrm` / `LiteOrm.Common` 且未引用 `LiteOrm.Framework` 的项目，无法再编译包含 `[AutoRegister]` 的代码。
- 使用 `LiteOrm.Common` 命名空间而未加 `using LiteOrm.Framework;` 的源码文件将编译失败。
- 依赖程序集级（assembly-level）解析此类型的代码（如 `typeof(AutoRegisterAttribute).Assembly`）会返回 `LiteOrm.Framework` 程序集。

### 迁移方法

1. 确保使用 `[AutoRegister]` 的项目引用 `LiteOrm.Framework` 包：
   ```xml
   <PackageReference Include="LiteOrm.Framework" Version="8.1.0" />
   ```
2. 在使用 `[AutoRegister]` 的源码文件顶部增加：
   ```csharp
   using LiteOrm.Framework;
   ```

`Lifetime` 枚举已移除，`AutoRegisterAttribute.Lifetime` 直接使用内置的 `ServiceLifetime`（见第 3 节）。

---

## 2. DI 注册入口合并为唯一的 `RegisterLiteOrm()`

### 变更内容

`LiteOrm.Framework` 原先提供两套 DI 集成入口，现已合并为一套：

- ~~`RegisterLiteOrmFramework()`~~（Autofac + Castle DynamicProxy）→ **已删除**
- ~~`RegisterLiteOrm()`~~（MS DI）→ **已删除**
- **`RegisterLiteOrm()`**（Autofac + Castle DynamicProxy）→ 唯一的注册入口

合并后的 `RegisterLiteOrm()` 位于 `LiteOrm.Framework` 命名空间的 `LiteOrmServiceExtensions` 中，使用 Autofac 容器并启用 Castle DynamicProxy 拦截器支持。`LiteOrm.Framework\FrameworkServiceExtensions.cs` 已被删除。

### 影响范围

- 使用 `RegisterLiteOrmFramework()` 的项目需将方法调用改为 `RegisterLiteOrm()`。
- 旧版 `LiteOrm` 核心包中的 `RegisterLiteOrm()`（MS DI 版本，位于 `LiteOrm/Classes/LiteOrmServiceExtensions.cs`）已随核心包移除 Hosting 依赖而删除。

### 迁移方法

```csharp
// 旧方式（任何入口）
Host.CreateDefaultBuilder(args).RegisterLiteOrmFramework();
// 或
Host.CreateDefaultBuilder(args).RegisterLiteOrm();

// 新方式（唯一入口）
Host.CreateDefaultBuilder(args).RegisterLiteOrm();
```

可选参数 `Action<LiteOrmOptions>` 保持不变：

```csharp
Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm(options =>
    {
        options.Assemblies = new[] { typeof(MyService).Assembly };
        options.RegisterSqlBuilder("MyDataSource", new MySqlBuilder());
    });
```

---

## 3. `AutoRegisterAttribute.Lifetime` 默认值改为 `Scoped`，枚举改为内置 `ServiceLifetime`

### 变更内容

- `AutoRegisterAttribute.Lifetime` 的类型由自定义 `Lifetime` 枚举改为 .NET 内置的 `ServiceLifetime`（`Microsoft.Extensions.DependencyInjection` 命名空间），原 `LiteOrm.Common` 中的 `Lifetime` 枚举已移除。
- `AutoRegisterAttribute.Lifetime` 的默认值由 `Singleton` 改为 `Scoped`。未显式指定生命周期时，自动注册的服务现在默认按作用域（每请求/每 `LifetimeScope` 一个实例）创建。

### 影响范围

- 使用 `[AutoRegister]` 且**未显式指定生命周期**的类型，其注册行为从单例变为作用域。
- 引用 `LiteOrm.Common` 中 `Lifetime` 枚举的代码需要改用 `ServiceLifetime`（需 `using Microsoft.Extensions.DependencyInjection;`）。
- 显式指定了 `Lifetime`（如 `[AutoRegister(ServiceLifetime.Singleton)]`）的类型不受影响。

### 迁移方法

- 将 `[AutoRegister(Lifetime.Scoped)]` 等写法替换为 `[AutoRegister(ServiceLifetime.Scoped)]`，并添加 `using Microsoft.Extensions.DependencyInjection;`。
- 如果你希望某个类型保持单例，请显式声明：
  ```csharp
  [AutoRegister(ServiceLifetime.Singleton)]
  public class MyService : IMyService { }
  ```
- 无状态服务建议显式声明 `ServiceLifetime.Transient` 以获得更小的占用：
  ```csharp
  [AutoRegister(ServiceLifetime.Transient)]
  public class MyService : IMyService { }
  ```

---

## 4. `LiteOrm` 核心 DAO/Service 移除 `[AutoRegister]` 自动注册

### 变更内容

`LiteOrm` 核心包中的以下类型**不再携带 `[AutoRegister]` 特性**，因此不会再被 `RegisterAutoService` 程序集扫描自动注册到 DI 容器：

- `EntityService<T, TView>`、`EntityService<T>`、`EntityViewService<TView>`
- `ObjectDAO<T>`、`ObjectViewDAO<T>`、`DataDAO<T>`、`DataViewDAO<T>`、`DAOBase`
- `AttributeTableInfoProvider`、`BulkProviderFactory`、`DdlGen`

### 影响范围

在 `v8.0.x` 中，这些核心类型依赖 `[AutoRegister]` 特性被自动扫描注册。升级后，**如果只做包升级而继续沿用旧的自动注册流程，上述类型将不再被注册**，从 DI 容器解析 `IEntityService<T>`、`ObjectDAO<T>` 等会失败。

### 迁移方法

`LiteOrm.Framework` 的 `AddCoreLiteOrmServices()`（由唯一的 `RegisterLiteOrm()` 调用）已改为**显式注册**这些核心类型，因此使用 `Host` 集成（推荐方式）的用户无需任何改动：

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

显式注册的类型包括：

```csharp
services.AddScoped(typeof(EntityService<>));
services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));
services.AddScoped(typeof(IEntityServiceAsync<>), typeof(EntityService<>));
services.AddScoped(typeof(EntityViewService<>));
services.AddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
services.AddScoped(typeof(IEntityViewServiceAsync<>), typeof(EntityViewService<>));
services.AddScoped(typeof(ObjectDAO<>));
services.AddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
services.AddScoped(typeof(ObjectViewDAO<>));
services.AddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
services.AddScoped(typeof(DataDAO<>));
services.AddScoped(typeof(DataViewDAO<>));
services.AddScoped(typeof(IDataViewDAO<>), typeof(DataViewDAO<>));
services.AddSingleton<TableInfoProvider, AttributeTableInfoProvider>();
services.AddSingleton<BulkProviderFactory>();
```

> 注意：不再通过 `[AutoRegister]` 扫描注册意味着**扫描范围缩小**。核心类型不再依赖反射扫描，注册行为更加确定。用户自定义类型（如业务 Service）仍可继续使用 `[AutoRegister]` 特性（定义于 `LiteOrm.Framework`）进行自动注册。对于依赖接口注入的用户自定义服务，建议显式声明 `[AutoRegister(ServiceLifetime.Scoped, typeof(IMyService))]`。

---

## 5. `LiteOrm` 核心移除 Hosting 依赖

### 变更内容

`LiteOrm` 核心项目的 `Microsoft.Extensions.Hosting` 包引用被移除，只保留 `Microsoft.Extensions.Logging.Abstractions`（用于 `ILogger` 日志调用）。

以下类型已从 `LiteOrm` 核心项目**移至 `LiteOrm.Framework`**：

| 原位置（LiteOrm） | 新位置（LiteOrm.Framework） |
|---|---|
| `LiteOrmServiceExtensions`（MS DI 扩展 `RegisterLiteOrm`） | 已删除，由 `LiteOrmServiceExtensions`（Autofac 版 `RegisterLiteOrm`）取代 |
| `LiteOrmCoreInitializer`（`IHostedService` 表结构同步） | `LiteOrm.Framework\LiteOrmCoreInitializer.cs` |

### 影响范围

- 使用 `IHostBuilder.RegisterLiteOrm()` 扩展方法的项目需引用 `LiteOrm.Framework`。
- DI 集成的唯一入口为 `LiteOrm.Framework` 中的 `RegisterLiteOrm()`（Autofac + Castle DynamicProxy）。

### 迁移方法

```csharp
// 旧方式（仅引用 LiteOrm）
Host.CreateDefaultBuilder(args).RegisterLiteOrm();

// 新方式（需引用 LiteOrm.Framework）
Host.CreateDefaultBuilder(args).RegisterLiteOrm();
```

---

## 6. `DataSourceProvider` 改为显式配置 API

### 变更内容

`LiteOrm` 核心的 `DataSourceProvider` **不再接受 `IConfiguration`**。连接配置通过显式 API 手工提供：

```csharp
var provider = new DataSourceProvider();
provider.AddDataSource(new DataSourceConfig
{
    Name = "DefaultConnection",
    ConnectionString = "Data Source=demo.db",
    Provider = typeof(SqliteConnection).AssemblyQualifiedName
});
provider.SetDefaultDataSource("DefaultConnection");
```

新增的链式 API：

| 方法 | 说明 |
|---|---|
| `AddDataSource(DataSourceConfig)` | 添加/覆盖数据源，返回 `this` 支持链式调用 |
| `SetDefaultDataSource(string)` | 设置默认数据源 |
| `RemoveDataSource(string)` | 移除数据源 |
| `GetDataSource(string?)` | 获取数据源（空名回落到默认/首个数据源） |

### 影响范围

- `LiteOrm` 核心中，凡是从 `IConfiguration` 构造 `DataSourceProvider` 的代码需要改写。
- 使用 `LiteOrm.Framework` 的用户**无感知**：`LiteOrm.Framework` 新增 `DataSourceProviderExtensions.LoadConfiguration(IConfiguration)`，从宿主配置的 `LiteOrm` 节点加载数据源并填充到 `DataSourceProvider`，并在 `AddCoreLiteOrmServices()` 中自动完成。

### 迁移方法（手动使用核心包时）

```csharp
// 旧方式
var provider = new DataSourceProvider(configuration.GetSection("LiteOrm"));

// 新方式（核心）
var provider = new DataSourceProvider();
provider.AddDataSource(new DataSourceConfig { ... });

// 新方式（Framework，自动从 appsettings.json 加载）
// RegisterLiteOrm() 内部已处理
```

---

## 7. `BulkProvider` 改用 `BulkProviderAttribute`

### 变更内容

自定义 `IBulkProvider` 实现原先通过 `[AutoRegister(Key = typeof(XxxConnection))]` 标记其对应的数据库连接类型。现在改为使用新增的 `BulkProviderAttribute`：

```csharp
// 旧方式
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }

// 新方式
[BulkProvider(typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }
```

`BulkProviderFactory` 现从 `BulkProviderAttribute.DbConnectionType` 读取映射。

### 影响范围

自定义 `IBulkProvider` 实现的类需更新特性。未自定义 BulkProvider 的用户无感知。

### 迁移方法

将类上的 `[AutoRegister(Key = typeof(Conn))]` 替换为 `[BulkProvider(typeof(Conn))]`。`BulkProviderAttribute` 定义于 `LiteOrm.Common`，位于 `LiteOrm.Common.Attributes` 命名空间，可直接使用。

---

## 8. 核心接口移除 `[AutoRegister(false)]` 标记

### 变更内容

以下非泛型标记接口上的 `[AutoRegister(false)]` 被移除（用于标记"不作为服务注册类型"）：

- `IObjectDAO`、`IObjectViewDAO`、`IObjectDAOAsync`
- `IEntityService`、`IEntityServiceAsync`、`IEntityViewService`、`IEntityViewServiceAsync`

`LiteOrm.Framework` 的扫描逻辑（`RegisterAutoService`）已改为按接口全名排除这些标记接口（`IsExcludedMarkerInterface`），行为与之前保持一致。

### 影响范围

源码层面无影响。仅当用户代码通过反射读取这些接口上的 `[AutoRegister]` 特性时可能受影响（一般不会）。

---

## 常见问题（FAQ）

### Q1: 升级后 `IEntityService<T>` 无法从 DI 解析？

请确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.Framework`），该入口会调用 `AddCoreLiteOrmServices()` 显式注册核心实体服务与 DAO。

### Q2: 我的业务 Service 用了 `[AutoRegister]`，升级后还能用吗？

可以。`[AutoRegister]` 现在定义于 `LiteOrm.Framework`。只要项目引用了 `LiteOrm.Framework` 并添加 `using LiteOrm.Framework;`，用户自定义类型的自动注册行为不变。注意生命周期默认值已改为 `Scoped`，如需单例请显式声明 `[AutoRegister(ServiceLifetime.Singleton)]`。

### Q3: 我的业务 Service 未显式指定 `ServiceTypes`，还能通过接口解析吗？

可以。`GetServiceTypes` 保留了接口推断逻辑：未显式指定 `ServiceTypes` 时，会自动推断实现类型实现的非系统命名空间接口作为服务类型（`LiteOrm.Common` / `LiteOrm.Service` 的非泛型标记接口除外）。依赖接口注入的用户自定义服务无需显式声明 `ServiceTypes`。

### Q4: 我只需要 `LiteOrm` 核心，不想引入 `LiteOrm.Framework`？

可以。核心包现在完全独立于 DI 集成，支持纯手动构造（`new EntityService<T>(...)`、`new ObjectDAO<T>(...)`）。但 `DataSourceProvider` 需手动调用 `AddDataSource` 配置连接。

### Q5: 为什么 `AutoRegisterAttribute` 命名空间改为 `LiteOrm.Framework`？

为了正确反映程序集归属，特性迁移到 `LiteOrm.Framework` 后命名空间同步调整为 `LiteOrm.Framework`。源码中需要增加 `using LiteOrm.Framework;`。

---

## 验证

升级后请确保：

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

完整测试套件（1922 项）全部通过是本版本验证基线。
