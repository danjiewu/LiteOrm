# LiteOrm 8.1 升级指南

本指南说明升级到 v8.1.0 需要改动的具体内容。

## 版本概览

| 包 | 新版本 |
|---|---|
| `LiteOrm` | 8.1.0 |
| `LiteOrm.Common` | 8.1.0 |
| `LiteOrm.DependencyInjection` | 8.1.0（新增） |

---

## 迁移步骤

### 第 1 步：引用 `LiteOrm.DependencyInjection` 包

`RegisterLiteOrm()` 扩展方法从 `LiteOrm` 核心包移至 `LiteOrm.DependencyInjection` 包，命名空间由 `LiteOrm` 改为 `LiteOrm.DependencyInjection`。

```xml
<PackageReference Include="LiteOrm.DependencyInjection" Version="8.1.0" />
```

`LiteOrm.DependencyInjection` 传递引用 `LiteOrm` 和 `LiteOrm.Common`，无需重复声明。

更新 `using`：

```csharp
// 旧
using LiteOrm;

// 新
using LiteOrm.DependencyInjection;
```

`RegisterLiteOrm()` 方法签名不变，调用方式无需改动。

### 第 2 步：更新 `BulkProvider` 使用方式（如有自定义实现）

`BulkProviderFactory`、`BulkProviderAttribute` 与 `[AutoRegister(Key = ...)]` 标记方式均已移除。自定义 `IBulkProvider` 不再需要任何标记，实现后直接设置到对应的 `SqlBuilder.BulkProvider` 属性即可：

```csharp
// 旧：通过工厂按连接类型查找（已移除）
var provider = services.GetRequiredService<BulkProviderFactory>().GetProvider(dbConnection.GetType());

// 新：直接设置到 SqlBuilder.BulkProvider
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = new MySqlBulkCopyProvider();
```

`SqlBuilder.BulkProvider` 未设置时返回 `null`，`BatchInsert`/`BatchInsertAsync` 自动回退到多值 INSERT 或逐条插入。

---

## 常见问题（FAQ）

### Q1: 升级后 `IEntityService<T>` 无法从 DI 解析？

确认宿主使用了 `RegisterLiteOrm()`（来自 `LiteOrm.DependencyInjection`）。核心类型（`EntityService<T>`、`ObjectDAO<T>` 等）不再通过 `[AutoRegister]` 扫描注册，改为由 `RegisterCoreServices()` 显式注册。

### Q2: 我的业务 Service 未显式指定 `ServiceTypes`，还能通过接口解析吗？

可以。未显式指定 `ServiceTypes` 时，会自动推断实现类型的非系统命名空间接口作为服务类型。依赖接口注入的用户自定义服务无需显式声明 `ServiceTypes`。

### Q3: 原来用 MS DI 的 `IServiceCollection` 注册的服务还能用吗？

可以。`RegisterLiteOrm()` 内部使用 `AutofacServiceProviderFactory` 桥接 MS DI，已有的 `services.AddXxx()` 注册仍然有效。

---

## 验证

升级后请确保：

```bash
dotnet build .\LiteOrm.sln
dotnet test .\LiteOrm.sln
```

完整测试套件（1922 项）全部通过是本版本验证基线。
