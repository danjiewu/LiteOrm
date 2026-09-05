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
using static LiteOrm.Common.Expr;
// Not recommended: query all fields
var users = await userService.SearchAsync();

// Recommended: use SearchAs to select specific fields
var result = await userService.SearchAs<UserView>(
    From<UserView>()
        .Where(Prop("Age") > 18)
        .Select("Id", "UserName", "DeptName")
);
```

### 3.1.1 Projection Pattern

Use `SearchAs<T>` with projection to avoid reading unnecessary columns:

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

The multi-service parallel batch-initialization pattern is captured in [CRUD guide § Batch initialization example](../02-core-usage/03-crud-guide.en.md) — not repeated here.

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

### 4.2.1 Complete Batch Insert/Update/Delete Cycle

A typical complete cycle validation for batch operations:

```csharp
using static LiteOrm.Common.Expr;
await service.BatchInsertAsync(users);

var inserted = await viewService.SearchAsync(Lambda<TestUser>(u => u.Name!.StartsWith("Batch")));
foreach (var user in inserted)
    user.Age += 5;

await service.BatchUpdateAsync(inserted);
await service.BatchDeleteAsync(inserted);
```

You can apply this pattern directly if your business requires importing a batch of data, making batch corrections, then cleaning up.

### 4.3 `IBulkProvider` (High-Performance Bulk Provider)

`IBulkProvider` is LiteOrm's high-performance bulk operation extension interface (optional dependency), significantly reducing network round trips and database load for large-scale inserts.

- **Use cases**: Data import, ETL, data sync, cold data backfill.
- **Features**:
  - Uses database-native bulk interfaces (e.g., `SqlBulkCopy`, `MySqlBulkCopy`).
  - When no provider is set, `BatchInsert`/`BatchInsertAsync` automatically falls back to multi-value INSERT or row-by-row inserts.

**How to use**: implement `IBulkProvider` and assign it directly to the `BulkProvider` property of the matching `SqlBuilder`:

```csharp
var provider = new MySqlBulkCopyProvider();
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = provider;
```

`ObjectDAO.BatchInsert` / `BatchInsertAsync` reads `SqlBuilder.BulkProvider` when executing batch inserts and calls the provider's `BulkInsert` / `BulkInsertAsync`.

### 4.3.1 MySQL `IBulkProvider` Implementation Example

Below is a simplified `IBulkProvider` example adapted from `LiteOrm.Demo.Demos.MySqlBulkCopyProvider`:

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

// Enable it: assign directly to SqlBuilder.BulkProvider, no auto-registration needed
SqlBuilderFactory.Instance.GetSqlBuilder(typeof(MySqlConnection)).BulkProvider = new MySqlBulkCopyProvider();
```

This example demonstrates two key points:

- `IBulkProvider` takes effect simply by implementing the interface and assigning it to `SqlBuilder.BulkProvider`; the base library does no auto-registration.
- True high-performance bulk writing typically relies on database-native capabilities rather than ORM-level loop-generated SQL.

**Implementation locations in LiteOrm (reference)**:

- Interface: `LiteOrm.IBulkProvider`
- Sample implementation: `LiteOrm.Demo.Demos.MySqlBulkCopyProvider` (demonstrates how to use MySqlBulkCopy)
- Usage point: `LiteOrm.ObjectDAO` reads `SqlBuilder.BulkProvider` when executing batch inserts

**Example: Bulk update (by primary key)**

```csharp
// Get the SqlBuilder for the current data source and make sure its BulkProvider is set
var provider = SqlBuilderFactory.Instance.GetSqlBuilder(dbConnection.GetType()).BulkProvider;
// Convert data to update into DataTable, then call provider's BulkInsert/BulkInsertAsync
await provider.BulkInsertAsync(ToDataTable(usersToUpdate), dbConnection, transaction);
```

`IBulkProvider` exposes only two methods: `BulkInsert` and `BulkInsertAsync`. Batch size, transaction, and concurrency are all controlled by the caller (e.g. partitioning into `DataTable`s and managing `IDbTransaction` yourself).

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
var sales = await salesService.SearchAsAsync<SalesRecordView>(tableArgs: [DateTime.Now.ToString("yyyyMM")]);
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

### 7.2.1 Existence Check Example

Directly compare the different purposes of `ExistsAsync` and `CountAsync`:

```csharp
using static LiteOrm.Common.Expr;
bool exists = await viewService.ExistsAsync(Lambda<TestUser>(u => u.Name == "Unique"));
int count = await viewService.CountAsync(Lambda<TestUser>(u => u.Age >= 50));
```

- Use `ExistsAsync` when you only care about "whether any exist"
- Only use `CountAsync` when you need the exact count

## 8. Connection Management

### 8.1 Use Scoped Lifecycle

```csharp
// In ASP.NET Core, use Scoped (scope tracking is enabled by default)
builder.Host.RegisterLiteOrm();
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
using static LiteOrm.Common.Expr;
// Large data query
await foreach (var user in userViewDAO.Search(Prop("Age") >= 18))
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

LiteOrm's performance advantage vs other ORMs, based on BenchmarkDotNet measurements from the `LiteOrm.Benchmark` project. All numbers below are `Mean`. **Batch time / batch memory are per-run values; single-row time / memory are cumulative over 1000 iterations** (per-call ≈ cumulative ÷ 1000). **Bold = best in row**.

### 10.1 Batch timings (µs, lower is better)

| Operation | Rows | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|-----------|-----:|--------:|-------:|--------:|---------:|-------:|
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

### 10.2 Batch memory (KB, lower is better)

| Operation | Rows | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|-----------|-----:|--------:|-------:|--------:|---------:|-------:|
| Insert | 10 | 83 | **62** | 124 | 105 | 466 |
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

### 10.3 Batch GC (Gen0 / Gen1 / Gen2, thousands of runs)

> `0` means that generation did not collect. The decimals are amortized over the run / row count.

| Operation | Rows | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|-----------|-----:|--------:|-------:|--------:|---------:|-------:|
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

### 10.4 Single-row operations (cumulative over 1000 iterations, lower is better)

> Per-call ≈ below value ÷ 1000.

| Metric | Operation | LiteOrm | Dapper | FreeSql | SqlSugar | EFCore |
|--------|-----------|--------:|-------:|--------:|---------:|-------:|
| Time (ms) | Insert | 3,640.6 | **3,618.0** | 3,932.1 | 3,954.6 | 4,446.5 |
| Time (ms) | Update | 3,655.9 | **3,637.6** | 4,052.4 | 4,077.6 | 4,188.7 |
| Time (ms) | Upsert | 3,829.1 | **3,682.8** | 4,398.1 | 4,663.4 | 4,550.6 |
| Time (ms) | Get | 240.3 | **234.9** | 625.4 | 691.7 | 718.8 |
| Memory (MB) | Insert | **4.30** | 5.43 | 20.22 | 25.27 | 58.57 |
| Memory (MB) | Update | 4.54 | **4.28** | 22.67 | 30.45 | 46.20 |
| Memory (MB) | Upsert | 6.60 | **6.07** | 20.78 | 87.75 | 55.01 |
| Memory (MB) | Get | **3.67** | 6.08 | 18.52 | 50.76 | 47.58 |
| GC (G0 / G1) | Insert | **0 / 0** | **0 / 0** | 1,000 / 0 | 2,000 / 0 | 4,000 / 3,000 |
| GC (G0 / G1) | Update | **0 / 0** | **0 / 0** | 1,000 / 0 | 2,000 / 0 | 3,000 / 1,000 |
| GC (G0 / G1) | Upsert | **0 / 0** | **0 / 0** | 1,000 / 0 | 7,000 / 0 | 4,000 / 1,000 |
| GC (G0 / G1) | Get | **0 / 0** | 500 / 0 | 1,000 / 0 | 4,000 / 0 | 3,000 / 1,000 |

### 10.5 Overall score (geometric mean across dimensions, lower is better)

Each ORM's six dimensions — **batch time / batch memory / batch GC** (4 operations × 4 row counts) and **single-row time / single-row memory / single-row GC** (4 operations) — are normalized to "× relative to the best ORM in that cell", then averaged geometrically per dimension. `1.00×` = the best in that dimension.

| ORM | Batch time | Batch memory | Batch GC | Single time | Single memory | Single GC | Overall |
|-----|-----------:|-------------:|---------:|------------:|--------------:|----------:|--------:|
| LiteOrm | **1.02×** | **1.12×** | **1.15×** | 1.02× | **1.04×** | **1.00×** | **1.06×** |
| Dapper | 1.17× | 2.03× | 2.97× | **1.00×** | 1.20× | 1.39× | 1.51× |
| FreeSql | 1.36× | 3.51× | 4.55× | 1.40× | 4.55× | 4.00× | 2.86× |
| SqlSugar | 2.09× | 9.12× | 10.04× | 1.46× | 9.56× | 4.51× | 4.79× |
| EFCore | 3.46× | 12.74× | 12.03× | 1.52× | 11.46× | 4.69× | 5.93× |

### 10.6 Test Environment

| Configuration | Value |
|---------------|-------|
| Test framework | BenchmarkDotNet v0.15.8 |
| .NET version | .NET SDK 10.0.11 (net10.0) |
| Runtime | X64 RyuJIT x86-64-v3 |
| OS | Linux Ubuntu 24.04 |
| CPU | Intel Xeon Silver 4314 (16 cores / 32 threads) |
| Database | MySQL (default; switchable to SQLite or Oracle via `appsettings.json`) |
| Test data volume | BatchCount: 10 / 100 / 1000 / 10000 rows |
| Benchmark mode | `[MemoryDiagnoser]` + `[MediumRunJob]` (Job=MediumRun, IterationCount=15, LaunchCount=2, WarmupCount=10) |
| Compared ORMs | LiteOrm, Dapper, FreeSql, SqlSugar, EFCore |

> Full BenchmarkDotNet reports live in `LiteOrm.Benchmark/BenchmarkDotNet.Artifacts/results/`. Visualization: https://aad8b2de3fba877d3.app.workbuddy.link/

### 10.7 Numbers worth calling out

- `Insert @10000`: LiteOrm `179,155µs`, **10.2×** faster than EFCore's `1,826,182µs`; **1.56×** faster than Dapper's `278,958µs`. Memory at the same scale: LiteOrm `16,695KB` vs EFCore `316,635KB` — **19.0×** less. GC: LiteOrm `1,333 / 0 / 0` (G0/G1/G2) vs EFCore `26,000 / 8,000 / 1,000` — under one twentieth the collections.
- `Upsert @10000`: SqlSugar `6,708,137µs` time / `3,413,708KB` memory / GC `277,000 / 5,000 / 1,000` — all three dimensions collapse. Time is **27.0×** LiteOrm, memory **148×**, Gen0 GC **185×**.
- `JoinQuery @10000`: EFCore `158,733µs` is actually **slower** than SqlSugar's `174,890µs`… but only by 10%. Memory is also close (`94,250KB` vs `91,661KB`). On large result sets EF Core has no structural edge.
- Single-row `Get`: LiteOrm `240.3ms` (cumulative) essentially ties Dapper's `234.9ms`. LiteOrm memory `3.67MB` beats Dapper's `6.08MB`; GC `0 / 0` beats Dapper's `500 / 0`. This is the only single-row cell where LiteOrm leads Dapper on two dimensions at once.
- Single-row Insert / Update / Upsert: LiteOrm cumulative time is in the `3,640–3,830ms` band; Dapper is 0.3–3.9% faster — much narrower than the batch gap. The reason: per-call "fixed round-trip cost" dominates every ORM in single-row mode. At batch `Insert@10000` the same comparison flips: LiteOrm `22,698ms` vs Dapper `30,853ms` — LiteOrm 26% faster.

## Related Links

- [Back to docs hub](../README.md)
- [Associations](../02-core-usage/08-associations.en.md)
- [Transactions](../06-di/01-transactions.en.md)
- [Expression Extension](../04-extensibility/01-expression-extension.en.md)

