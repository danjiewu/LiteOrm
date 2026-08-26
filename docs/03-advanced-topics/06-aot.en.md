# NativeAOT Support

LiteOrm has first-class NativeAOT support. With `PublishAot` enabled, the `LiteOrm.Generators` source generator performs, at compile time, all the work that would otherwise require runtime reflection/dynamic codegen, thereby eliminating trimming and dynamic-code (AOT) warnings and keeping the framework fully usable without a JIT. The `LiteOrm.AotDemo` project in this repo is a complete end-to-end verification example.

> Note: `LiteOrm.DependencyInjection` is built on Autofac / Castle DynamicProxy and relies on runtime reflection and dynamic assembly generation, so it does **not support NativeAOT**. For AOT scenarios, register services with the base library's `AddLiteOrm()` (pure MS DI).

## Project Switches

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <TrimMode>full</TrimMode>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>

<ItemGroup>
  <!-- Reference the source generator as an Analyzer -->
  <ProjectReference Include="..\LiteOrm.Generators\LiteOrm.Generators.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## What the Source Generator Provides at Compile Time

1. **Table metadata**: generates `TableInfo` for each `[Table]` entity and registers it into `CommonTableInfoProvider`. Under AOT, table metadata is always carried by `CommonTableInfoProvider`, so runtime and AOT behave identically.
2. **DataReader mapping delegates**: generates `RegisterMapper<T>` mappings. Each mapping **initializes all of an entity's column converters in one shot** (a static method plus an initialization flag, triggered lazily), then **reads each row by reusing the cached converters** via `GetValue`, avoiding runtime `Expression.Compile()` and repeated lookups.
3. **Property accessors / SQL builder / connection types**: `ObjectBase` property access, `SqlBuilder`-derived and `DbConnection`-derived types are pre-registered into `TypeResolverHelper`.
4. **DI registration**: services annotated with `[AutoRegister]` get MS DI registration code generated at compile time.

`CommonTableInfoProvider` lazily resolves and backfills column-level converters (`SqlTable.EnsureConverters`, initialized only once).

## Rules and Constraints

- **Complex types require an explicit `[Column]`**: properties of complex types — arrays/collections and custom classes (`Object`) — are not auto-recognized as table columns without `[Column]`; mark them explicitly (specifying `DbType = Array` as needed) to persist them. `Json`/`Jsonb` mapped types are now auto-generated as columns.
- **Converters must be declared or pre-registered**: complex/custom conversions must be declared via `Column.ConverterType`, `ForeignColumn.ConverterType`, or pre-registered via `SqlBuilder.RegisterDbValueConverter`. `Column.ConverterType` requires a type implementing `IDbValueConverter`. A column with `ForeignColumn.ConverterType` prefers its own converter when reading and otherwise falls back to the target column's.
- Avoid introducing runtime reflection into entities/conversion chains that the source generator cannot cover.

## Full CRUD and SearchAs Example

`LiteOrm.AotDemo` demonstrates full CRUD (including batch insert/update, `UpdateOrInsert`, `UpdateAll`, and the various deletes) plus `SearchAs`/`SearchOneAs` calls (sync and async):

```csharp
// Entity (AotUser) and view (AotUserView, both have generated DataReader mappers)
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

// ---- Insert ----
userService.Insert(new AotUser { UserName = "alice", Age = 30, CreateTime = DateTime.Now });
await userService.InsertAsync(new AotUser { UserName = "carol", Age = 28, CreateTime = DateTime.Now });
userService.BatchInsert(new[] { dave, eve });

// ---- Query / Count ----
var total = userService.Count(null);
var old = userService.Search(Expr.Prop("Age") > 20);
var alice = userService.SearchOne(Expr.Prop("UserName") == "alice");

// ---- SearchAs / SearchOneAs (Lambda projection to AotUserView) ----
var projected = userService.SearchAs(u => u.Where(x => x.Age > 20));
var one = userService.SearchOneAs(u => u.Where(x => x.UserName == "bob"));
var projectedAsync = await userService.SearchAsAsync(u => u.Where(x => x.Age > 20));
var oneAsync = await userService.SearchOneAsAsync(u => u.Where(x => x.UserName == "carol"));

// ---- Update ----
alice.Age = 31; userService.Update(alice);
userService.BatchUpdate(/* IEnumerable<AotUser> */);
userService.UpdateOrInsert(new AotUser { UserName = "frank", Age = 40, CreateTime = DateTime.Now });
int n = userService.UpdateAll(new UpdateExpr(new TableExpr(typeof(AotUser)),
    Expr.Prop("UserName") == "bob").Set(("Age", Expr.Const(26))));

// ---- Delete ----
userService.Delete(dave);          // by entity
userService.DeleteID(alice.Id);    // by primary key
var nd = await userService.DeleteAllAsync(Expr.Prop("UserName") == "frank");
```

See the full code in [AotDemo/Program.cs](../../LiteOrm.AotDemo/Program.cs). Example publish command:

```bash
dotnet publish LiteOrm.AotDemo/LiteOrm.AotDemo.csproj -c Release -r win-x64 --self-contained
```

## Related Links

- [Entity Mapping](../02-core-usage/01-entity-mapping.en.md)
- [View Models and Services](../02-core-usage/02-view-models-and-services.en.md)
- [Associations](../02-core-usage/08-associations.en.md)