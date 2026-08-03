# LiteOrm 8.1 升级指南

本指南说明升级到 v8.1.0 需要改动的具体内容。

## 版本概览

| 包 | 新版本 |
|---|---|
| `LiteOrm` | 8.1.0 |
| `LiteOrm.Common` | 8.1.0 |
| `LiteOrm.Framework` | 8.1.0（新增） |

---

## 迁移步骤

### 第 1 步：引用 `LiteOrm.Framework` 包

`RegisterLiteOrm()` 扩展方法从 `LiteOrm` 核心包移至 `LiteOrm.Framework` 包，命名空间由 `LiteOrm` 改为 `LiteOrm.Framework`。

```xml
<PackageReference Include="LiteOrm.Framework" Version="8.1.0" />
```

`LiteOrm.Framework` 传递引用 `LiteOrm` 和 `LiteOrm.Common`，无需重复声明。

更新 `using`：

```csharp
// 旧
using LiteOrm;

// 新
using LiteOrm.Framework;
```

`RegisterLiteOrm()` 方法签名不变，调用方式无需改动。

### 第 2 步：更新 `[AutoRegister]` 命名空间

`AutoRegisterAttribute` 从 `LiteOrm.Common` 迁移到 `LiteOrm.Framework`，命名空间随之改变：

```csharp
// 旧
using LiteOrm.Common;  // AutoRegisterAttribute、Lifetime 枚举

// 新
using LiteOrm.Framework;  // AutoRegisterAttribute
using Microsoft.Extensions.DependencyInjection;  // ServiceLifetime
```

### 第 3 步：替换 `Lifetime` 枚举

`LiteOrm.Common` 中的 `Lifetime` 枚举已移除，改用 .NET 内置 `ServiceLifetime`：

```csharp
// 旧
[AutoRegister(Lifetime.Singleton)]
public class MyService : IMyService { }

// 新
[AutoRegister(ServiceLifetime.Singleton)]
public class MyService : IMyService { }
```

默认值保持 `Singleton` 不变。

### 第 4 步：更新 `BulkProvider` 特性（如有自定义实现）

自定义 `IBulkProvider` 实现原先通过 `[AutoRegister(Key = typeof(XxxConnection))]` 标记，现在改用 `[BulkProvider(typeof(XxxConnection))]`：

```csharp
// 旧
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }

// 新
[BulkProvider(typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider { }
```

`BulkProviderAttribute` 定义于 `LiteOrm.Common`（`LiteOrm.Common.Attributes` 命名空间）。

---

## 常见问题（FAQ）

### Q1: 升级后 `IEntityService<T>` 无法从 DI 解析？

确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.Framework`）。核心类型（`EntityService<T>`、`ObjectDAO<T>` 等）不再通过 `[AutoRegister]` 扫描注册，改为由 `RegisterCoreServices()` 显式注册。

### Q2: 我的业务 Service 用了 `[AutoRegister]`，升级后还能用吗？

可以。只要项目引用了 `LiteOrm.Framework` 并添加 `using LiteOrm.Framework;`，自动注册行为不变。

### Q3: 我的业务 Service 未显式指定 `ServiceTypes`，还能通过接口解析吗？

可以。未显式指定 `ServiceTypes` 时，会自动推断实现类型的非系统命名空间接口作为服务类型。依赖接口注入的用户自定义服务无需显式声明 `ServiceTypes`。

### Q4: 原来用 MS DI 的 `IServiceCollection` 注册的服务还能用吗？

可以。`RegisterLiteOrm()` 内部使用 `AutofacServiceProviderFactory` 桥接 MS DI，已有的 `services.AddXxx()` 注册仍然有效。

---

## 验证

升级后请确保：

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

完整测试套件（1922 项）全部通过是本版本验证基线。
