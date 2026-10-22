# Lambda 查询指南

Lambda 是 LiteOrm 最直观的查询方式，强类型、可读性最好，适合大部分业务查询。本文聚焦 Lambda 查询的过滤、排序、参数化、三目运算和子查询用法。

三种查询方式的对比与选型见[查询总览](./query-overview.md)；动态拼装条件请看 [Expr 使用指南](./expr-guide.md)；DAO 层手写 SQL 请看 [ExprString 使用指南](./exprstring-guide.md)。

## 1. 基础过滤

```csharp
var adults = await userService.SearchAsync(u => u.Age >= 18);
var admins = await userService.SearchAsync(u => u.UserName.Contains("admin"));
var targets = await userService.SearchAsync(u => new[] { 1, 2, 3 }.Contains(u.Id));
```

Lambda 中的属性访问会被解析成 `PropertyExpr`，比较/字符串/集合操作会被解析成 `LogicExpr`，最终统一走 `Expr` → SQL 的生成管道。

### 1.1 逻辑组合（`&&`、`||`、`!`）

Lambda 中可以直接使用 C# 逻辑运算符组合条件：

```csharp
// AND + OR 组合
var result = await userService.SearchAsync(
    u => u.Age > 18 && u.Status == 1 || u.IsVip
);

// NOT 取反
var active = await userService.SearchAsync(
    u => !u.IsDeleted && u.IsActive
);

// 加括号改变优先级
var complex = await userService.SearchAsync(
    u => u.Age > 18 && (u.Status == 1 || u.IsVip)
);
```

`&&` 转换为 `AND`，`||` 转换为 `OR`，`!` 转换为 `NOT`。运算符优先级遵循 C# 语义：`!` 优先于 `&&`，`&&` 优先于 `||`。如需改变优先级，请加括号。

### 1.2 支持的字符串与方法

LiteOrm 在启动时通过 `LiteOrmLambdaHandlerInitializer` 注册了一批 Lambda 方法处理器，以下是实际支持的字符串方法（对照源码 `LiteOrm.Common/Converter/LiteOrmLambdaHandlerInitializer.cs`）：

| 方法 | SQL 语义 | 示例 |
|------|----------|------|
| `string.Contains(text)` | `LIKE '%text%'` | `u => u.Name.Contains("admin")` |
| `string.StartsWith(text)` | `LIKE 'text%'` | `u => u.Name.StartsWith("admin")` |
| `string.EndsWith(text)` | `LIKE '%text'` | `u => u.Name.EndsWith("admin")` |
| `string.Concat(...)` | `CONCAT(...)` | `u => string.Concat(u.FirstName, " ", u.LastName)` |
| `string.ToUpper()` | `UPPER` | `u => u.Name.ToUpper() == "ADMIN"` |
| `string.ToLower()` | `LOWER` | `u => u.Name.ToLower() == "admin"` |
| `string.Trim()` / `TrimStart()` / `TrimEnd()` | `TRIM` / `LTRIM` / `RTRIM` | `u => u.Name.Trim() == "admin"` |
| `string.Remove(startIndex)` | `LEFT` | `u => u.Code.Remove(3) == "ABC"` |
| `string.Length`（属性） | `CHAR_LENGTH` / `LEN` | `u => u.Name.Length > 5` |
| `Equals(obj)` | `=` | `u => u.Name.Equals("admin")` |
| `ToString()` / `ToString(format)` | 原值 / `Format` | `u => u.CreateTime.ToString("yyyy-MM-dd")` |

集合方法：

| 方法 | SQL 语义 | 示例 |
|------|----------|------|
| `IList.Contains(item)` / `Enumerable.Contains(collection, item)` | `IN` | `u => new[] { 1, 2, 3 }.Contains(u.Id)` |

此外还支持以下类型的成员和方法：

- **DateTime**：`DateTime.Now`（`CURRENT_TIMESTAMP`）、`DateTime.Today`（`CURRENT_DATE`）、`AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds`（`DATE_ADD` / `DATEADD`）
- **Math**：`Abs`、`Max`、`Min`、`Floor`、`Ceiling`、`Round`、`Pow`、`Sqrt`、`Truncate` 等（直接映射为 SQL 数学函数）
- **TimeSpan**：`TotalSeconds` / `TotalDays` / `TotalHours` / `TotalMinutes` / `TotalMilliseconds`。当两个日期相减时（如 `(DateTime.Now - u.CreateTime).TotalDays`），自动转换为 `DateDiffDays` 等日期差函数

## 2. 排序

Lambda 查询中，排序通过 `OrderBy` / `OrderByDescending` / `ThenBy` / `ThenByDescending` 链式调用实现。

### 2.1 单列排序

```csharp
// 按创建时间升序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.CreateTime)
);

// 按年龄降序
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => u.Age)
);
```

### 2.2 多列排序（ThenBy）

```csharp
// 先按部门升序，同部门内按创建时间降序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.DeptId)
          .ThenByDescending(u => u.CreateTime)
);
```

`ThenBy` / `ThenByDescending` 必须在 `OrderBy` / `OrderByDescending` 之后使用，可以级联多个。

### 2.3 排序与分页组合

```csharp
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0)
          .Take(20)
);
```

### 2.4 按计算表达式排序

```csharp
// 按计算字段排序
var users = await userService.SearchAsync(
    q => q.OrderBy(u => u.FirstName + " " + u.LastName)
);

// 按时间差排序
var users = await userService.SearchAsync(
    q => q.OrderByDescending(u => (DateTime.Now - u.CreateTime).TotalMilliseconds)
);
```

> Lambda 中的字符串 `+` 会在解析阶段被转换为 concat，最终通过 `SqlBuilder.BuildConcatSql` 按方言输出 `CONCAT(a,b,...)` 或 `a || b`。手写 `Expr` 时则需要显式使用 `.Concat(...)`，见 [Expr 使用指南](./expr-guide.md#字符串拼接不要用-用-concat)。

### 2.5 Skip/Take 分页语义

Lambda 查询中，分页通过查询构建器的 `.Skip(skip)` 和 `.Take(take)` 链式方法实现：

```csharp
// 取第 1 页（每页 10 条）
var paged = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
          .OrderBy(u => u.UserName)
          .Skip(0)
          .Take(10)
);
```

- `Skip(n)`：跳过前 `n` 条记录，对应 SQL 的 `OFFSET n`（部分数据库为 `LIMIT n, ...` 或 `ROWNUM` 方言）。
- `Take(n)`：取 `n` 条记录，对应 SQL 的 `LIMIT n`（或 `FETCH FIRST n ROWS ONLY`）。
- `Skip` 和 `Take` 可以单独使用，也可以组合使用；分页时通常先 `OrderBy` 再 `Skip`/`Take`。
- 大偏移量分页（如 `Skip(10000)`）性能较差，建议改用基于 ID 的游标分页，见[性能优化](../advanced-topics/performance.md#33-分页优化)。

## 3. 变量捕获与参数化

```csharp
var keyword = "admin";
var users = await userService.SearchAsync(u => u.UserName.Contains(keyword));
```

Lambda 外定义的变量会被参数化。如果是 `DateTime.Now` 这类值，希望参数化时应先赋给变量：

```csharp
var now = DateTime.Now;
var users = await userService.SearchAsync(u => u.CreateTime <= now);
```

## 4. 三目运算符会转成 `CASE`

```csharp
var users = await userService.SearchAsync(
    u => (u.Age >= 18 ? "Adult" : "Minor") == "Adult"
);
```

这类 Lambda 会先转成 `Expr.If(...)`，最终生成 SQL `CASE WHEN ... THEN ... ELSE ... END`。多条件 `CASE` 可通过 `Expr.Case(...)` 构造，见 [Expr 使用指南](./expr-guide.md#14-类型转换与条件值)。

## 5. `Exists` 与 `ExistsRelated`

### 5.1 显式 `Exists`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => Exists<Department>(d => d.Id == u.DeptId && d.Name == "研发中心")
);
```

适合你想**自己明确写出关联条件**的场景。`Exists<T>` 是 `Expr` 的静态方法，等价的 `Expr` 写法见 [Expr 使用指南](./expr-guide.md#21-显式-exists)。

### 5.2 自动关联 `ExistsRelated`

```csharp
using static LiteOrm.Common.Expr;

var users = await userService.SearchAsync(
    u => ExistsRelated<DepartmentView>(d => d.Name == "研发中心")
);
```

`ExistsRelated` 会根据 `ForeignType` / `TableJoin` 等元数据自动补关联条件。适合模型里已经声明好关联路径，只想"按关联表条件过滤主表"的场景。匹配逻辑、继承链规则和 `ConstFilter` 行为请看[关联查询](./associations.md)。

### 5.3 嵌套 Lambda 与参数作用域

内层 Lambda 的参数按引用区分作用域，与外层参数同名时不会互相覆盖：

```csharp
// 内层与外层参数均命名 u，转换完成后外层 u 的引用依然有效
var users = await userService.SearchAsync(
    u => u.Age > 18 && Exists<Department>(u => u.Name == "研发中心") && u.UserName != null
);
```

多参数 Lambda 的全部参数都会注册：第一个参数对应别名 `T0` 的根表，其余参数获得新别名 `T{n}`。

自定义方法处理器里转换内层 Lambda 时，可以通过构造重载把外层参数上下文传进子转换器，内层即可引用外层参数，且父子转换器共享别名编号，嵌套作用域不会分配到重复别名：

```csharp
LambdaExprConverter.RegisterMethodHandler("ExistsIn", (node, converter) =>
{
    var lambda = (LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand;
    var child = new LambdaExprConverter(lambda, converter);
    // 子转换器的根表作为子查询外表，InnerExpr 中可见外层参数
    return new ForeignExpr(child.Table!.Type)
    {
        Alias = child.Table!.Alias,
        InnerExpr = child.ToLogicExpr()
    };
});
```

扩展点注册的完整机制见[表达式扩展](../extensibility/expression-extension.md)。

### 5.4 连接查询 `Join` 与 `JoinRelated`

`Join<T2>` 显式给出连接条件，`JoinRelated<T2>` 按外键元数据自动推断。两者都是 `From<T1>()` 返回的 `QuerySource<T1>` 上的实例方法，左表类型已经定住，所以只写右表类型：

```csharp
using static LiteOrm.Common.Expr;

// 显式条件
var expr = Query<Order, IEnumerable<(Order, Customer)>>(
    q => From<Order>()
          .Join<Customer>((o, c) => o.CustomerId == c.Id)
          .Where(x => x.Item2.Name == "VIP")
);

// 需要投影时，结果类型写 IEnumerable<object>（匿名类型无法写进泛型实参）
var projected = Query<Order, IEnumerable<object>>(
    q => From<Order>()
          .Join<Customer>((o, c) => o.CustomerId == c.Id)
          .Select(x => new { x.Item1.Id, x.Item2.Name })
);

// 自动推断外键条件
var related = Query<Order, IEnumerable<(Order, Customer)>>(
    q => From<Order>().JoinRelated<Customer>()
);

// 自动条件 + 附加条件，两者按 AND 合并
var merged = Query<Order, IEnumerable<(Order, Customer)>>(
    q => From<Order>().JoinRelated<Customer>((o, c) => o.Amount > 0)
);

// 一次连接多张表：结果平铺为 (Order, Customer, Region)，最多 7 个元素
var multiple = Query<Order, IEnumerable<(Order, Customer, Region)>>(
    q => From<Order>()
          .Join<Customer, Region>((o, c, r) =>
              o.CustomerId == c.Id && c.RegionId == r.Id)
          .Where(x => x.Item3.Name == "North")
);

// 多表自动推断：每个右表都按最内层主表寻找关联
var multipleRelated = Query<Order, IEnumerable<(Order, Customer, Region)>>(
    q => From<Order>().JoinRelated<Customer, Region>()
);
```

返回的是元素元组序列，`Where` / `OrderBy` 会保持元素元组类型，`Select` 则会把元素类型改成投影结果。元组元素用 `Item1`、`Item2`……`Item7` 访问，分别对应左表和各个右表，解析时各自带上正确的表别名。`Join<T2, ..., Tn>` / `JoinRelated<T2, ..., Tn>` 的 `n` 最多到 7，结果元组按顺序平铺；显式条件写在一个 lambda 中，参数依次是左表和各个右表，转换器会按每个 `&&` 分支引用到的最右侧右表，把条件拆到对应的 JOIN 段。

`Join` / `JoinRelated` 只能在 Lambda 内解析，所以上面用 `Expr.Query` 构造；`Expr.Query` 未标记过期，与 `Expr.From<T>()` 链式写法一样是受支持的入口。

几处要点：

- `Join` 生成的 SQL 形如 `FROM Orders T0 LEFT JOIN Customers T1 ON T0.CustomerId = T1.Id`，连接类型默认左连接。
- `JoinRelated` 推断不到关联关系时抛 `InvalidOperationException`，提示改用带条件的 `Join`。这类表需要先用 `[ForeignType]` 或 `[TableJoin]` 声明关联。
- 支持多级连接，`From<A>().Join<B>(...).Join<C>(...)` 会连续追加 JOIN。连接结果仍是 `QuerySource`，所以能一路接下去；第二级 ON 里用 `x.Item1.Column` 引用左侧已有的表，第三级的左参数是 `((A, B), C)`，写法是 `y.Item2.Column` 与 `y.Item1.Item1.Column`。
- 多表 `Join<T2, ..., Tn>` 的单个显式条件会按 `&&` 拆分；如果某个新增表没有任何条件落点，该 JOIN 段会生成 `CROSS JOIN`。多表 `JoinRelated<T2, ..., Tn>` 的每个右表都按最内层主表推断关联，与链式 `JoinRelated` 的行为一致；显式条件按同样的规则拆到各段，并与自动条件取 AND。
- 从已经是元组的 `QuerySource<T>` 再调用多表重载时，左侧元组会作为结果的 `Item1` 保留，整体仍是嵌套结构。要得到完全平铺的 3–7 元组，请从单表源一次写全右表类型。
- 连接表别名按分配顺序生成，需要按别名手工拼 `SelectItemExpr` 时从 `from.Source.Alias` 与 `from.Joins[i].Source.Alias` 读取，不要写死 `T1`，外层查询可能已经占用过该编号。
- `Select(x => x.Item1)` 这种整表投影不支持，`Select` 里请写具体列，例如 `Select(x => new { x.Item1.Id, x.Item2.Name })`。嵌套元组上的 `x.Item1` 直接当列用会抛 `NotSupportedException`。
- `Join` / `JoinRelated` 后接 `Select` 时元素类型已经变化，`Expr.Query<T, TResult>` 的 `TResult` 不能再写连接结果的元组类型，应改为 `IEnumerable<object>`（或与投影结果类型兼容的接口）。

`QuerySource<T>` 实现 `IQueryable<T>`，所以在 Lambda 之外也能拼查询，再交给 DAO 转成 SQL 执行：

```csharp
// 直接从容器取，已绑定执行器，不需要先拿 DAO
var users = serviceProvider.GetRequiredService<QuerySource<User>>();

foreach (User user in users.Where(u => u.Age >= 18)) { }
List<User> adults = users.Where(u => u.Age >= 18).ToList();

// 异步：Where 返回静态类型 IQueryable，用 AsSource 收窄回 QuerySource 即可用异步实例方法
List<User> asyncAdults = await users.Where(u => u.Age >= 18).AsSource().ToListAsync();
int count = await users.Where(u => u.Age >= 18).AsSource().CountAsync();

await foreach (User u in users.Where(u => u.Age >= 18).AsSource().ToAsyncEnumerable()) { }

// 投影之后同样适用，AsSource 的元素类型会跟着 Select 走
List<string> names = await users.Where(u => u.Age >= 18)
    .Select(u => u.Name)
    .AsSource()
    .ToListAsync();

// 链尾调用 GetQueryExpr() 把查询转成 Expr，不需要执行器
var expr = users.Where(u => u.Age >= 18).Select(u => new { u.Id, u.Name }).GetQueryExpr();

// 也可以交给 DAO 执行，任意 IQueryable 源都接受
var dao = serviceProvider.GetRequiredService<ObjectViewDAO<User>>();
var result = await dao.SearchAs(
    users.Where(u => u.Age >= 18).Select(u => new { u.Id, u.Name })).ToListAsync();
```

`Expr.From<T>()` 返回的实例不带执行器，只能用来拼表达式；直接枚举或调用 `ToList` 会抛 `InvalidOperationException`，提示补上执行器。`QuerySource<T>` 与其 DAO 生命周期一致，不要缓存到单例或静态字段里。

## 6. 投影查询（`SearchAs` / `SearchOneAs`）

`SearchAs` / `SearchOneAs` 有两组重载：接收 IQueryable Lambda，或接收已拼好的 `IQueryable<T>`。两者都把结果投影为指定类型（自定义类、匿名类均可），适合「只取部分列」或「组装成视图 / 摘要对象」。

```csharp
// IQueryable Lambda 形式
List<UserSummary> summaries = userService.SearchAs(
    q => q.Where(u => u.Age >= 18)
          .Select(u => new UserSummary { UserName = u.UserName, Age = u.Age })
);

// 也可以先用 Expr.From<T>() 拼好查询，再直接传入
var anon = userService.SearchAs(
    Expr.From<User>().Where(u => u.Age >= 18).Select(u => new { u.UserName, u.Age })
);

// 只过滤不投影：TResult 推断为实体类型
List<User> adults = userService.SearchAs(Expr.From<User>().Where(u => u.Age >= 18));

// 单条
UserSummary? one = userService.SearchOneAs(
    Expr.From<User>().Where(u => u.Id == 1).Select(u => new UserSummary { UserName = u.UserName, Age = u.Age })
);
```

异步版本为 `SearchAsAsync` / `SearchOneAsAsync`：

```csharp
var list = await userService.SearchAsAsync(
    q => q.Where(u => u.Age >= 18).Select(u => new { u.UserName, u.Age })
);

var list2 = await userService.SearchAsAsync(
    Expr.From<User>().Where(u => u.Age >= 18).Select(u => new { u.UserName, u.Age })
);
```

> `SearchAs` 会把 Lambda 或 `IQueryable<T>` 查询转成 `SelectExpr` 后执行（等价于 [查询总览](./query-overview.md#3-search-vs-searchas) 中的 `SelectExpr` 用法）。结果映射由 `DataReaderConverter` 完成：未注册的普通类 / 匿名类按「成员名 ↔ 列别名」匹配，注册过的实体类型按位置匹配，建议优先使用 DTO / 匿名类投影。分表时通过 `tableArgs` 参数指定分表名，缺省取查询根上的分表参数。

## 7. JsonNode 查询

LiteOrm 原生支持 `System.Text.Json.Nodes.JsonNode`（含 `JsonObject`、`JsonArray`、`JsonValue`）类型的属性，自动映射为 JSON 列（字符串存储），并支持在 Lambda 中直接通过索引器和 `GetValue<T>()` 查询 JSON 字段。

> JsonNode 属性的自动映射与序列化机制详见 [数据映射与值转换](../advanced-topics/data-mapping.md#33-jsonnode-映射导航)。

### 7.1 基础用法：索引器访问

`JsonNode` 索引器（`this[string]` / `this[int]`）会被翻译为 `JsonExtract` 函数，返回 JSON 片段：

```csharp
// 查询 Settings 中 name 为 "admin" 的配置
var result = await configDAO.Search(
    c => c.Settings!["name"].GetValue<string>() == "admin"
).ToListAsync();

// 嵌套访问：Settings.profile.age > 18
var adults = await configDAO.Search(
    c => c.Settings!["profile"]["age"].GetValue<int>() > 18
).ToListAsync();

// 数组访问：Settings.tags[0] == "vip"
var vips = await configDAO.Search(
    c => c.Settings!["tags"][0].GetValue<string>() == "vip"
).ToListAsync();
```

### 7.2 标量值提取：`GetValue<T>()`

`GetValue<T>()` 会被翻译为 `JsonValue` 函数，用于提取标量值（字符串、数字、布尔等）：

| C# 写法 | 映射函数 | 说明 |
|---------|---------|------|
| `json["key"].GetValue<string>()` | `JsonValue` | 提取字符串标量 |
| `json["key"].GetValue<int>()` | `JsonValue` | 提取整数标量 |
| `json["key"].GetValue<bool>()` | `JsonValue` | 提取布尔标量 |
| `json["key"]` (无 GetValue) | `JsonExtract` | 返回 JSON 片段，用于继续索引 |

```csharp
// 提取数值并比较
var result = await configDAO.Search(
    c => c.Settings!["score"].GetValue<int>() >= 90
).ToListAsync();

// 提取布尔值
var enabled = await configDAO.Search(
    c => c.Settings!["enabled"].GetValue<bool>() == true
).ToListAsync();
```

### 7.3 索引链与路径规则

LiteOrm 会自动解析连续的索引器调用，拼接为正确的 JSON 路径：

- **字符串键** → 点号路径（`$.name`）
- **整数索引** → 方括号路径（`$[0]`）
- **混合嵌套** → 组合路径（`$.items[0].name`）

```csharp
// 等价于 JSON 路径 $.items[2].price
var price = await configDAO.Search(
    c => c.Settings!["items"][2]["price"].GetValue<decimal>() > 100
).ToListAsync();
```

> 如果索引键是**常量**（字符串或整数），路径会在编译期拼接完成；如果是动态变量，则在运行时拼接。两种方式都支持。

### 7.4 各数据库 JSON 函数映射

| 通用函数名 | SQL Server | MySQL | PostgreSQL | SQLite | Oracle |
|-----------|-----------|-------|------------|--------|--------|
| **JsonExtract** | `JSON_QUERY` | `JSON_EXTRACT` | `->` 运算符 | `json_extract` | `JSON_QUERY` |
| **JsonValue** | `JSON_VALUE` | `JSON_UNQUOTE(JSON_EXTRACT(...))` | `->>` 运算符 | `json_extract` | `JSON_VALUE` |

不同数据库对 JSON 函数的支持程度不同，请根据实际使用的数据库验证。如需在 `Expr` 中手动构建 JSON 查询，见 [表达式扩展](../extensibility/expression-extension.md#9-json-函数扩展)。

## 8. 常见问题

### 8.1 Lambda 中不支持的方法会怎样？

如果 Lambda 中调用了未注册处理器的方法（即 `LiteOrmLambdaHandlerInitializer` 未覆盖的方法），LiteOrm 会在解析阶段抛出异常，提示该方法不被支持。这是设计上的安全保障：未知方法不会被静默忽略，解析阶段就抛异常，开发者能尽早发现。

如需支持自定义方法，可以参考 `LiteOrmLambdaHandlerInitializer` 中的注册方式，通过 `LambdaExprConverter.RegisterMethodHandler` 注册自定义处理器。

### 8.2 Lambda 与 Expr 如何互转？

- **Lambda → Expr**：Lambda 表达式在解析阶段会自动转换为 `Expr` 树。也可以通过 `Expr.Lambda<T>(u => ...)` 手动将 Lambda 转成 `LogicExpr`，便于与手写 Expr 组合使用。
- **Expr → Lambda**：在 Lambda 中可以通过 `ExprExtensions.To()` 方法嵌入已有的 `Expr` 对象，例如 `u => u.IsActive && extra.To<bool>()`，其中 `extra` 是外部构建的 `LogicExpr`。

详细用法见 [Lambda 与 Expr 组合使用](./lambda-expr-mixing.md)。

### 8.3 Lambda 性能是否有额外开销？

Lambda 查询在**解析阶段**会将表达式树转换为 `Expr` 对象，这个转换只发生一次（每次查询调用时）。一旦转换为 `Expr`，后续的 SQL 生成流程与手写 `Expr` 完全一致，**没有额外的运行时开销**。

也就是说，Lambda 的开销仅体现在表达式解析阶段，生成的 SQL 和执行路径与等价的手写 `Expr` 相同。在绝大多数业务场景中，解析开销可以忽略不计。

## 9. 相关链接

- [查询总览](./query-overview.md)
- [Expr 使用指南](./expr-guide.md)
- [ExprString 使用指南](./exprstring-guide.md)
- [增删改查](./crud-guide.md)
- [关联查询](./associations.md)
- [Lambda 与 Expr 组合使用](./lambda-expr-mixing.md)
- [CTE 指南](./cte-guide.md)
- [表达式扩展](../extensibility/expression-extension.md)
