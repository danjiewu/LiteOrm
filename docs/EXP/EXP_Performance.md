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
// 生成 SQL: SELECT * FROM Users WHERE Age >= @p0
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
    Expr.From<User>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "Email")
);
```

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
    q => q.Where(u => u.Status == 1)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10000).Take(20)  // 偏移量大时慢
);

// 推荐：基于 ID 的游标分页（快）
var lastId = 10000;
var page = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1 && u.Id > lastId)
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
    await userService.InsertAsync(new User { Name = $"user{i}" });
}

// 批量插入（一次网络往返）
await userService.BatchInsertAsync(users);  // 推荐
```

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

### 4.3 BulkProvider（高性能批量提供器）

BulkProvider 是 LiteOrm 提供的高性能批量操作扩展（可选依赖），用于大规模插入/更新/删除时显著减少网络往返与数据库负载。

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

- 在使用 BulkProvider 时，务必在测试环境评估索引负载、日志增长与锁等待；对于写密集型场景，考虑在导入期间禁用辅助索引或延迟索引重建。
- BulkProvider 实现会因数据库不同而不同：例如 SQL Server 常使用 SqlBulkCopy，MySQL 可使用 LOAD DATA INFILE 或 MySqlBulkCopy。请参考 LiteOrm.Demo 中的示例实现。

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
var orders = await orderService.SearchAsync();

// 并行查询
var userTask = userService.SearchAsync();
var orderTask = orderService.SearchAsync();
await Task.WhenAll(userTask, orderTask);
var users = userTask.Result;
var orders = orderTask.Result;
```

## 6. 索引优化

确保查询条件字段有适当索引：

```sql
-- 查询条件
WHERE Status = 1 AND Age >= 18

-- 建议索引
CREATE INDEX idx_status_age ON Users(Status, Age);
```

## 7. 避免 N+1 查询

### 7.1 使用关联查询

```csharp
// N+1 查询（不推荐）
var orders = await orderService.SearchAsync();
foreach (var order in orders)
{
    var user = await userService.GetObjectAsync(order.UserId);  // 每次查询
}

// 关联查询（推荐）
var orders = await orderService.SearchAsync<OrderView>();
// 自动 JOIN，一次查询
```

### 7.2 使用 EXISTS 代替 COUNT

```csharp
// 低效
int count = await userService.CountAsync(u => u.Status == 1);
if (count > 0) { ... }

// 高效
bool exists = await userService.ExistsAsync(u => u.Status == 1);
if (exists) { ... }
```

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
// 使用 using 确保释放
using (var session = SessionManager.Current.BeginTransaction())
{
    // 操作
    session.Commit();
}  // 自动释放
```

## 9. 内存优化

### 9.1 使用 Stream 处理大数据

```csharp
// 大数据量查询
await foreach (var user in userViewDAO.Search(Expr.Prop("Status") == 1))
{
    // 流式处理，避免一次性加载到内存
    Process(user);
}
```

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

- 关联查询：[关联查询](../05_Associations.md)
- 事务处理：[事务处理](./EXP_Transaction.md)
- 表达式扩展：[表达式扩展](./EXP_ExpressionExtension.md)
