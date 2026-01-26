# LiteOrm Benchmark Comparison

This benchmark compares **LiteOrm**, **EF Core**, **SqlSugar**, **FreeSql**, and **Dapper** across various database operations.

## Comparison Categories

### 1. Insert Operations
- **Sync/Async**: Comparing the overhead of asynchronous task management vs synchronous execution.
- **Batching**: Measuring efficiency when inserting 1,000 records.
- **Methods**:
    - `EFCore_Insert_Async` / `EFCore_Insert_Sync`
    - `SqlSugar_Insert_Async` / `SqlSugar_Insert_Sync`
    - `LiteOrm_Insert_Async` / `LiteOrm_Insert_Sync`
    - `Dapper_Insert_Async` / `Dapper_Insert_Sync`
    - `FreeSql_Insert_Async` / `FreeSql_Insert_Sync`

### 2. Update Operations
- **Scenario**: Updating the `Name` field for 1,000 existing records.
- **Methods**:
    - `EFCore_Update_Async`
    - `SqlSugar_Update_Async`
    - `LiteOrm_Update_Async` (Uses `BatchUpdateAsync`)
    - `Dapper_Update_Async`
    - `FreeSql_Update_Async`

### 3. Query Operations (Filtered)
- **Scenario**: Single table filtered query (`BenchmarkUser` where `Age > 20`).
- **Sync/Async**: Comparing simple selection and object materialization speed.
- **Methods**:
    - `*_Query_Async`
    - `*_Query_Sync`

### 4. Complex Join Queries
- **Scenario**: Multi-table join query (`BenchmarkLog` join `BenchmarkUser`) filtering logs for users younger than 30 (`User.Age < 30`).
- **Focus**: Comparing different ORM approaches to table joins, foreign property mapping, and complex filtering.
- **Methods**:
    - `*_JoinQuery_Async`

---

## How to Run

1.  **Database Setup**: Ensure a MySQL instance is running with the credentials provided in `OrmBenchmark.cs`.
2.  **Execution**:
    ```bash
    cd LiteOrm.Benchmark
    dotnet run -c Release
    ```
3.  **Results**: Check the `BenchmarkDotNet.Artifacts\results` folder for detailed Markdown and CSV reports.
