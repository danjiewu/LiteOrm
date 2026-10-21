# NativeAOT Support

Both `LiteOrm` and `LiteOrm.Common` can be published under NativeAOT. With `PublishAot` enabled, the `LiteOrm.Generators` source generator takes over, at compile time, the work that would otherwise need runtime reflection and `Expression.Compile()`. The runtime no longer depends on dynamic code, and trimming warnings drop to zero. The `LiteOrm.AotDemo` project in this repo is a complete end-to-end verification example, covering five scenario groups: single-table CRUD, entity associations, projections and aggregation, JSON and computed columns, transactions and fixed filtering.

> Note: `LiteOrm.DependencyInjection` is built on Autofac / Castle DynamicProxy and relies on runtime reflection and dynamic assembly generation, so it does **not support NativeAOT**. For AOT scenarios, register services with the base library's `AddLiteOrm()` (pure MS DI).

This article covers two things: where NativeAOT and JIT differ in usage, and what to watch out for when migrating.

## Differences from JIT

Business code reads the same in both modes. Every difference lives inside the framework. Same job, two approaches:

| Dimension | JIT (regular publish) | NativeAOT |
|------|--------------|-----------|
| Table metadata | `AttributeTableInfoProvider` reads attributes at runtime and builds the column model by reflection | `TableInfo` (including association metadata) is generated at compile time and registered into `CommonTableInfoProvider` |
| DataReader mapping | mapping delegates compiled at runtime via `Expression.Compile()` | static delegates generated at compile time, pre-registered through `RegisterMapper<T>` |
| Property access | accessors compiled from expression trees | accessor delegates generated at compile time |
| `SqlBuilder` / `DbConnection` derived types | resolved by type name at runtime | derived types enumerated at compile time and pre-registered into `TypeResolverHelper` |
| `[AutoRegister]` services | registered by scanning assemblies at runtime | MS DI registration code generated at compile time, hooked up by a module initializer |
| DI wiring | `AddLiteOrm()` or `RegisterLiteOrm()` | `AddLiteOrm()` only, and no AOP |
| Remote calls | `LiteOrm.Remote` works | `LiteOrm.Remote` / `LiteOrm.Remote.Server` unavailable |
| First query | compiles the mapping delegate on the spot | uses the delegate that was already generated |

```mermaid
flowchart TB
    APP[Business code: DAO / Service / Expr]
    APP --> MODE{Build mode}
    MODE -->|default| JIT[JIT: work happens at runtime]
    MODE -->|PublishAot=true| AOT[NativeAOT: work happens at compile time]
    JIT --> J1[AttributeTableInfoProvider reads attributes]
    JIT --> J2[Expression.Compile builds mapping delegates]
    JIT --> J3[Resolve SqlBuilder and connections by type name]
    AOT --> A1[CommonTableInfoProvider registers TableInfo]
    AOT --> A2[RegisterMapper pre-registers mappings]
    AOT --> A3[TypeResolverHelper pre-registers types]
```

Four points deserve their own paragraph:

- **The metadata provider changes, but it carries just as much.** Under JIT, `AttributeTableInfoProvider` reads attributes the first time an entity is touched and turns `[TableJoin]` / `[ForeignType]` / `[ForeignColumn]` into `TableView.JoinedTables`. Under AOT that whole job moves to compile time: the source generator writes those attributes into `TableInfo`, and `CommonTableInfoProvider` assembles the same `TableDefinition` and `TableView` at runtime, joined tables, `ForeignColumn` projections and the `Used` marks along the foreign-key chain included. `[Column(Constant = ...)]` lands in `ColumnInfo.Constant` as well, and the same `TableDefinition.ConstFilter` is built at runtime.
- **There are three read-mapping paths, and speed depends on whether code was generated.** Entities marked with `[Table]` get a static delegate from the source generator in an AOT build. Entities without `[Table]`, projection types and anonymous types have no generated code and fall back to the `AotMapper` reflection path under AOT. Under JIT, every type goes through expression-tree compilation. Boxing only happens where it is actually needed: a column that hits a column-level converter goes through `GetValue()` plus a non-generic delegate, so that column boxes; a column without a converter reads through a hard-coded typed method in the generated code and does not box; the reflection path uses `GetValue()` plus `SetValue` uniformly and boxes every column.
- **AOP is gone.** `[Transaction]` / `[ServicePermission]` / `[ServiceLog]` rely on Castle dynamic proxies and are unavailable under NativeAOT. Transactions can move to the manual `DAOContext` transaction API, permission filtering to a runtime `Expr`, and logging to service events.

## Project Switches

```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>true</InvariantGlobalization>
  <TrimMode>full</TrimMode>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

- The source generator only looks at build properties: `IsAotCompatible`, `PublishAot`, `PublishTrimmed` or `IsTrimmable` set to `true` each trigger generation on their own; you do not have to wait for `dotnet publish`. Non-AOT builds generate nothing and fall back to the runtime reflection path.
- The generator ships inside `LiteOrm.Common` (`analyzers/dotnet/cs`), so a NuGet reference to `LiteOrm` or `LiteOrm.Common` brings it in automatically. No hand-written `ProjectReference` is needed. The explicit reference in `LiteOrm.AotDemo` exists so the generator project can be compiled straight from the source tree.
- `EmitCompilerGeneratedFiles` is a debugging aid that writes generated code to disk for inspection; drop it for release builds.

## What the Source Generator Provides at Compile Time

1. **Table metadata**: generates `TableInfo` for each `[Table]` entity and registers it into `CommonTableInfoProvider`. Beyond column definitions, class-level `[TableJoin]` and member-level `[ForeignType]` / `[ForeignColumn]` land in the metadata too, so associated views behave exactly as they do under JIT. Multiple assemblies share one global singleton, so they never overwrite each other.
2. **DataReader mapping delegates**: generates `RegisterMapper<T>` mappings. Each mapping initializes all of an entity's column converters in one shot (a static method plus an initialization flag, triggered lazily), then reuses the cached converters row by row; columns without a converter read through a typed method directly, avoiding runtime `Expression.Compile()` and repeated lookups.
3. **Property accessors and type registration**: `ObjectBase` property access, `SqlBuilder`-derived and `DbConnection`-derived types are all pre-registered into `TypeResolverHelper`, so the runtime can resolve them by name.
4. **DI registration**: services annotated with `[AutoRegister]` get MS DI registration code generated at compile time.

Among these, `CommonTableInfoProvider` lazily resolves and backfills column-level converters (`SqlTable.EnsureConverters`, initialized only once).

The generated content can be controlled in fine grain with the assembly-level `LiteOrmCodeGen`:

```csharp
[assembly: LiteOrm.Common.LiteOrmCodeGen(
    LiteOrm.Common.LiteOrmCodeGenKind.TableInfo |
    LiteOrm.Common.LiteOrmCodeGenKind.DataReaderMappers)]
```

`Kinds` accepts `TableInfo` / `DataReaderMappers` / `PropertyAccessors` / `AotTypeRegistration` / `AutoRegister`. Once declared, the attribute is the only source of truth and build properties are ignored; when absent, generators fall back to AOT auto-detection and generate everything. Declaring it in an ordinary build is a convenient way to verify AOT compatibility early.

## Notes and Caveats

Go through this list item by item before migrating to NativeAOT.

1. **Every hop of an association chain needs metadata.** A `[ForeignColumn]` target column is only looked up among types whose `TableInfo` is registered. When the target lives in another assembly that does not run the generator, you get "target column does not exist in joined tables". Chains longer than one hop also need `AutoExpand = true` on the intermediate hop, otherwise the next joined table is never expanded.
2. **Services must be `Scoped`.** `[AutoRegister]` defaults to `Singleton`, while `SessionManager` and the DAOs are `Scoped`. An entity service registered with the default resolves a second session in the root container, and transactions, connection reuse and `SessionManager.Current` all stop lining up; the usual symptoms are a transaction that throws but leaves its rows behind, or a `database is locked` error. Always write `[AutoRegister(Lifetime.Scoped)]` on entity services.
3. **All three `Column.Constant` forms work.** `Constant = RecordState.Enabled`, `Constant = 1` and `Constant = "Enabled"` all resolve against an enum property. Under AOT the source generator writes the value into `ColumnInfo.Constant`, and it is aligned to the property type and folded into `TableDefinition.ConstFilter` at runtime, matching JIT. `TableDefinition.ConstFilter` can still be assigned at runtime to narrow the visible set temporarily (switching tenants, for example), with the value carried in a `GenericSqlExpr`.
4. **Complex types require an explicit `[Column]`**: properties of complex types — arrays/collections and custom classes (`Object`) — are not auto-recognized as table columns without `[Column]`; mark them explicitly (specifying `DbType = Array` as needed) to persist them. `Json`/`Jsonb` mapped types are now auto-generated as columns.
5. **Column auto-mapping can be turned off**: with `[Table(AutoGenerateColumns = false)]`, the source generator collects only properties explicitly marked with `[Column]`; unmarked properties stay out of the table definition, matching the runtime `AttributeTableInfoProvider`.
6. **Converters must be declared or pre-registered**: complex/custom conversions must be declared via `Column.ConverterType`, `ForeignColumn.ConverterType`, or pre-registered via `SqlBuilder.RegisterDbValueConverter`. A global registration alone is not enough — the source generator has to see the attribute before it emits read code for that column. A converter must have a public parameterless constructor because the generator emits `new` directly; components that need configuration or secrets should use static initialization or environment variables instead.
7. **An entity caches exactly one read delegate**, decided by the data source dialect it binds to first. When the same entity is switched between data sources dynamically, either keep the column `DbValueType` identical across databases, or use a dedicated entity per data source, or fix the read rules with `DataReaderConverter.RegisterMapper<T>` during startup.
8. **Keep performance expectations level.** The source-generator path removes reflection and runtime compilation and beats pure reflection, but the reflection fallback and anonymous-type projections still box and reflect, so the whole picture trails JIT's typed reads. The gap shows up in row-by-row queries; writes and batch operations barely notice it.
9. **Prefer known strong types for projections.** Anonymous types take the `AotMapper` reflection path under AOT: workable, but slower, and it requires the target type's constructors and properties to survive trimming.
## Entity Associations

Associations are declared exactly as under JIT; syntax and limits live in [Entity Mapping](../core-usage/entity-mapping.en.md). The snippet below comes from `LiteOrm.AotDemo`: an order belongs to a user, a user belongs to a department, and a department has a parent department.

```csharp
[Table("AotDepartments")]
[TableJoin(typeof(AotDepartment), "ParentId", Alias = "Parent")]
public class AotDepartment : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    [Column("Name")]
    public string? Name { get; set; }
    [Column("ParentId")]
    public int? ParentId { get; set; }
}

// Projection columns live on the view; Foreign takes the alias declared in [TableJoin]
public class AotDepartmentView : AotDepartment
{
    [ForeignColumn("Parent", Property = nameof(AotDepartment.Name))]
    public string? ParentName { get; set; }
}
```

The order hangs off the user foreign key, and `AutoExpand = true` carries the chain one hop further, so the view can project the department name directly:

```csharp
[Table("AotOrders")]
public class AotOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }
    [Column("UserId")]
    [ForeignType(typeof(AotUser), Alias = "User", AutoExpand = true)]
    public int UserId { get; set; }
    [Column("Amount")]
    public double Amount { get; set; }
    [Column("AmountWithTax", Expression = "{Amount} * 1.1")]
    public double AmountWithTax { get; set; }
}

public class AotOrderView : AotOrder
{
    [ForeignColumn("User", Property = nameof(AotUser.UserName))]
    public string? UserName { get; set; }
    // Pass the Type directly and the alias falls back to the type name;
    // the upstream AutoExpand is what makes the Dept join possible
    [ForeignColumn(typeof(AotDepartment), Property = nameof(AotDepartment.Name))]
    public string? DeptName { get; set; }
}
```

What the generator emits looks like this (from the `TableInfo.g.cs` written to disk by `EmitCompilerGeneratedFiles`):

```csharp
new ColumnInfo
{
    PropertyName = "UserId",
    // ...
    ForeignTables = new ForeignTable[]
    {
        new ForeignTable { ForeignType = typeof(AotUser), Alias = "User", AutoExpand = true },
    },
},
// ...
new ColumnInfo
{
    PropertyName = "DeptName",
    // ...
    ForeignColumn = new ForeignColumnInfo { ForeignAlias = "Dept", Property = "Name" },
},
```

`TableInfo.JoinedTables` is filled as well (the self-join above becomes `Alias = "Parent"` with `ForeignKeys = "ParentId"`). At runtime `CommonTableInfoProvider` feeds that data through the same assembly pipeline as `AttributeTableInfoProvider`, producing a `TableView` complete with joined tables.

Business code is unchanged. Calling `SearchAsync()` with no condition pulls in the view columns and the joins:

```csharp
var orders = await orderService.SearchAsync();
foreach (var order in orders)
    Console.WriteLine($"{order.Id} {order.Amount} {order.UserName} {order.DeptName} {order.AmountWithTax:0.##}");

// Associated columns take part in filtering, ordering and paging; lambdas work too
var expr = Expr.From(typeof(AotOrderView))
    .Where(Expr.Prop(nameof(AotOrderView.DeptName)) == Expr.Const("R&D"))
    .OrderBy(Expr.Prop(nameof(AotOrder.Amount)).Desc())
    .Section(0, 2);
var page = await orderService.SearchAsync(expr);
var lambda = await orderService.SearchAsync(q => q.Where(x => x.DeptName == "R&D"));
```

The SQL it produces:

```sql
SELECT "User"."UserName", "Dept"."Name" AS "DeptName",
       "T0"."Id", "T0"."UserId", "T0"."Amount", "T0"."AmountWithTax" ...
FROM "AotOrders" "T0"
LEFT JOIN "AotUsers" "User" ON "T0"."UserId" = "User"."Id"
LEFT JOIN "AotDepartments" "Dept" ON "User"."DeptId" = "Dept"."Id"
WHERE "Dept"."Name" = 'R&D'
```

## Full CRUD and SearchAs Example

The `LiteOrm.AotDemo` entry point only does two things: wire up the container and run the scenarios in order.

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<IConfiguration>(configuration);
services.AddLiteOrm();

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var sp = scope.ServiceProvider;

DemoData.Reset(sp);                              // wipe and seed departments/users/orders

await CrudScenario.RunAsync(sp);                 // single-table CRUD
await AssociationScenario.RunAsync(sp);          // entity associations
await ProjectionScenario.RunAsync(sp);           // projections and aggregation
await JsonAndComputedScenario.RunAsync(sp);      // JSON and computed columns
await TransactionAndFilterScenario.RunAsync(sp); // transactions and fixed filtering
```

Scenario code is split by topic under `LiteOrm.AotDemo/Scenarios/`. Single-table CRUD covers sync and async, single and batch, entity updates and conditional updates, and the three delete flavors:

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
    [Column("Role")]
    public UserRole? Role { get; set; }
    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }
    [Column("Guid")]
    public Guid Guid { get; set; }
    [Column("Info")]
    public JsonNode? Info { get; set; }
    [Column("Duration")]
    public TimeSpan Duration { get; set; }
    [Column("DeptId")]
    [ForeignType(typeof(AotDepartment), Alias = "Dept", AutoExpand = true)]
    public int? DeptId { get; set; }
}
public class AotUserView : AotUser
{
    [ForeignColumn("Dept", Property = nameof(AotDepartment.Name))]
    public string? DeptName { get; set; }
}

// Service: the lifetime has to be Scoped
public interface IAotUserService : IEntityService<AotUser>, IEntityServiceAsync<AotUser>,
    IEntityViewService<AotUserView>, IEntityViewServiceAsync<AotUserView> { }

[AutoRegister(Lifetime.Scoped)]
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

See the full code in [AotDemo/Program.cs](../../LiteOrm.AotDemo/Program.cs) and [AotDemo/Scenarios](../../LiteOrm.AotDemo/Scenarios). Example publish command:

```bash
dotnet publish LiteOrm.AotDemo/LiteOrm.AotDemo.csproj -c Release -r win-x64 --self-contained
```

## Related Links

- [Entity Mapping](../core-usage/entity-mapping.en.md)
- [View Models and Services](../core-usage/view-models-and-services.en.md)
- [Data Mapping and Value Conversion](./data-mapping.en.md)
- [Tenant Isolation](../typical-applications/tenant-isolation.en.md)
- [Performance Tuning](./performance.en.md)
