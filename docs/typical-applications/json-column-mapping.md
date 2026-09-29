# 对象的 JSON 列存储与字段查询

有些字段天生不适合拆成列：下单时的渠道与会员等级、商品的自由参数、第三方回调的原始报文。给每个字段建一列，加一个字段就要改一次表结构。常见做法是把整段结构序列化成 JSON 存进一个文本列，需要过滤时再按 JSON 路径取值。

LiteOrm 把这件事分成两半：整段 JSON 的存取交给值转换器，SQL 里按字段取值的表达式交给 Lambda 成员处理器。

| 目标 | 做法 |
| --- | --- |
| 整段 JSON 存进去、读回来 | 注册「对象 ↔ JSON 文本」转换器，并声明该类型按 JSON 存 |
| 还要按对象内部字段过滤、排序 | 给这个类登记 Lambda 成员兜底，把成员映射到 `JsonValue` 函数 |
| 字段结构不固定、不想定义类 | 属性类型直接写 `JsonNode`，用内置的索引器与 `GetValue<T>()`，见[数据映射](../advanced-topics/data-mapping.md#33-jsonnode-映射导航) |

下面按「存整段 JSON」「按字段过滤」「按整个对象匹配」三个需求展开，文中的 SQL 都是真实渲染出来的结果，方言在每段里标出。

## 需求一：订单扩展信息整段存一列

需求：Orders 表要装下单渠道、会员等级、标签这些扩展信息，字段还会继续加，不想每加一个就改表结构。

做法分两步。先定一个普通类：

```csharp
using System.Text.Json;

/// <summary>订单扩展信息，整段以 JSON 文本存进 Settings 列。</summary>
public class OrderSettings
{
    public string? Channel { get; set; }

    public int Level { get; set; }

    public List<string>? Tags { get; set; }
}
```

再把「存储形式」和「读写转换」各注册一次，放在程序启动处：

```csharp
using LiteOrm;
using LiteOrm.Common;

// 声明存储形式：OrderSettings 在库里按 JSON 处理
DbValueTypeMap.Set(typeof(OrderSettings), DbValueType.Json);

SqlBuilder.Instance.RegisterDbValueConverter<SqlBuilder, string, OrderSettings>(
    targetType: DbValueType.Json,
    fromDb: json => JsonSerializer.Deserialize<OrderSettings>(json)!,   // 读取：库中文本还原成对象
    toDb: value => JsonSerializer.Serialize(value)                      // 写入：对象序列化成文本
);
```

实体上只声明属性类型：

```csharp
[Table("Orders")]
public class Order
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("OrderNo", AllowNull = true)]
    public string? OrderNo { get; set; }

    // 类型已由 DbValueTypeMap 声明，列上不用再写 DbType
    [Column("Settings")]
    public OrderSettings? Settings { get; set; }
}
```

建表时这列按 JSON 生成，各方言给出的列类型：

| 数据库 | `Settings` 列类型 |
| --- | --- |
| SQLite / SQL Server | `TEXT` |
| MySQL / PostgreSQL | `JSON` |
| Oracle | `CLOB` |

写进去的对象落成一串文本，读出来又是对象，业务代码里不用出现序列化调用：

```csharp
await orderService.InsertAsync(new Order
{
    OrderNo = "SO-1",
    Settings = new OrderSettings { Channel = "APP", Level = 5 }
});
```

```json
// 落库的 Settings 列
{"Channel":"APP","Level":5,"Tags":null}
```

```csharp
var order = await orderService.Search(o => o.OrderNo == "SO-1").FirstOrDefaultAsync();

// 读回来已经是对象
Console.WriteLine(order!.Settings!.Level);   // 5
```

上面是全局注册的做法。只有一列要按 JSON 存、不想动全局注册时，把转换逻辑写成一个实现 `IDbValueConverter<string, OrderSettings>` 的类，用 `ConverterType` 挂到列上，列上仍要写 `DbType = DbValueType.Json`：

```csharp
using System.Text.Json;
using LiteOrm.Common;

/// <summary>OrderSettings 与 JSON 文本的双向转换器。</summary>
public sealed class OrderSettingsJsonConverter : IDbValueConverter<string, OrderSettings>
{
    Type IDbValueConverter.ValueType => typeof(OrderSettings);

    // 泛型版本走 JIT 路径，非泛型版本走源生成（AOT）路径
    DbConvertHandler<string, OrderSettings>? IDbValueConverter<string, OrderSettings>.DbReadConverter
        => json => JsonSerializer.Deserialize<OrderSettings>(json)!;

    DbConvertHandler<OrderSettings, object>? IDbValueConverter<string, OrderSettings>.DbWriteConverter
        => value => JsonSerializer.Serialize(value);

    DbConvertHandler? IDbValueConverter.DbReadConverter
        => json => JsonSerializer.Deserialize<OrderSettings>((string)json)!;

    DbConvertHandler? IDbValueConverter.DbWriteConverter
        => value => JsonSerializer.Serialize((OrderSettings)value);
}
```

```csharp
[Column("Settings", DbType = DbValueType.Json, ConverterType = typeof(OrderSettingsJsonConverter))]
public OrderSettings? Settings { get; set; }
```

列级转换器优先级最高，会盖住全局注册；需要公共无参构造函数，框架解析列元数据时用 `Activator.CreateInstance` 实例化，要注入配置或密钥的组件改用静态初始化或环境变量。AOT 下这类列必须显式写 `ConverterType`，源生成器看到它才会为该列生成读取映射，只靠全局注册不够；完整写法见[敏感字段加密与脱敏](./sensitive-data-protection.md)的方式一。

要点：

- 只注册转换器还不够，框架得知道类型按 JSON 存。`DbValueTypeMap.Set(typeof(T), DbValueType.Json)` 或列上 `DbType = DbValueType.Json`，任选一处；都不写时类型退回 `Object`，转换器查不到，读到的是原始文本。
- `RegisterDbValueConverter` 的 `TDbType` 写 `string`。`DbValueType.Json` 落库就是文本，驱动按 `GetString` 取值，转换器收到的是字符串。
- 转换器不处理 null。属性值为 null 时框架统一送 `DBNull.Value`，读回来是 null 或 `default`。
- `DbValueTypeMap.Set` 全进程生效，登记后项目里所有 `OrderSettings` 属性都按 JSON 处理，`Expr` 条件里的裸值参数也走同一张注册表。只影响一列就用上面的列级转换器。
- 反序列化失败会直接抛出。库里已有脏数据时，建议在转换器里包一层 `try`，解析不动就返回默认值并记日志，别让一条脏数据带崩整个查询。

## 需求二：按对象里的字段过滤

需求：列表页要按「渠道 = APP」筛单，还要按会员等级排序，可等级埋在 JSON 文本里，SQL 的 `=` 和 `>` 比不了。

做法：给这个类的属性注册 Lambda 成员处理器。不注册时 `o.Settings!.Channel` 会被当成方法调用，属性名直接充当函数名，生成一段跑不通的 SQL：

```sql
-- 注册前
WHERE Channel("Settings") = 'APP'
```

注册之后，成员访问变成 JSON 取值函数。只给类型就能给整类的成员挂上兜底，路径在处理器里按成员名拼：

```csharp
using LiteOrm.Common;
using static LiteOrm.Common.Expr;

// 给 OrderSettings 登记成员兜底；处理器里用 node.Member.Name 拼 JSON 路径
LambdaExprConverter.RegisterMemberHandler(typeof(OrderSettings), handler: (node, converter) =>
    new FunctionExpr(
        "JsonValue",
        converter.Convert(node.Expression!).AsValue(),   // node.Expression 是宿主对象，这里就是 Settings 列
        Const("$." + node.Member.Name)));                 // JSON 路径，用 Const 包成字面量
```

路径由 `node.Member.Name` 拼出，与默认的 `JsonSerializer` 键名一致。序列化时配了命名策略（`PropertyNamingPolicy = JsonNamingPolicy.CamelCase`），路径要按同一策略转换成员名，两边差一个字母就查不到数据。

只给类型时登记的是**类型级兜底**，不在注册时枚举成员，而是解析到该类型的成员访问时接管。个别成员要换路径、或要额外处理时，再单独注册一次，显式指定的成员注册优先于类型级兜底，且与两者先后顺序无关：

```csharp
// 库里的键名与属性名不同（如 camelCase）时单独注册，优先于上面的类型级兜底
LambdaExprConverter.RegisterMemberHandler(typeof(OrderSettings), nameof(OrderSettings.Channel),
    (node, converter) => new FunctionExpr(
        "JsonValue",
        converter.Convert(node.Expression!).AsValue(),
        Const("$.channel")));
```

之后在服务、DAO 里按普通属性写就行：

```csharp
var appOrders = await orderService.SearchAsync(
    o => o.Settings!.Channel == "APP" && o.Settings!.Level > 3, cancellationToken: ct);
```

同一个条件在不同方言下落到各自的 JSON 函数：

```sql
-- SQLite
WHERE json_extract("T0"."Settings", '$.Channel') = 'APP'
-- MySQL
WHERE JSON_UNQUOTE(JSON_EXTRACT(`T0`.`Settings`, '$.Channel')) = 'APP'
-- SQL Server / Oracle
WHERE JSON_VALUE("T0"."Settings", '$.Channel') = 'APP'
-- PostgreSQL
WHERE "t0"."settings" ->> '$.Channel' = 'APP'
```

排序、投影也能直接用。把「列名 + 路径」抽成一个小函数，路径照样内联成字面量：

```csharp
using static LiteOrm.Common.Expr;

static FunctionExpr JsonField(string column, string path)
    => new FunctionExpr("JsonValue", Prop(column), Const(path));

var query = Expr.From<Order>()
    .Where(JsonField("Settings", "$.Level") > 3)
    .OrderBy(JsonField("Settings", "$.Channel").Asc())
    .Select(Prop("OrderNo"), JsonField("Settings", "$.Channel"));
```

```sql
SELECT "T0"."OrderNo", json_extract("T0"."Settings", '$.Channel')
FROM "Orders" "T0"
WHERE json_extract("T0"."Settings", '$.Level') > @0
ORDER BY json_extract("T0"."Settings", '$.Channel')
```

要点：

- 处理器按「类型 + 成员名」或「类型」注册。只给类型登记的是类型级兜底，解析到该类型的成员访问才接管，与 `RegisterMethodHandler(typeof(SomeType))` 对称；显式指定成员的注册（按类型或按成员名）优先命中，要改某个成员就单独注册它。只按成员名注册会把其它类型的同名成员一起接管，别这么写。
- 类型级兜底对集合属性同样生效，`o.Settings!.Tags!.Contains("vip")` 会渲染成 `'vip' IN json_extract("Settings", '$.Tags')`，这段 SQL 没有意义。数组内容过滤要另注册一个按方言实现的自定义函数覆盖该成员。
- 处理器只作用于嵌套对象上的成员。宿主是 Lambda 根参数时（`o => o.OrderNo`）走列路径，更早一步就处理完了，给实体类型注册同名成员处理器不会命中。
- 注册要在第一次转换之前完成，注册表是静态的，进程跑起来之后再改，同一条查询改前改后生成的 SQL 会不一样；已注册的成员也不再参与常量折叠。
- 路径统一用 `Expr.Const("$.Channel")`。内置的 `JsonValue`、`JsonExtract` 扩展方法把路径当普通值、生成绑定参数，SQLite 与 MySQL 接受，SQL Server 与 Oracle 的 `JSON_VALUE` 只认字面量，要跨库就自己构造 `FunctionExpr`。
- 一层成员访问对应一层路径。`o.Settings.Profile.Level` 这种两级嵌套直接写整条路径 `Const("$.Profile.Level")`；在中间类型上也注册处理器会得到嵌套的 `JSON_VALUE(JSON_VALUE(...), ...)`，未必是想要的。
- 数值字段取出来是文本（MySQL 上是 `JSON_UNQUOTE` 的结果），与数字比较靠各库隐式转换，够用。想显式转类型可以套 `.Cast(DbValueType.Int32)`，各方言会渲染成自己接受的类型名，MySQL 上是 `CAST(... AS SIGNED)`。

## 需求三：按整个对象匹配

需求：对接方回传一整份配置，要判断库里是否已经存过同样的一份。

做法：属性直接比对，参数会按全局注册的转换器序列化成 JSON 文本：

```csharp
var exists = await orderService.SearchOneAsync(
    o => o.Settings == new OrderSettings { Channel = "APP", Level = 5, Tags = null });
```

```sql
WHERE "Settings" = @0
-- @0 = {"Channel":"APP","Level":5,"Tags":null}
```

要点：

- 比的是整段 JSON 文本，序列化结果要对得上：属性顺序、null 值、数值格式都得一致。上面 `Tags = null` 必须写出来，省略它就少一个字段，比不中。
- 这条走的是裸值参数的转换路径：参数的取值类型为 `Default` 时，框架按「值的运行时类型」查注册表，命中 `DbValueTypeMap` 登记的 JSON 才落到转换器上。所以它依赖[需求一](#需求一订单扩展信息整段存一列)的全局注册，换成列级 `ConverterType` 就不成立了。
- 序列化器换成 `Newtonsoft.Json` 时属性顺序可能不同，这个写法不再稳，改用主键或字段级条件。

## 何时不适合

- **要高频过滤或排序的字段**：每个条件都在解析 JSON，用不上普通索引，数据量大时开销明显。命中量高的字段还是落成物理列；MySQL 可以建生成列再加索引，但那是数据库侧的活。
- **要参与 JOIN 或跨表聚合的字段**：JSON 路径不能像普通列那样放进 `ON`。PostgreSQL 把列换成 jsonb 可以建 GIN 索引，其它库没有对等能力。
- **字段需要强校验**：JSON 列在数据库侧没有类型约束，属性名和路径名写错，要到查询报错那一刻才发现。字段稳定又要约束的，老实建列。
- **要按字段做范围检索**：路径取出来的是文本，范围比较依赖各方言的隐式转换，跨库行为不一致，这类字段不适合留在 JSON 里。

## 相关链接

- [返回目录](../README.md)
- [数据映射与值转换](../advanced-topics/data-mapping.md)
- [Lambda 查询指南](../core-usage/lambda-guide.md)
- [表达式扩展](../extensibility/expression-extension.md)
- [敏感字段加密与脱敏](./sensitive-data-protection.md)
- [AOT 支持](../advanced-topics/aot.md)
