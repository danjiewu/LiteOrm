# 国产 / 兼容数据库 SqlBuilder 开发指南

本文档面向需要在业务项目或独立包中接入国产数据库 / 第三方兼容数据库（如达梦、人大金仓、华为 GaussDB、OceanBase、TiDB、GreatDB 等）的**第三方开发者**。

LiteOrm 已经为这些数据库提供了开箱即用的 SqlBuilder 实现，你可以直接使用；如果默认实现不满足你的版本或场景需求，本文档以**达梦（DM）**为完整示例，演示从分析差异到实现子类并通过公开 API 注册的全流程。

## 1. 开箱即用的国产 / 兼容数据库支持

LiteOrm 自带以下方言构建器，可以直接通过数据源名或连接类型注册使用：

| 构建器 | 兼容基类 | 典型驱动 / 连接类型 | 自动匹配关键字（仅兜底） |
|--------|----------|---------------------|-----------------|
| `DamengBuilder` | `OracleBuilder` | `Dm.DmConnection`（Dm 程序集） | `DAMENG`、`DMNET`、`DM.DMCONNECTION` |
| `KingbaseESBuilder` | `PostgreSqlBuilder` | `Kdbndp.KdbndpConnection` | `KINGBASE`、`KDBNDP` |
| `GaussDBBuilder` | `PostgreSqlBuilder` | `Npgsql.NpgsqlConnection`（openGauss 兼容） | `GAUSSDB`、`OPENGAUSS` |
| `OceanBaseBuilder` | `MySqlBuilder` | `MySql.Data.MySqlClient`（MySQL 兼容模式） | `OCEANBASE` |
| `TiDBBuilder` | `MySqlBuilder` | `MySqlConnector.MySqlConnection` | `TIDB` |
| `GreatDBBuilder` | `MySqlBuilder` | `MySql.Data.MySqlClient` | `GREATDB` |

> 这些构建器都遵循「继承最接近的基础方言 + 仅覆盖差异点」的设计原则。在大部分场景下，国产数据库的 SQL 行为已经与对应的基础方言（Oracle / PostgreSQL / MySQL）保持一致，因此构建器内部可能只覆盖少数方法甚至为零。
>
> 即使默认实现与基础方言完全一致，仍然建议你通过 `RegisterSqlBuilder` 显式注册到对应的数据源名，**不要依赖关键字自动识别**——后者只是兜底机制，驱动版本变化可能导致关键字不再命中。

## 2. 选择基类的策略

| 目标数据库 SQL 行为 | 推荐基类 |
|---------------------|----------|
| Oracle 兼容（达梦、人大金仓 V8 Oracle 模式、GaussDB Oracle 兼容模式） | `OracleBuilder` |
| PostgreSQL 兼容（人大金仓默认、openGauss / GaussDB、PolarDB-PG） | `PostgreSqlBuilder` |
| MySQL 兼容（OceanBase MySQL 模式、TiDB、GreatDB、PolarDB-MySQL） | `MySqlBuilder` |
| SQL Server 兼容（极少见） | `SqlServerBuilder` |
| SQLite 兼容 | `SQLiteBuilder` |
| 行为与标准 SQL 一致 | `SqlBuilder`（基类） |

> 如果同一个国产数据库同时提供多种兼容模式（例如人大金仓 V8 同时支持 Oracle 模式与 PostgreSQL 模式），**默认实现按更通用的模式**（PostgreSQL 模式），Oracle 模式建议自行实现一个子类继承 `OracleBuilder` 并通过数据源名称注册。

## 3. 以达梦（DM）为例：从分析到实现

### 3.1 分析目标数据库的差异点

在动手写代码之前，先对照 [差异速查表](#7-其他方言差异速查) 列出达梦与基础方言（Oracle）的差异：

| 差异点 | Oracle 默认 | 达梦期望 |
|--------|-------------|----------|
| 标识符包裹 | `"NAME"`（双引号，转大写） | 一致，沿用 Oracle 行为 |
| 参数前缀 | `:p0` | 一致 |
| 字符串连接 | `\|\|` | 一致 |
| 分页语法 | `OFFSET ... FETCH`（12c+） | 一致，达梦支持 `OFFSET ... FETCH` |
| 自增列片段 | `GENERATED AS IDENTITY` | **`IDENTITY(1, 1)`**（达梦专属内联语法） |
| EXCEPT 关键字 | `MINUS` | 一致，沿用 Oracle 行为 |
| 布尔类型 | `NUMBER(1)` | 一致 |
| 批量更新 | `MERGE INTO` | 一致 |
| 标识值返回 | `RETURNING ... INTO :p` | 一致 |

结论：达梦只需要覆盖 `GetAutoIncrementSql()` 一个方法。

### 3.2 实现构建器

**注意**： `DamengBuilder`  已包含在 LiteOrm 内置 SqlBuilder 中，示例仅演示如何开发自定义 SqlBuilder 增加数据库支持，下面示例中的 `namespace`、`using` 都属于**你自己的项目或独立包**，不是 LiteOrm 源码。`DamengBuilder` 类名也可以替换为你自己的命名（如 `CompanyDamengBuilder`）。

新建文件 `YourProject\SqlBuilder\DamengBuilder.cs`：

```csharp
using LiteOrm;          // 引用 LiteOrm 基类
using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;


namespace YourProject.SqlBuilder
{
    /// <summary>
    /// 达梦（DM）SQL 构建器。
    /// </summary>
    /// <remarks>
    /// 达梦数据库（DM7/DM8）SQL 语法与 Oracle 高度兼容，使用 Dm 驱动接入，
    /// 故本构建器继承自 <see cref="OracleBuilder"/>。主要差异点：
    /// <list type="bullet">
    /// <item>标识列采用 <c>IDENTITY(1, 1)</c> 内联语法，而非 Oracle 的 GENERATED AS IDENTITY。</item>
    /// <item>默认大小写策略与 Oracle 一致（双引号包裹、内部转大写）。</item>
    /// <item>EXCEPT 仍需翻译为 MINUS。</item>
    /// <item>布尔类型映射为 NUMBER(1)。</item>
    /// </list>
    /// </remarks>
    public class DamengBuilder : OracleBuilder
    {
        /// <summary>
        /// 获取 <see cref="DamengBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new DamengBuilder Instance = new DamengBuilder();

        /// <summary>
        /// 达梦使用 IDENTITY(1, 1) 内联语法生成标识列，与 Oracle 的 GENERATED AS IDENTITY 不同。
        /// </summary>
        protected override string GetAutoIncrementSql() => "IDENTITY(1, 1)";
    }
}
```

**实现要点：**

1. **继承 `OracleBuilder` 而不是 `SqlBuilder`**：这样我们可以复用 Oracle 的所有 SQL 生成逻辑（`MERGE INTO` 批量更新、`MINUS` 翻译、`RETURNING INTO` 标识值返回等），只覆盖差异点。
2. **提供 `public static readonly new XxxBuilder Instance`**：所有内置构建器都遵循单例模式，工厂返回的就是这个单例。
3. **只覆盖 `GetAutoIncrementSql()`**：这是与 Oracle 的唯一显著差异。
4. **使用 `protected override` 而不是 `public override`**：因为基类的 `GetAutoIncrementSql()` 就是 `protected virtual`，保持访问级别一致。

### 3.3 注册自定义 SqlBuilder

实现完子类后，必须通过 `RegisterSqlBuilder(...)` 公开 API 注册到工厂，工厂才能在生成 SQL 时找到你的构建器，而 内置 SqlBuilder 是通过关键字匹配。

`RegisterSqlBuilder` 有两个重载，分别写入工厂内部的两个字典：

| 重载 | 写入的字典 | 适用场景 |
|------|-----------|---------|
| `RegisterSqlBuilder(string dataSourceName, SqlBuilder sqlBuilder)` | `RegisteredSqlBuildersByDataSource` | 按数据源名注册（推荐，多数据源场景） |
| `RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)` | `RegisteredSqlBuilders` | 按连接类型注册（全局替换该驱动的默认方言） |

两个字典的查询都**优先于关键字识别**，所以写入即生效，不会受内置关键字匹配影响。

#### 直接在启动配置阶段注册

```csharp
using Dm;  // 你的目标数据库驱动

builder.Host.RegisterLiteOrm(options =>
{
    // 方式 A：按数据源名注册（优先级最高，推荐用于多数据源场景）
    options.RegisterSqlBuilder("Dameng", DamengBuilder.Instance);

    // 方式 B：按连接类型注册（全局替换该驱动的默认方言）
    options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
});
```

#### 多数据源场景推荐统一用数据源名注册

避免不同驱动共用了某个关键字导致误匹配：

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("Dameng",   DamengBuilder.Instance);
    options.RegisterSqlBuilder("Kingbase", KingbaseESBuilder.Instance);
    options.RegisterSqlBuilder("Gauss",    GaussDBBuilder.Instance);
});
```

#### 直接调用工厂单例（无 DI 场景）

```csharp
SqlBuilderFactory.Instance.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
SqlBuilderFactory.Instance.RegisterSqlBuilder("Dameng", DamengBuilder.Instance);
```

#### 通过配置文件注册

在 `appsettings.json` 中通过 `SqlBuilder` 字段直接指定：

```json
{
  "LiteOrm": {
    "Default": "DamengDataSource",
    "DataSources": [
      {
        "Name": "DamengDataSource",
        "ConnectionString": "Server=dm-host:5236;User Id=SYSDBA;Password=SYSDBA001;",
        "Provider": "Dm.DmConnection, Dm",
        "SqlBuilder": "YourProject.SqlBuilder.DamengBuilder, YourProject"
      }
    ]
  }
}
```

- `SqlBuilder` 字段的值格式为 `类型全名称, 程序集名称`。
- 内置的 `DamengBuilder` 位于 `LiteOrm` 命名空间下，可以直接填 `LiteOrm.DamengBuilder, LiteOrm`；自定义子类填你自己的命名空间和程序集。
- 自定义子类需要确保程序集已被加载（通常在启动时引用即可）。

#### 工厂识别顺序

以上四种注册方式最终都写入工厂内部的两个字典，`SqlBuilderFactory.GetSqlBuilder` 的查找优先级从高到低为：

1. **按数据源名称**：`RegisteredSqlBuildersByDataSource[dataSourceName]` — 由 `RegisterSqlBuilder(string, SqlBuilder)` 写入
2. **按连接类型**：`RegisteredSqlBuilders[providerType]` — 由 `RegisterSqlBuilder(Type, SqlBuilder)` 写入
3. **按连接类型名关键字**：内置方言的兜底识别（仅适用于 LiteOrm 自带的驱动关键字）
4. **默认**：`SqlBuilder.Instance`（标准 SQL）

**第三方开发者重点关注第 1、2 层**：

- 这两层是工厂**仅有的两个公开注册入口**，写入后总是优先于关键字识别命中。
- 即便你的驱动 FullName 不包含任何被识别的关键字（例如某个私有达梦驱动叫 `Acme.Db.Connection`），只要在启动时调用了 `RegisterSqlBuilder(typeof(AcmeConnection), DamengBuilder.Instance)`，工厂就会正确返回你的构建器。
- 第 3 层关键字识别只是 LiteOrm 内置方言的兜底机制，第三方不应依赖它，更不能修改它。

> **结论**：永远通过 `RegisterSqlBuilder(...)` 显式注册，**不要依赖关键字自动识别**——后者在驱动版本变更时可能失效。

### 3.4 为子类写单元测试

如果你覆盖了某个方法并改变了输出语义（例如达梦的 `GetAutoIncrementSql()` 返回 `IDENTITY(1, 1)`），建议在你自己的测试项目中追加断言。`GetAutoIncrementSql` 是 `protected`，需通过 `BuildCreateTableSql` 间接验证：

```csharp
[Fact]
public void Dameng_GetAutoIncrementSql_ReturnsIdentitySyntax()
{
    // 通过 AttributeTableInfoProvider 构造测试上下文
    var tableDefinition = CreateProvider(DamengBuilder.Instance).GetTableDefinition(typeof(DamengIdentityModel));
    var sql = DamengBuilder.Instance.BuildCreateTableSql(tableDefinition.Name, tableDefinition.Columns);
    Assert.Contains("IDENTITY(1, 1)", sql);
    Assert.DoesNotContain("GENERATED AS IDENTITY", sql);
}

[Table("DamengIdentityModels")]
private class DamengIdentityModel
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true, AllowNull = false)]
    public int Id { get; set; }
}
```

`CreateProvider` 的辅助方法可以参考 `LiteOrm.Tests\SqlBuilderTests.cs` 中已有的实现（通过 Moq 构造 `ISqlBuilderFactory` 和 `IDataSourceProvider`，再实例化 `AttributeTableInfoProvider`）。

如果你的方言有独特函数翻译（例如达梦的 `SYSDATE`、`TO_DATE` 行为差异），按 [表达式扩展](./01-expression-extension.md) 文档注册函数处理器并写专门测试。

## 4. 函数处理器（SqlFunction）的继承查找机制

除了覆盖 `virtual` 方法外，LiteOrm 还提供了一种更轻量的扩展点：**函数处理器（FunctionSqlHandler）**。它允许你为某个具体函数名（如 `DATE_FORMAT`、`SYSDATE`）注册翻译逻辑，而无需新建子类或覆盖方法。详见 [表达式扩展](./01-expression-extension.md)。

本节重点说明**继承链查找机制**——这是自定义子类与父类函数处理器协同工作的关键。

### 4.1 查找顺序

`SqlBuilder.BuildFunctionSql` 在生成函数 SQL 时，会从**当前实例的最派生类型**开始，沿继承链逐层向上查找注册的处理器，**第一个命中的就用**：

```
当前实例类型（如 DamengBuilder）  →  查找 DamengBuilder 注册的处理器
    ↓ 未命中
父类型（如 OracleBuilder）       →  查找 OracleBuilder 注册的处理器
    ↓ 未命中
祖父类型（如 SqlBuilder）        →  查找 SqlBuilder 注册的处理器
    ↓ 全部未命中
默认函数映射表（如 IndexOf → CHARINDEX）
```

**核心结论**：

- **子类自己注册的处理器优先级最高**。
- **父类注册的处理器也会生效**，优先级在子类注册的之后——子类没注册时自动 fallback 到父类。
- 全部未命中才走默认函数映射表（如 `IndexOf→CHARINDEX`、`Now→CURRENT_TIMESTAMP`）。

### 4.2 实践意义

这意味着你可以**继承现有的内置构建器，只追加自己的函数处理器，而无需重新注册父类已有的处理器**：

```csharp
using LiteOrm;
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

public class MyDamengBuilder : DamengBuilder
{
    public static readonly new MyDamengBuilder Instance = new MyDamengBuilder();

    public MyDamengBuilder()
    {
        // 只注册达梦专属的函数翻译
        // 例如把 GETDATE 翻译为 SYSDATE
        this.RegisterFunctionSqlHandler("GETDATE", (ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams) =>
        {
            outSql.Append("SYSDATE");
        });
    }
}
```

上面这段代码执行后：

- `GETDATE` → 命中 `MyDamengBuilder` 注册的处理器，输出 `SYSDATE`。
- `Now` → `MyDamengBuilder` 没注册，沿继承链查找，命中 `SqlBuilder` 基类注册的处理器（输出 `CURRENT_TIMESTAMP`）。
- `IndexOf` → 全部未命中，走默认映射表（输出 `CHARINDEX`）。

### 4.3 覆盖父类处理器的两种方式

如果父类注册的某个处理器不符合你的需求，有两种覆盖方式：

| 方式 | 说明 | 影响范围 |
|------|------|---------|
| 在子类构造函数中用**相同函数名**注册新处理器 | 子类命中后直接返回，不再查父类 | 仅子类及其后代 |
| 覆盖 `BuildFunctionSql` 方法 | 完全自定义查找逻辑 | 子类及其后代 |

> 由于查找是沿继承链从子到父逐层进行的，子类注册的处理器**永远不会被父类同名的处理器覆盖**。这点与 `virtual` 方法覆盖语义一致。

### 4.4 查找链何时终止

查找会一直向上直到 `SqlBuilder` 基类（含）。如果某个处理器只希望对特定派生类生效，可以在处理器内部通过 `sqlBuilder.GetType()` 判断后决定是否处理，未命中则不调用并继续查找。不过更推荐的做法是直接在对应派生类的构造函数中注册，让查找链天然命中。

## 5. 验证与排错

### 5.1 推荐验证清单

接入达梦（或任何新国产数据库）后，建议按以下顺序验证：

1. **基础增删改查**：确认标识列插入能正确返回自增值（达梦通过 `RETURNING INTO`）。
2. **排序分页**：`Skip(...).Take(...)` 生成的 SQL 在目标数据库版本下可执行。
3. **批量插入 / 批量更新**：达梦走 `MERGE INTO`，需要验证主键列、可更新列的参数顺序。
4. **DDL 验证**：`BuildCreateTableSql` 生成的 `IDENTITY(1, 1)` 语法可被达梦执行器接受。
5. **函数翻译**：如有自定义 Lambda 翻译（如 `DateTime.Now` 翻译为 `SYSDATE`），需要单独验证。

### 5.2 先看生成 SQL，再看 ORM 代码

排查兼容性问题时按以下顺序：

1. 确认目标数据库版本（DM7 / DM8 行为略有差异）。
2. 通过日志查看实际生成的 SQL，确认方言是否命中。详见 [日志与诊断](../03-advanced-topics/07-logging.md)。
3. 如果发现生成的 SQL 是 Oracle 方言（如 `GENERATED AS IDENTITY`）而不是达梦方言（`IDENTITY(1, 1)`），说明工厂没有命中你注册的构建器。先检查启动配置是否调用了 `RegisterSqlBuilder(...)`，再核对数据源名 / 连接类型是否与运行时一致。
4. 必要时显式注册，绕过关键字识别：

   ```csharp
   options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
   ```

### 5.3 大小写策略提示

达梦默认的标识符大小写策略与 Oracle 一致：

- 双引号包裹的标识符按**大写**处理（`"USER"` → 内部存储为 `USER`）。
- 不带双引号的标识符会被达梦自动转大写。

如果你的表 / 列名在达梦中实际是小写存储（例如迁移自 MySQL 的库），需要：

- 在实体 `[Table]` / `[Column]` 上使用全小写或带引号的名字；
- 或者实现一个继承 `DamengBuilder` 的子类，覆盖 `ToSqlName` 改为「双引号 + 转小写」策略，并通过数据源名注册到对应数据源。

## 6. 其他方言差异速查

下表汇总了 6 个内置国产 / 兼容数据库与基础方言的差异点，可作为接入新数据库时的对照参考：

| 数据库 | 基类 | 主要差异点 | 是否覆盖方法 |
|--------|------|-----------|--------------|
| 达梦 DM | `OracleBuilder` | 自增列用 `IDENTITY(1, 1)` | 覆盖 `GetAutoIncrementSql` |
| 人大金仓 KingbaseES | `PostgreSqlBuilder` | 无（默认与 PG 完全一致） | 无 |
| 华为 GaussDB / openGauss | `PostgreSqlBuilder` | 无（默认与 PG 完全一致） | 无 |
| OceanBase（MySQL 模式） | `MySqlBuilder` | 无（默认与 MySQL 完全一致） | 无 |
| TiDB | `MySqlBuilder` | 自增列分布式语义不同（不保证连续） | 无（行为差异由驱动层吸收） |
| GreatDB | `MySqlBuilder` | 无（默认与 MySQL 完全一致） | 无 |

**额外注意点：**

- **人大金仓 V8 Oracle 模式**：默认的 `KingbaseESBuilder` 走 PostgreSQL 模式；如果使用 Oracle 兼容模式，建议自行实现一个 `KingbaseESOracleBuilder : OracleBuilder` 并按数据源名注册。
- **OceanBase Oracle 模式**：默认的 `OceanBaseBuilder` 走 MySQL 模式；如果使用 Oracle 兼容模式，建议自行实现一个 `OceanBaseOracleBuilder : OracleBuilder` 并按数据源名注册。
- **TiDB `AUTO_RANDOM`**：如果使用了 `AUTO_RANDOM` 主键，自增值不是连续整数，`BuildBatchIdentityInsertSql` 返回的 `LAST_INSERT_ID()` 仅供参考，业务上不要依赖连续 ID。可通过子类覆盖 `BuildBatchIdentityInsertSql` 来禁用该返回或调整为 `ROW_COUNT()` 之类。
- **GaussDB 分布式**：如果使用 GaussDB 的分布式版本，可能涉及分布键 / 分布表的 DDL 差异，需要在 `BuildCreateTableSql` 中追加 `DISTRIBUTE BY ...` 子句，建议通过子类实现。

## 7. 常见问题

### Q1：为什么我配置了达梦连接，但生成的 SQL 还是 Oracle 方言（如 `GENERATED AS IDENTITY`）？

**A：** 说明工厂没有命中你注册的构建器。可能原因：

1. 启动时没有调用 `RegisterSqlBuilder(...)`，或调用时机晚于首次查询。
2. 注册的数据源名 / 连接类型与运行时实际使用的不一致。
3. 你依赖的是关键字自动识别，但你的驱动 FullName 不包含内置关键字（`DAMENG`、`DMNET`、`DM.DMCONNECTION`）中的任意一个。

解决方案：**不要依赖关键字识别**，直接显式注册：

```csharp
options.RegisterSqlBuilder(typeof(DmConnection), DamengBuilder.Instance);
// 或
options.RegisterSqlBuilder("YourDataSourceName", DamengBuilder.Instance);
```

### Q2：达梦和 Oracle 在分页上完全一样吗？

**A：** DM8 完全支持 `OFFSET ... FETCH` 语法，与 Oracle 12c+ 一致。如果是 DM7 或更老版本，可能需要降级为 `ROW_NUMBER() OVER(...)` 双层嵌套写法，参考 [Oracle 11g 自定义分页示例](../03-advanced-topics/05-custom-paging.md) 实现一个 `Dameng7Builder : DamengBuilder` 并按数据源名注册。

### Q3：批量插入返回的 ID 在 TiDB 上不连续，怎么处理？

**A：** TiDB 的自增 ID 在分布式场景下仅保证唯一、不保证连续。`BuildBatchIdentityInsertSql` 返回的 `LAST_INSERT_ID()` 是首行的 ID，后续行 ID 不一定连续递增。业务上应避免依赖 `BatchInsert` 后通过 ID 推算其他行；如需获取全部插入 ID，建议改为循环单条插入并收集返回值，或使用业务自然键。

### Q4：国产数据库的布尔类型怎么处理？

**A：** 默认 `OracleBuilder` / `DamengBuilder` 会把 `bool` 映射为 `NUMBER(1)`（0/1）。`PostgreSqlBuilder` 系（KingbaseES、GaussDB）原生支持 `BOOLEAN` 类型。`MySqlBuilder` 系（OceanBase、TiDB、GreatDB）原生支持 `TINYINT(1)`。如果你需要其他映射（例如达梦的 `BIT`），覆盖 `GetDbTypeInternal` 和 `GetSqlTypeDefinition` 即可。

### Q5：为什么内置的 KingbaseESBuilder / GaussDBBuilder / OceanBaseBuilder / TiDBBuilder / GreatDBBuilder 内部是空的？

**A：** 这是**有意的设计**。这些数据库在常用 SQL 语法上与对应基础方言完全一致，但保留独立类型有两个目的：

1. 让工厂的关键字识别分流到正确的方言（而不是命中通用的 `SqlBuilder.Instance`）；
2. 为你后续按版本追加专属扩展点提供挂载基类（例如要支持 TiDB 的 `AUTO_RANDOM`，直接继承 `TiDBBuilder` 覆盖方法即可，不会影响普通 MySQL 用户）。

如果你发现某个国产数据库与基础方言存在未覆盖的差异，建议自行实现一个子类并通过数据源名注册：

```csharp
public class MyKingbaseESBuilder : KingbaseESBuilder
{
    public static readonly new MyKingbaseESBuilder Instance = new MyKingbaseESBuilder();

    // 覆盖差异方法
}

// 注册
options.RegisterSqlBuilder("Kingbase", MyKingbaseESBuilder.Instance);
```

### Q6：我可以修改 `SqlBuilderFactory.cs` 源码添加自己的关键字吗？

**A：** 不建议。第三方修改 LiteOrm 源码会带来维护负担（升级时需要重新合并冲突），且并非必要——通过 `RegisterSqlBuilder(...)` 注册到字典的构建器**总是优先于**关键字识别命中。如果你需要支持某个不被识别的驱动，直接在启动时显式注册即可：

```csharp
options.RegisterSqlBuilder(typeof(YourCustomConnection), YourBuilder.Instance);
```

## 8. 扩展流程总结

接入一个新的国产 / 兼容数据库时，按以下步骤进行：

1. **确定兼容基类**：根据目标数据库的 SQL 行为，从 `OracleBuilder` / `PostgreSqlBuilder` / `MySqlBuilder` / `SqlServerBuilder` / `SQLiteBuilder` / `SqlBuilder` 中选择最接近的基类。
2. **列出差异点**：参照 [差异速查表](#7-其他方言差异速查) 与基础方言对照，列出需要覆盖的方法。
3. **实现子类**：在你自己的项目或独立包中新建 `XxxBuilder.cs`，仅覆盖差异方法，提供 `public static readonly new XxxBuilder Instance` 单例。
4. **注册 SqlBuilder**：通过 `RegisterSqlBuilder(...)` 公开 API 写入 `RegisteredSqlBuilders` / `RegisteredSqlBuildersByDataSource` 字典。**无需修改 LiteOrm 源码**。详见 [3.3 注册自定义 SqlBuilder](#33-注册自定义-sqlbuilder)。
5. **追加测试**：在你自己的测试项目中写断言，验证差异方法的输出符合预期；公共行为可参考 LiteOrm.Tests 中已有的参数化测试模式。
6. **配置数据源**：在 `appsettings.json` 或 `RegisterLiteOrm` 选项中注册数据源并指定 `SqlBuilder`。
7. **验证清单**：按 [5.1 推荐验证清单](#51-推荐验证清单) 跑一遍业务场景。

## 相关链接

- [返回目录](../README.md)
- [自定义 SqlBuilder / 方言扩展](./03-custom-sqlbuilder.md)
- [自定义分页实现示例](../03-advanced-topics/05-custom-paging.md)
- [数据库差异与兼容性说明](../05-reference/08-database-compatibility.md)
- [表达式扩展](./01-expression-extension.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)
- [日志与诊断](../03-advanced-topics/07-logging.md)
