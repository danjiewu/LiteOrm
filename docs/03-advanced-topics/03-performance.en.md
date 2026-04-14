# Performance Optimization

This guide covers performance optimization techniques for LiteOrm.

## 1. Connection Pool Configuration

### 1.1 Configuration Parameters

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

| Parameter | Default | Description |
|-----------|---------|-------------|
| `PoolSize` | 16 | Maximum cached connections in the pool |
| `MaxPoolSize` | 100 | Maximum concurrent connections |
| `KeepAliveDuration` | 00:10:00 | Connection keep-alive duration |

### 1.2 Appropriate Pool Sizing

- **Low concurrency**: PoolSize=5, MaxPoolSize=20
- **Medium concurrency**: PoolSize=16, MaxPoolSize=100
- **High concurrency**: PoolSize=50, MaxPoolSize=500

## 2. Parameterized Queries

LiteOrm uses parameterized queries by default, which prevents SQL injection and improves query plan cache hit rates.

### 2.1 Automatic Parameterization

```csharp
var minAge = 18;
var users = await userService.SearchAsync(u => u.Age >= minAge);
// Generated SQL: SELECT * FROM Users WHERE Age >= @0
```

### 2.2 String Interpolation Parameterization

```csharp
// Using interpolated strings, {name} will be parameterized
var name = "admin";
var users = await userViewDAO.Search($"WHERE UserName = {name}").ToListAsync();
```

## 3. Query Optimization

### 3.1 Only Query Required Fields

```csharp
// Not recommended: query all fields
var users = await userService.SearchAsync();

// Recommended: use SearchAs to select specific fields
var result = await userService.SearchAs<UserView>(
    Expr.From<UserView>()
        .Where(Expr.Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

### 3.1.1 Projection Pattern from Demo

`LiteOrm.Demo\Demos\WindowFunctionDemo.cs` uses `SearchAs<T>` with projection to avoid reading unnecessary columns:

```csharp
var results = await factory.SalesDAO
    .WithArgs([tableMonth])
    .SearchAs<SalesWindowView>(selectExpr)
    .ToListAsync();
```

This pattern is especially useful for reports, leaderboards, and aggregate views where the result model differs from the entity model.

### 3.2 Use Appropriate Result Types

| Scenario | Recommended Type | Reason |
|----------|-----------------|--------|
| Entity mapping | `ObjectViewDAO<T>` | Auto-maps to strongly-typed results |
| Large data processing | `DataViewDAO<T>` | Returns DataTable directly |
| Stream processing | `IAsyncEnumerable` | Low memory footprint |

### 3.3 Pagination Optimization

```csharp
// Large offset pagination (slow)
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(10000).Take(20)  // Slow with large offsets
);

// Recommended: ID-based cursor pagination (fast)
var lastId = 10000;
var page = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18 && u.Id > lastId)
          .OrderByDescending(u => u.Id)
          .Take(20)
);
```

## 4. Batch Operations

### 4.1 Batch Insert

```csharp
// Single insert (multiple network round trips)
for (int i = 0; i < 100; i++)
{
    await userService.InsertAsync(new User { UserName = $"user{i}", Age = 18 + i % 10, CreateTime = DateTime.Now });
}

// Batch insert (single network round trip)
await userService.BatchInsertAsync(users);  // Recommended
```

### 4.1.1 Batch Initialization Example from Demo

`LiteOrm.Demo\Data\DbInitializer.cs` uses batch inserts to initialize multiple groups of data:

```csharp
await deptService.BatchInsertAsync(depts);
await userService.BatchInsertAsync(users);
await salesService.BatchInsertAsync(records);
```

This pattern is ideal for seed data initialization, stress test preparation, and demo data generation. It significantly reduces database round trips compared to looping single inserts.

### 4.2 Batch Update

```csharp
// Single updates (multiple network round trips)
foreach (var user in users)
{
    await userService.UpdateAsync(user);
}

// Batch update (single network round trip)
await userService.BatchUpdateAsync(users);  // Recommended
```

### 4.2.1 Complete Batch Insert/Update/Delete Cycle from Tests

`LiteOrm.Tests\ServiceTests.cs` has a typical complete cycle validation for batch operations:

```csharp
await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

You can apply this pattern directly if your business requires importing a batch of data, making batch corrections, then cleaning up.

### 4.3 `IBulkProvider` / `BulkProviderFactory` (High-Performance Bulk Provider)

`IBulkProvider` combined with `BulkProviderFactory` provides LiteOrm's high-performance bulk operation extension (optional dependency). It significantly reduces network round trips and database load for large-scale insert/update/delete operations.

- **Use cases**: Data import, ETL, data sync, cold data backfill.
- **Features**:
  - Uses database-native bulk interfaces or efficient multi-value insert statements.
  - Supports configurable batch size (`BatchSize`), concurrency (`ParallelDegree`), and transaction boundaries (`UseTransaction`).
  - Works with transactions, supporting failure rollback or partial success strategies.

**Example: Bulk insert (pseudocode)**

```csharp
var factory = services.GetRequiredService<BulkProviderFactory>();
var provider = factory.GetProvider(dbConnection.GetType());
await provider.BulkInsertAsync(ToDataTable(users), dbConnection, transaction);
```

### 4.3.1 MySQL `IBulkProvider` Implementation from Demo

`LiteOrm.Demo\Demos\MySqlBulkInsertProvider.cs` provides a real `IBulkProvider` implementation (class `MySqlBulkCopyProvider`):

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

This example demonstrates two key points:

- `IBulkProvider` can be auto-registered by database connection type and retrieved via `BulkProviderFactory`.
- True high-performance bulk writing typically relies on database-native capabilities rather than ORM-level loop-generated SQL.

**Implementation locations in LiteOrm (reference)**:

- Interface and factory: `LiteOrm.DbAccess.IBulkProvider`, `LiteOrm.DbAccess.BulkProviderFactory`
- Default/sample implementation: `LiteOrm.Demo.Demos.MySqlBulkCopyProvider` (demonstrates how to use MySqlBulkCopy)
- Usage point: `LiteOrm.DAO.ObjectDAO` calls `BulkProviderFactory` to get a provider when executing batch inserts

**Example: Bulk update (by primary key)**

```csharp
// Convert data to update into DataTable, then call provider's BulkInsert/BulkInsertAsync or provider-supported BulkUpdate
await provider.BulkInsertAsync(ToDataTable(usersToUpdate), dbConnection, transaction);
```

**Configurable options (common)**:

- `BatchSize`: Records per commit, adjust based on database and network throughput (e.g., 500-5000).
- `UseTransaction`: Whether to execute the entire batch in a single transaction (note: large transactions may consume significant resources).
- `ParallelDegree`: Parallel partitioning count, suitable for sharded databases or multi-connection environments.
- `Upsert`: Whether to enable insert-or-update logic; internal implementation can choose MERGE/ON DUPLICATE KEY based on database features.

**Caveats**:

- When using `IBulkProvider`, evaluate index load, log growth, and lock waits in a test environment. For write-intensive scenarios, consider disabling non-essential indexes during import or deferring index rebuilds.
- `IBulkProvider` implementations vary by database: SQL Server typically uses `SqlBulkCopy`, MySQL can use `LOAD DATA INFILE` or `MySqlBulkCopy`. See sample implementations in LiteOrm.Demo.

## 5. Async Programming

### 5.1 Use Async Methods

```csharp
// Synchronous (blocks thread)
var users = userService.Search();

// Async (releases thread)
var users = await userService.SearchAsync();  // Recommended
```

### 5.2 Parallel Queries

```csharp
// Serial queries
var users = await userService.SearchAsync();
var departments = await departmentService.SearchAsync();

// Parallel queries
var userTask = userService.SearchAsync();
var departmentTask = departmentService.SearchAsync();
await Task.WhenAll(userTask, departmentTask);
var users = userTask.Result;
var departments = departmentTask.Result;
```

### 5.3 When to Use Parallelism

- Two queries are independent and don't share a transaction context that must be serial.
- Dashboard aggregation panels, statistics, multiple independent lists loading simultaneously.
- Don't mindlessly parallelize strongly related small queries. If you can solve it with one join query, prioritize reducing database round trips.

## 6. Index Optimization

Ensure query condition fields have appropriate indexes:

```sql
-- Query condition
WHERE DeptId = 2 AND Age >= 18

-- Recommended index
CREATE INDEX idx_users_dept_age ON Users(DeptId, Age);
```

## 7. Avoiding N+1 Queries

### 7.1 Use JOIN Queries

```csharp
// N+1 query (not recommended)
var sales = await salesService.SearchAsync(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
foreach (var sale in sales)
{
    var user = await userService.GetObjectAsync(sale.SalesUserId);  // Query each time
}

// JOIN query (recommended)
var sales = await salesService.SearchAsync<SalesRecordView>(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
// Automatic JOIN, single query
```

### 7.2 Use EXISTS Instead of COUNT

```csharp
// Inefficient
int count = await userService.CountAsync(u => u.Age >= 18);
if (count > 0) { ... }

// Efficient
bool exists = await userService.ExistsAsync(u => u.Age >= 18);
if (exists) { ... }
```

### 7.2.1 Existence Check Examples from Tests

`LiteOrm.Tests\ServiceTests.cs` directly validates the different purposes of `ExistsAsync` and `CountAsync`:

```csharp
bool exists = await viewService.ExistsAsync(Expr.Lambda<TestUser>(u => u.Name == "Unique"));
int count = await viewService.CountAsync(Expr.Lambda<TestUser>(u => u.Age >= 50));
```

- Use `ExistsAsync` when you only care about "whether any exist"
- Only use `CountAsync` when you need the exact count

## 8. Connection Management

### 8.1 Use Scoped Lifecycle

```csharp
// In ASP.NET Core, use Scoped
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;  // Recommended
});
```

### 8.2 Release Connections Promptly

```csharp
var sessionManager = SessionManager.Current;
sessionManager.BeginTransaction();
try
{
    // Operations
    sessionManager.Commit();
}
catch
{
    sessionManager.Rollback();
    throw;
}
```

## 9. Memory Optimization

### 9.1 Use Streams for Large Data

```csharp
// Large data query
await foreach (var user in userViewDAO.Search(Expr.Prop("Age") >= 18))
{
    // Stream processing, avoid loading all into memory at once
    Process(user);
}
```

### 9.1.1 Usage Recommendations

- Ideal for log export, report traversal, and background batch processing.
- If you only need a few pages of results, you don't need stream processing.
- Keep individual record processing logic lightweight during stream processing to avoid holding connections for extended periods.

### 9.2 Avoid Large Objects

```csharp
// Not recommended: storing large text
[Column("Content")]
public string LargeContent { get; set; }  // Could be very large

// Recommended: store reference
[Column("ContentId")]
public long ContentId { get; set; }  // Foreign key reference
```

## 10. Performance Benchmarks

LiteOrm's performance advantages compared to other ORMs:

| Operation | LiteOrm | EF Core | Dapper |
|-----------|---------|---------|--------|
| Insert 1000 rows | ~16ms | ~150ms | ~215ms |
| Update 1000 rows | ~25ms | ~126ms | ~248ms |
| JOIN query | ~9ms | ~15ms | ~9ms |

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/05-associations.en.md)
- [Transactions](./01-transactions.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)
