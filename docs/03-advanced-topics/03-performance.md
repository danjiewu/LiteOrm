# 性能优化

本文介绍 LiteOrm 的性能优化技巧。

## 1. 连接池配置

### 1.1 配置参数

```json
{
  "LiteOrm": {
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=TestDb;...",
        "PoolSize": 16,
        "MaxPoolSize": 100,
        "KeepAliveDuration": "00:10:00"
      }
    ]
  }
}
```

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `PoolSize` | 16 | 连接池缓存的最大连接数 |
| `MaxPoolSize` | 100 | 最大并发连接数 |
| `KeepAliveDuration` | 00:10:00 | 连接保活时长 |

### 1.2 合理设置池大小

- **小并发**：PoolSize=5, MaxPoolSize=20
- **中等并发**：PoolSize=16, MaxPoolSize=100
- **大并发**：PoolSize=50, MaxPoolSize=500

## 2. 参数化查询

LiteOrm 默认使用参数化查询，防止 SQL 注入的同时提高查询计划缓存命中率。

### 2.1 自动参数化

```csharp
var minAge = 18;
var users = await userService.SearchAsync(u => u.Age >= minAge);
// 生成 SQL: SELECT * FROM Users WHERE Age >= @0
```

### 2.2 字符串拼接参数化

```csharp
// 使用插值字符串，{name} 会被参数化传入
var name = "admin";
var users = await userViewDAO.Search($"WHERE UserName = {name}").ToListAsync();
```

## 3. 查询优化

### 3.1 只查询需要的字段

```csharp
using static LiteOrm.Common.Expr;
// 不推荐：查询所有字段
var users = await userService.SearchAsync();

// 推荐：使用 SearchAs 选择字段
var result = await userService.SearchAs<UserView>(
    From<UserView>()
        .Where(Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

### 3.1.1 投影思路

使用 `SearchAs<T>` 和投影来避免读取不必要的列：

```csharp
var results = await factory.SalesDAO
    .WithArgs([tableMonth])
    .SearchAs<SalesWindowView>(selectExpr)
    .ToListAsync();
```

这种模式特别适合报表、排行榜、聚合视图等“结果模型与实体模型不同”的查询。

### 3.2 使用合适的结果类型

| 场景 | 推荐类型 | 原因 |
|------|----------|------|
| 实体映射 | `ObjectViewDAO<T>` | 自动映射到强类型 |
| 大数据量处理 | `DataViewDAO<T>` | 直接返回 DataTable |
| 流式处理 | `IAsyncEnumerable` | 内存占用低 |

### 3.3 分页优化

```csharp
// 大偏移量分页（慢）
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10000).Take(20)  // 偏移量大时慢
);

// 推荐：基于 ID 的游标分页（快）
var lastId = 10000;
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18 && u.Id > lastId)
          .OrderByDescending(u => u.Id)
          .Take(20)
);
```

## 4. 批量操作

### 4.1 批量插入

```csharp
// 单条插入（多次网络往返）
for (int i = 0; i < 100; i++)
{
    await userService.InsertAsync(new User { UserName = $"user{i}", Age = 18 + i % 10, CreateTime = DateTime.Now });
}

// 批量插入（一次网络往返）
await userService.BatchInsertAsync(users);  // 推荐
```

批量初始化的多 service 并行写法已收录于 [CRUD 指南 §批量初始化示例](../02-core-usage/03-crud-guide.md)，这里不再重复。

### 4.2 批量更新

```csharp
// 单条更新（多次网络往返）
foreach (var user in users)
{
    await userService.UpdateAsync(user);
}

// 批量更新（一次网络往返）
await userService.BatchUpdateAsync(users);  // 推荐
```

### 4.2.1 批量增改删闭环

对批量操作的一组典型闭环验证：

```csharp
using static LiteOrm.Common.Expr;
await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

如果你的业务需要导入一批数据、批量修正后再清理，这种模式可直接套用。

### 4.3 `IBulkProvider`（高性能批量提供器）

`IBulkProvider` 是 LiteOrm 的高性能批量操作扩展接口（可选依赖），用于大规模插入时显著减少网络往返与数据库负载。

- 场景：导入大量数据、ETL、数据同步、冷数据回填。
- 特点：
  - 使用数据库原生批量接口（如 `SqlBulkCopy`、`MySqlBulkCopy`）。
  - 未设置 provider 时，`BatchInsert`/`BatchInsertAsync` 自动回退到多值 INSERT 或逐条插入。

使用方式：实现 `IBulkProvider` 后，直接设置到对应的 `SqlBuilder.BulkProvider` 属性即可：

```csharp
var provider = new MySqlBulkCopyProvider();
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = provider;
```

`ObjectDAO.BatchInsert` / `BatchInsertAsync` 在执行批量插入时读取 `SqlBuilder.BulkProvider`，获取到 provider 后调用其 `BulkInsert` / `BulkInsertAsync`。

### 4.3.1 MySQL `IBulkProvider` 实现示例

下面是一个简化自 `LiteOrm.Demo.Demos.MySqlBulkCopyProvider` 的 `IBulkProvider` 实现示例：

```csharp
public class MySqlBulkCopyProvider : IBulkProvider
{
    public async Task<int> BulkInsertAsync(
        DataTable dt,
        IDbConnection dbConnection,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        MySqlBulkCopy bulkCopy = CreateBulkCopy(dbConnection, transaction, dt.TableName);

        for (int i = 0; i < dt.Columns.Count; i++)
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));

        return (await bulkCopy.WriteToServerAsync(dt).ConfigureAwait(false)).RowsInserted;
    }

    public int BulkInsert(
        DataTable dt,
        IDbConnection dbConnection,
        IDbTransaction transaction)
    {
        MySqlBulkCopy bulkCopy = CreateBulkCopy(dbConnection, transaction, dt.TableName);

        for (int i = 0; i < dt.Columns.Count; i++)
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));

        return bulkCopy.WriteToServer(dt).RowsInserted;
    }

    private static MySqlBulkCopy CreateBulkCopy(
        IDbConnection dbConnection, IDbTransaction transaction, string tableName)
    {
        var copy = new MySqlBulkCopy(
            dbConnection as MySqlConnection,
            transaction as MySqlTransaction);
        copy.DestinationTableName = tableName;
        copy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
        return copy;
    }
}

// 启用：直接设置到 SqlBuilder 的 BulkProvider 属性，无需任何自动注册
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = new MySqlBulkCopyProvider();
```

这个例子说明了两点：

- `IBulkProvider` 只需实现接口并赋值给 `SqlBuilder.BulkProvider` 即可生效，基础库不做自动注册。
- 真正的高性能批量写入通常依赖数据库原生能力，而不是 ORM 层循环拼接 SQL。

在 LiteOrm 中的实现位置（参考）：

- 接口： LiteOrm.IBulkProvider
- 示例实现： LiteOrm.Demo.Demos.MySqlBulkCopyProvider（演示如何使用 MySqlBulkCopy）
- 使用点： LiteOrm.ObjectDAO 在执行批量插入时读取 `SqlBuilder.BulkProvider` 并调用 `BulkInsert`/`BulkInsertAsync`

示例：批量更新（按主键）

```csharp
// 获取当前数据源对应的 SqlBuilder，并确保其 BulkProvider 已设置
var provider = SqlBuilderFactory.Instance.GetSqlBuilder(dbConnection.GetType()).BulkProvider;
// 将需要更新的数据转换为 DataTable，然后调用 provider.BulkInsert/BulkInsertAsync
await provider.BulkInsertAsync(ToDataTable(usersToUpdate), dbConnection, transaction);
```

`IBulkProvider` 只暴露 `BulkInsert` / `BulkInsertAsync` 两类方法；批量大小、事务、并发度等均由调用方自行控制（如分批构造 `DataTable`、自行管理 `IDbTransaction`）。

注意事项：

- 在使用 `IBulkProvider` 时，务必在测试环境评估索引负载、日志增长与锁等待；对于写密集型场景，考虑在导入期间禁用辅助索引或延迟索引重建。
- `IBulkProvider` 的实现会因数据库不同而不同：例如 SQL Server 常使用 `SqlBulkCopy`，MySQL 可使用 `LOAD DATA INFILE` 或 `MySqlBulkCopy`。请参考 LiteOrm.Demo 中的示例实现。

## 5. 异步编程

### 5.1 使用异步方法

```csharp
// 同步（阻塞线程）
var users = userService.Search();

// 异步（释放线程）
var users = await userService.SearchAsync();  // 推荐
```

### 5.2 并行查询

```csharp
// 串行查询
var users = await userService.SearchAsync();
var departments = await departmentService.SearchAsync();

// 并行查询
var userTask = userService.SearchAsync();
var departmentTask = departmentService.SearchAsync();
await Task.WhenAll(userTask, departmentTask);
var users = userTask.Result;
var departments = departmentTask.Result;
```

### 5.3 什么时候适合并行

- 两个查询互不依赖，并且不会共享同一个必须串行访问的事务上下文时。
- 首页聚合面板、仪表盘统计、多个独立列表同时加载时。
- 不要把强关联的小查询无脑并行化；如果能通过一个关联查询解决，优先减少数据库往返。

## 6. 索引优化

确保查询条件字段有适当索引：

```sql
-- 查询条件
WHERE DeptId = 2 AND Age >= 18

-- 建议索引
CREATE INDEX idx_users_dept_age ON Users(DeptId, Age);
```

## 7. 避免 N+1 查询

### 7.1 使用关联查询

```csharp
// N+1 查询（不推荐）
var sales = await salesService.SearchAsync(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
foreach (var sale in sales)
{
    var user = await userService.GetObjectAsync(sale.SalesUserId);  // 每次查询
}

// 关联查询（推荐）
var sales = await salesService.SearchAsAsync<SalesRecordView>(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
// 自动 JOIN，一次查询
```

### 7.2 使用 EXISTS 代替 COUNT

```csharp
// 低效
int count = await userService.CountAsync(u => u.Age >= 18);
if (count > 0) { ... }

// 高效
bool exists = await userService.ExistsAsync(u => u.Age >= 18);
if (exists) { ... }
```

### 7.2.1 存在性判断示例

直接对比 `ExistsAsync` 和 `CountAsync` 的差异化用途：

```csharp
using static LiteOrm.Common.Expr;
bool exists = await viewService.ExistsAsync(Lambda<TestUser>(u => u.Name == "Unique"));
int count = await viewService.CountAsync(Lambda<TestUser>(u => u.Age >= 50));
```

- 只关心“有没有”时用 `ExistsAsync`
- 需要精确数量时才用 `CountAsync`

## 8. 连接管理

### 8.1 使用 Scoped 生命周期

```csharp
// ASP.NET Core 中使用 Scoped（作用域跟踪默认自动启用）
builder.Host.RegisterLiteOrm();
```

### 8.2 及时释放连接

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try
{
    // 操作
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

## 9. 内存优化

### 9.1 使用 Stream 处理大数据

```csharp
using static LiteOrm.Common.Expr;
// 大数据量查询
await foreach (var user in userViewDAO.Search(Prop("Age") >= 18))
{
    // 流式处理，避免一次性加载到内存
    Process(user);
}
```

### 9.1.1 使用建议

- 适合日志导出、报表遍历、后台批处理。
- 如果你只是为了拿几十条分页结果，不必使用流式遍历。
- 流式处理时尽量把单条记录的处理逻辑做轻，避免拖长连接占用时间。

### 9.2 避免大对象

```csharp
// 不推荐：存储大文本
[Column("Content")]
public string LargeContent { get; set; }  // 可能很大

// 推荐：存储引用
[Column("ContentId")]
public long ContentId { get; set; }  // 外键引用
```

## 10. 性能基准

LiteOrm 相比其他 ORM 的性能优势，基于 `LiteOrm.Benchmark` 项目跑出的 BenchmarkDotNet 实测数据。下表所有数值为各基准维度上的 `Mean`；**批量耗时/批量内存为单次基准运行的数值**，**单条耗时/内存为循环 1000 次的累计值**（单次单条 ≈ 累计值 ÷ 1000）。加粗为该行最优。

### 10.1 批量耗时（µs，越低越好）

| 操作 | 数据量 | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|------|------:|--------:|-------:|--------:|---------:|-------:|
| Insert | 10 | 3,444 | **3,317** | 3,478 | 3,862 | 5,765 |
| Insert | 100 | **4,663** | 5,223 | 6,357 | 6,085 | 21,948 |
| Insert | 1000 | **22,698** | 30,853 | 38,759 | 35,348 | 207,915 |
| Insert | 10000 | **179,155** | 278,958 | 328,979 | 344,190 | 1,826,182 |
| Update | 10 | **3,699** | 3,811 | 4,189 | 4,474 | 6,002 |
| Update | 100 | **6,056** | 6,403 | 8,481 | 8,673 | 18,696 |
| Update | 1000 | **32,539** | 41,996 | 65,513 | 64,261 | 176,164 |
| Update | 10000 | **278,754** | 366,908 | 612,913 | 606,954 | 1,366,445 |
| Upsert | 10 | 4,264 | **3,759** | 4,470 | 5,520 | 6,294 |
| Upsert | 100 | 6,792 | **5,999** | 6,584 | 10,201 | 19,688 |
| Upsert | 1000 | **27,978** | 34,842 | 31,851 | 118,620 | 206,438 |
| Upsert | 10000 | **248,066** | 322,097 | 270,599 | 6,708,137 | 1,487,294 |
| JoinQuery | 10 | **519** | 530 | 658 | 769 | 1,068 |
| JoinQuery | 100 | **1,348** | 1,530 | 1,833 | 2,279 | 2,970 |
| JoinQuery | 1000 | **8,372** | 9,706 | 10,059 | 21,088 | 16,243 |
| JoinQuery | 10000 | **82,573** | 102,093 | 102,162 | 174,890 | 158,733 |

### 10.2 批量内存（KB，越低越好）

| 操作 | 数据量 | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|------|------:|--------:|-------:|--------:|---------:|-------:|
| Insert | 10 | **83** | 62 | 124 | 105 | 466 |
| Insert | 100 | **178** | 550 | 1,163 | 898 | 3,261 |
| Insert | 1000 | **1,679** | 5,372 | 11,456 | 8,834 | 31,348 |
| Insert | 10000 | **16,695** | 55,104 | 115,232 | 89,300 | 316,635 |
| Update | 10 | 102 | **81** | 180 | 214 | 399 |
| Update | 100 | **254** | 676 | 1,636 | 1,545 | 2,426 |
| Update | 1000 | **2,298** | 6,409 | 15,176 | 14,918 | 23,690 |
| Update | 10000 | **22,908** | 65,550 | 154,497 | 150,935 | 232,157 |
| Upsert | 10 | 128 | **76** | 86 | 301 | 430 |
| Upsert | 100 | 760 | 626 | **535** | 2,077 | 2,707 |
| Upsert | 1000 | **2,321** | 5,916 | 4,576 | 41,943 | 27,034 |
| Upsert | 10000 | **23,026** | 60,560 | 45,672 | 3,413,708 | 257,766 |
| JoinQuery | 10 | 30 | **19** | 46 | 170 | 239 |
| JoinQuery | 100 | **48** | 78 | 161 | 991 | 1,073 |
| JoinQuery | 1000 | **225** | 672 | 1,327 | 9,221 | 9,460 |
| JoinQuery | 10000 | **2,100** | 6,785 | 13,140 | 91,661 | 94,250 |

### 10.3 批量 GC（G0 / G1 / G2 千次合计）

> `0` 表示该代未触发 GC；小数是按基准运行 / 数据量分摊后的均值。

| 操作 | 数据量 | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|------|------:|--------:|-------:|--------:|---------:|-------:|
| Insert | 10 | 0 / 0 / 0 | 0 / 0 / 0 | 7.8 / 0 / 0 | 7.8 / 0 / 0 | 31 / 16 / 0 |
| Insert | 100 | 7.8 / 0 / 0 | 31 / 0 / 0 | 94 / 16 / 0 | 62 / 16 / 0 | 250 / 167 / 0 |
| Insert | 1000 | 125 / 31 / 0 | 656 / 625 / 375 | 857 / 429 / 0 | 778 / 444 / 111 | 2,000 / 1,000 / 0 |
| Insert | 10000 | **1,333 / 0 / 0** | 3,667 / 3,333 / 1,000 | 9,000 / 4,000 / 0 | 7,000 / 4,000 / 1,000 | 26,000 / 8,000 / 1,000 |
| Update | 10 | 7.8 / 0 / 0 | **0 / 0 / 0** | 7.8 / 0 / 0 | 16 / 0 / 0 | 31 / 16 / 0 |
| Update | 100 | 16 / 0 / 0 | **47 / 0 / 0** | 125 / 47 / 0 | 125 / 47 / 0 | 154 / 77 / 0 |
| Update | 1000 | 188 / 94 / 0 | 625 / 500 / 250 | 1,167 / 1,000 / 167 | 1,167 / 500 / 167 | 1,000 / 0 / 0 |
| Update | 10000 | **1,500 / 500 / 0** | 4,000 / 3,000 / 1,000 | 12,000 / 4,000 / 1,000 | 11,000 / 6,000 / 1,000 | 19,000 / 7,000 / 1,000 |
| Upsert | 10 | **0 / 0 / 0** | **0 / 0 / 0** | **0 / 0 / 0** | 23 / 0 / 0 | 31 / 16 / 0 |
| Upsert | 100 | 47 / 0 / 0 | 47 / 16 / 0 | **31 / 0 / 0** | 156 / 62 / 0 | 167 / 83 / 0 |
| Upsert | 1000 | 167 / 83 / 0 | 556 / 444 / 222 | 500 / 344 / 188 | 3,000 / 1,000 / 0 | 2,000 / 1,000 / 0 |
| Upsert | 10000 | **1,500 / 500 / 0** | 4,000 / 3,000 / 1,000 | 3,500 / 1,000 / 500 | 277,000 / 5,000 / 1,000 | 21,000 / 7,000 / 1,000 |
| JoinQuery | 10 | 1.95 / 0.98 / 0 | **0.98 / 0 / 0** | 2.93 / 0.98 / 0 | 14 / 0 / 0 | 20 / 8 / 0 |
| JoinQuery | 100 | **0 / 0 / 0** | 3.9 / 0 / 0 | 12 / 4 / 0 | 78 / 0 / 0 | 78 / 16 / 0 |
| JoinQuery | 1000 | **16 / 0 / 0** | 47 / 16 / 0 | 94 / 31 / 0 | 700 / 200 / 0 | 750 / 375 / 0 |
| JoinQuery | 10000 | **143 / 0 / 0** | 667 / 500 / 167 | 1,200 / 600 / 200 | 7,333 / 667 / 0 | 7,000 / 2,000 / 0 |

### 10.4 单条操作（循环 1000 次的累计值，越低越好）

> 实际单次单条 ≈ 下表数值 ÷ 1000。

| 维度 | 操作 | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|------|------|--------:|-------:|--------:|---------:|-------:|
| 耗时 (ms) | Insert | 3,640.6 | **3,618.0** | 3,932.1 | 3,954.6 | 4,446.5 |
| 耗时 (ms) | Update | 3,655.9 | **3,637.6** | 4,052.4 | 4,077.6 | 4,188.7 |
| 耗时 (ms) | Upsert | 3,829.1 | **3,682.8** | 4,398.1 | 4,663.4 | 4,550.6 |
| 耗时 (ms) | Get | 240.3 | **234.9** | 625.4 | 691.7 | 718.8 |
| 内存 (MB) | Insert | **4.30** | 5.43 | 20.22 | 25.27 | 58.57 |
| 内存 (MB) | Update | 4.54 | **4.28** | 22.67 | 30.45 | 46.20 |
| 内存 (MB) | Upsert | 6.60 | **6.07** | 20.78 | 87.75 | 55.01 |
| 内存 (MB) | Get | **3.67** | 6.08 | 18.52 | 50.76 | 47.58 |
| GC (G0 / G1) | Insert | **0 / 0** | **0 / 0** | 1,000 / 0 | 2,000 / 0 | 4,000 / 3,000 |
| GC (G0 / G1) | Update | **0 / 0** | **0 / 0** | 1,000 / 0 | 2,000 / 0 | 3,000 / 1,000 |
| GC (G0 / G1) | Upsert | **0 / 0** | **0 / 0** | 1,000 / 0 | 7,000 / 0 | 4,000 / 1,000 |
| GC (G0 / G1) | Get | **0 / 0** | 500 / 0 | 1,000 / 0 | 4,000 / 0 | 3,000 / 1,000 |

### 10.5 综合得分（各维度几何平均，越低越好）

把每个 ORM 在**批量**（4 操作 × 4 数据量）与**单条**（4 操作）下的 **耗时 / 内存 / GC** 共 6 个维度，逐格换算为「相对该格最优值的倍数」，再取**几何平均**得到各维度得分。1.00× = 该维度全场最优。

| ORM | 批量耗时 | 批量内存 | 批量 GC | 单条耗时 | 单条内存 | 单条 GC | 综合 |
|-----|--------:|--------:|------:|-------:|-------:|------:|----:|
| LiteOrm | **1.02×** | **1.12×** | **1.15×** | 1.02× | **1.04×** | **1.00×** | **1.06×** |
| Dapper | 1.17× | 2.03× | 2.97× | **1.00×** | 1.20× | 1.39× | 1.51× |
| FreeSql | 1.36× | 3.51× | 4.55× | 1.40× | 4.55× | 4.00× | 2.86× |
| SqlSugar | 2.09× | 9.12× | 10.04× | 1.46× | 9.56× | 4.51× | 4.79× |
| EFCore | 3.46× | 12.74× | 12.03× | 1.52× | 11.46× | 4.69× | 5.93× |

### 10.6 测试环境

| 配置项 | 值 |
|--------|-----|
| 测试框架 | BenchmarkDotNet v0.15.8 |
| .NET 版本 | .NET SDK 10.0.11 (net10.0) |
| 运行时 | X64 RyuJIT x86-64-v3 |
| 操作系统 | Linux Ubuntu 24.04 |
| CPU | Intel Xeon Silver 4314（16 核 / 32 线程） |
| 数据库 | MySQL（默认；可通过 `appsettings.json` 切换为 SQLite 或 Oracle） |
| 测试数据量 | BatchCount: 10 / 100 / 1000 / 10000 条 |
| 基准模式 | `[MemoryDiagnoser]` + `[MediumRunJob]`（Job=MediumRun，IterationCount=15，LaunchCount=2，WarmupCount=10） |
| 对比对象 | LiteOrm、Dapper、FreeSql、SqlSugar、EFCore |

> 完整的 BenchmarkDotNet 报告位于 `LiteOrm.Benchmark/BenchmarkDotNet.Artifacts/results/` 目录下。可视化分析页：https://aad8b2de3fba877d3.app.workbuddy.link/

### 10.7 几个值得点出的事实

- `Insert@10000` LiteOrm 用时 `179,155µs`，相比 EFCore 的 `1,826,182µs` 快 **10.2×**；相比 Dapper 的 `278,958µs` 快 **1.56×**。同场景下 LiteOrm 内存 `16,695KB`，EFCore `316,635KB`，省 **19.0×**。GC 方面 LiteOrm `1,333 / 0 / 0`（G0/G1/G2），EFCore `26,000 / 8,000 / 1,000` —— 触发次数不到 EFCore 的二十分之一。
- `Upsert@10000` SqlSugar 用时 `6,708,137µs`、内存 `3,413,708KB`、GC `277,000 / 5,000 / 1,000`，三项均严重退化——耗时是 LiteOrm 的 **27.0×**、内存是 **148×**、Gen0 GC 是 **185×**。
- `JoinQuery@10000` EFCore 用时 `158,733µs`，已被 SqlSugar 的 `174,890µs` 反超，但 EFCore 内存 `94,250KB` 也被 SqlSugar 的 `91,661KB` 反超得不多——大结果集场景下 EF Core 并不天然占优。
- 单条 `Get` LiteOrm `240.3ms`（累计）与 Dapper `234.9ms` 几乎打平；LiteOrm 内存 `3.67MB` 反超 Dapper 的 `6.08MB`，GC `0 / 0` 也优于 Dapper 的 `500 / 0` —— 这是 LiteOrm 在单条场景里对 Dapper 唯一能在两个维度同时领先的格子。
- 单条 Insert/Update/Upsert LiteOrm 累计耗时在 3,640~3,830ms 量级，Dapper 略快 0.3%~3.9%，差距远小于批量场景；这是因为单条场景下"每次往返固定成本"主导了所有 ORM，单批 1 万条时 LiteOrm 累计耗时 `22,698ms` vs Dapper `30,853ms`，LiteOrm 反过来快 26%。

## 相关链接

- [返回目录](../README.md)
- [关联查询](../02-core-usage/08-associations.md)
- [事务处理](../06-di/01-transactions.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)


