# NativeAOT 支持

LiteOrm 具备完善的 NativeAOT 兼容能力。开启 `PublishAot` 后，编译期由 `LiteOrm.Generators` 源生成器完成所有需要运行期反射/动态编程的工作，从而避免裁剪（trimming）与动态代码（AOT）相关告警，保证在无 JIT 环境下端到端可用。本工程内的 `LiteOrm.AotDemo` 即为完整的端到端验证示例。

> 注意：`LiteOrm.DependencyInjection` 基于 Autofac / Castle DynamicProxy，依赖运行时反射与动态程序集生成，**不支持 NativeAOT**。AOT 场景请使用基础库 `AddLiteOrm()` 进行纯 MS DI 注册。

## 工程开关

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <TrimMode>full</TrimMode>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

<ItemGroup>
  <!-- 将源生成器作为 Analyzer 引用 -->
  <ProjectReference Include="..\LiteOrm.Generators\LiteOrm.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## 源生成器在编译期提供的能力

1. **表元信息**：为每个 `[Table]` 实体生成 `TableInfo` 并注册到 `CommonTableInfoProvider`。AOT 下表元信息一律由 `CommonTableInfoProvider` 承载，运行时与 AOT 行为一致。
2. **DataReader 映射委托**：为每个实体生成 `RegisterMapper<T>` 映射。映射为**每个实体一次性初始化所有列转换器**（静态方法 + 初始化标记位，惰性触发），随后**逐行直接复用缓存的转换器**并按 `GetValue` 方式读取，避免运行期 `Expression.Compile()` 与重复解析。
3. **属性访问器 / SQL 构建器 / 连接类型**：`ObjectBase` 属性访问、`SqlBuilder` 派生与 `DbConnection` 派生类型均预注册到 `TypeResolverHelper`。
4. **DI 注册**：带 `[AutoRegister]` 的服务在编译期生成 MS DI 注册代码。

其中 `CommonTableInfoProvider` 对列级转换器（`DbValueConverter`）进行惰性解析并回填（`SqlTable.EnsureConverters`，仅初始化一次）。

## 规则与约束

- **复杂类型需显式 `[Column]`**：数组/集合、自定义类（`Object`）等复杂类型属性，未标注 `[Column]` 时不会被自动识别为表列；需显式 `[Column]`（并按需指定 `DbType = Array`）才会持久化。`Json`/`Jsonb` 映射类型已支持自动生成列。
- **转换器需显式声明或预注册**：复杂/自定义转换须通过列级 `Column.ConverterType`、`ForeignColumn.ConverterType` 声明，或经 `SqlBuilder.RegisterDbValueConverter` 预注册；`Column.ConverterType` 需提供实现 `IDbValueConverter` 的转换器类型。声明了 `ForeignColumn.ConverterType` 的列在读取时优先使用自身转换器，否则回退目标列。
- 避免在实体/转换链路中引入无法被源生成器覆盖的运行期反射。

## 完整 CRUD 与 SearchAs 示例

`LiteOrm.AotDemo` 演示了完整 CRUD（含批量插入/更新、`UpdateOrInsert`、`UpdateAll`、各种删除）与 `SearchAs`/`SearchOneAs`（同步 + 异步）的调用方式：

```csharp
// 实体（AotUser）与视图（AotUserView，含生成的 DataReader 映射）
[Table("AotUsers")]
public class AotUser : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    [Column("UserName")]
    public string? UserName { get; set; }
    [Column("Age")]
    public int Age { get; set; }
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
    [Column("Guid")]
    public Guid Guid { get; set; }
}
public class AotUserView : AotUser { }

// Service
public interface IAotUserService : IEntityService<AotUser>, IEntityServiceAsync<AotUser>,
    IEntityViewService<AotUserView>, IEntityViewServiceAsync<AotUserView> { }

[AutoRegister]
public class AotUserService(IServiceProvider sp) : EntityService<AotUser, AotUserView>(sp), IAotUserService { }
```

```csharp
var userService = scope.ServiceProvider.GetRequiredService<IAotUserService>();

// ---- 插入 ----
userService.Insert(new AotUser { UserName = "alice", Age = 30, CreateTime = DateTime.Now });
await userService.InsertAsync(new AotUser { UserName = "carol", Age = 28, CreateTime = DateTime.Now });
userService.BatchInsert(new[] { dave, eve });

// ---- 查询 / 计数 ----
var total = userService.Count(null);
var old = userService.Search(Expr.Prop("Age") > 20);
var alice = userService.SearchOne(Expr.Prop("UserName") == "alice");

// ---- SearchAs / SearchOneAs（Lambda 投影到 AotUserView）----
var projected = userService.SearchAs(u => u.Where(x => x.Age > 20));
var one = userService.SearchOneAs(u => u.Where(x => x.UserName == "bob"));
var projectedAsync = await userService.SearchAsAsync(u => u.Where(x => x.Age > 20));
var oneAsync = await userService.SearchOneAsAsync(u => u.Where(x => x.UserName == "carol"));

// ---- 更新 ----
alice.Age = 31; userService.Update(alice);
userService.BatchUpdate(/* IEnumerable<AotUser> */);
userService.UpdateOrInsert(new AotUser { UserName = "frank", Age = 40, CreateTime = DateTime.Now });
int n = userService.UpdateAll(new UpdateExpr(new TableExpr(typeof(AotUser)),
    Expr.Prop("UserName") == "bob").Set(("Age", Expr.Const(26))));

// ---- 删除 ----
userService.Delete(dave);          // 按实体
userService.DeleteID(alice.Id);    // 按主键
var nd = await userService.DeleteAllAsync(Expr.Prop("UserName") == "frank");
```

完整代码见 [AotDemo/Program.cs](../../LiteOrm.AotDemo/Program.cs)。完整发布命令示例：

```bash
dotnet publish LiteOrm.AotDemo/LiteOrm.AotDemo.csproj -c Release -r win-x64 --self-contained
```

## 相关链接

- [实体映射](../02-core-usage/01-entity-mapping.md)
- [视图模型与服务](../02-core-usage/02-view-models-and-services.md)
- [关联查询](../02-core-usage/08-associations.md)