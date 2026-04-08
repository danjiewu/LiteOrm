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
// 不推荐：查询所有字段
var users = await userService.SearchAsync();

// 推荐：使用 SearchAs 选择字段
var result = await userService.SearchAs<UserView>(
    Expr.From<UserView>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

### 3.1.1 来自 Demo 的投影思路

`LiteOrm.Demo\Demos\WindowFunctionDemo.cs` 中使用 `SearchAs<T>` 和投影来避免读取不必要的列：

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

### 4.1.1 来自 Demo 的批量初始化示例

`LiteOrm.Demo\Data\DbInitializer.cs` 中使用批量插入初始化多组数据：

```csharp
await deptService.BatchInsertAsync(depts);
await userService.BatchInsertAsync(users);
await salesService.BatchInsertAsync(records);
```

这个模式适合种子数据初始化、压测准备、演示数据构造等场景。相比循环逐条插入，它能显著减少数据库往返次数。

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

### 4.2.1 来自测试的批量增改删闭环

`LiteOrm.Tests\ServiceTests.cs` 对批量操作有一组很典型的闭环验证：

```csharp
await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

如果你的业务需要导入一批数据、批量修正后再清理，这种模式可直接套用。

### 4.3 `IBulkProvider` / `BulkProviderFactory`（高性能批量提供器）

`IBulkProvider` 配合 `BulkProviderFactory` 构成 LiteOrm 的高性能批量操作扩展（可选依赖），用于大规模插入/更新/删除时显著减少网络往返与数据库负载。

- 场景：导入大量数据、ETL、数据同步、冷数据回填。
- 特点：
  - 使用数据库原生批量接口或高效的多值插入语句。
  - 支持可配置的批次大小（BatchSize）、并发度（ParallelDegree）与事务边界（UseTransaction）。
  - 可与事务配合，支持失败回滚或部分成功策略。

示例：批量插入（伪代码）

```csharp
var factory = services.GetRequiredService<BulkProviderFactory>();
var provider = factory.GetProvider(dbConnection.GetType());
await provider.BulkInsertAsync(ToDataTable(users), dbConnection, transaction);
```

### 4.3.1 来自 Demo 的 MySQL `IBulkProvider` 实现

`LiteOrm.Demo\Demos\MySqlBulkInsertProvider.cs` 文件中提供了一个真实的 `IBulkProvider` 实现（类名为 `MySqlBulkCopyProvider`）：

```csharp
[AutoRegister(Key = typeof(MySqlConnection))]
public class MySqlBulkCopyProvider : IBulkProvider
{
    public async Task<int> BulkInsertAsync(
        DataTable dt,
        IDbConnection dbConnection,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        MySqlBulkCopy bulkCopy = new MySqlBulkCopy(
            dbConnection as MySqlConnection,
            transaction as MySqlTransaction);

        bulkCopy.DestinationTableName = dt.TableName;
        bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;

        for (int i = 0; i < dt.Columns.Count; i++)
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));

        return (await bulkCopy.WriteToServerAsync(dt).ConfigureAwait(false)).RowsInserted;
    }
}
```

这个例子说明了两点：

- `IBulkProvider` 可以按数据库连接类型自动注册，并通过 `BulkProviderFactory` 获取。
- 真正的高性能批量写入通常依赖数据库原生能力，而不是 ORM 层循环拼接 SQL。

在 LiteOrm 中的实现位置（参考）：

- 接口与工厂： LiteOrm.DbAccess.IBulkProvider、LiteOrm.DbAccess.BulkProviderFactory
- 默认/示例实现： LiteOrm.Demo.Demos.MySqlBulkCopyProvider（演示如何使用 MySqlBulkCopy）
- 使用点： LiteOrm.DAO.ObjectDAO 在执行批量插入时会调用 BulkProviderFactory 获取 provider 并调用 BulkInsert/BulkInsertAsync

示例：批量更新（按主键）

```csharp
// 将需要更新的数据转换为 DataTable，然后调用 provider.BulkInsert/BulkInsertAsync 或 provider 支持的 BulkUpdate
await provider.BulkInsertAsync(ToDataTable(usersToUpdate), dbConnection, transaction);
```

可配置选项（常见）：

- BatchSize: 单次提交的记录条数，建议根据数据库与网络吞吐量调整（例如 500-5000）。
- UseTransaction: 是否在单个事务中执行整个批量操作（注意大事务可能占用大量资源）。
- ParallelDegree: 并行分片数，适合分库或多连接环境。
- Upsert: 是否启用插入或更新逻辑，内部实现可根据数据库特性选择 MERGE/ON DUPLICATE KEY 等。

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
var sales = await salesService.SearchAsync<SalesRecordView>(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
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

### 7.2.1 来自测试的存在性判断示例

`LiteOrm.Tests\ServiceTests.cs` 中直接验证了 `ExistsAsync` 和 `CountAsync` 的差异化用途：

```csharp
bool exists = await viewService.ExistsAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"));
int count = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Age >= 50));
```

- 只关心“有没有”时用 `ExistsAsync`
- 需要精确数量时才用 `CountAsync`

## 8. 连接管理

### 8.1 使用 Scoped 生命周期

```csharp
// ASP.NET Core 中使用 Scoped
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;  // 推荐
});
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
// 大数据量查询
await foreach (var user in userViewDAO.Search(Expr.Prop("Age") >= 18))
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

LiteOrm 相比其他 ORM 的性能优势：

| 操作 | LiteOrm | EF Core | Dapper |
|------|---------|---------|--------|
| 插入 1000 条 | ~16ms | ~150ms | ~215ms |
| 更新 1000 条 | ~25ms | ~126ms | ~248ms |
| 关联查询 | ~9ms | ~15ms | ~9ms |

## 11. 下一步

- [返回目录](../README.md)
- [关联查询](../02-core-usage/05-associations.md)
- [事务处理](./01-transactions.md)
- [表达式扩展](../04-extensibility/01-expression-extension.md)

