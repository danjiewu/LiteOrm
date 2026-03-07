# LiteOrm API Reference

---

## 📖 Language / 语言

**[中文](./LITEORM_API_REFERENCE.md)** | **English** 

---

## 📚 Table of Contents

### Main Chapters
- [1. Project Overview](#1-project-overview)
- [2. Quick Start](#2-quick-start)
- [3. File Structure](#3-file-structure)
- [4. Basic Definitions](#4-basic-definitions)
- [5. Expr Expression System](#5-expr-expression-system)
- [6. Basic Features](#6-basic-features)
- [7. Extensions & Advanced Features](#7-extensions--advanced-features)
- [8. Performance](#8-performance)

### Quick Navigation
- [CRUD Operations](#61-basic-crud)
- [Query Methods](#62-query-methods)
- [Aggregation & Joins](#63-aggregation--joins)
- [Sharding Support](#64-sharding)
- [Transaction Operations](#65-transaction-operations)

---

## 1. Project Overview

<a id="1-project-overview"></a>

LiteOrm is a lightweight, high-performance .NET ORM framework that supports multiple databases (SQL Server, MySQL, Oracle, PostgreSQL, SQLite) and provides comprehensive CRUD operations, flexible query expression systems, declarative transactions, automatic association queries, and dynamic table sharding capabilities.

---

## 2. Quick Start

<a id="2-quick-start"></a>

### 2.1 Installation 

<a id="21-installation--configuration"></a>

### Adding LiteOrm Package

```bash
dotnet add package LiteOrm
```

### 2.2 Configuration in appsettings.json

```json
{
    "LiteOrm": {
        "Default": "DefaultConnection",
        "DataSources": [
            {
                "Name": "DefaultConnection",
                "ConnectionString": "Server=localhost;Port=3306;Database=liteorm;Uid=root;Pwd=123456;",
                "Provider": "MySql.Data.MySqlClient.MySqlConnection, MySql.Data",
                "KeepAliveDuration": "00:10:00",
                "PoolSize": 20,
                "MaxPoolSize": 100,
                "ParamCountLimit": 2000,
                "SyncTable": true,
                "ReadOnlyConfigs": [
                    {
                        "ConnectionString": "Server=readonly01;Port=3306;Database=liteorm;Uid=readonly;Pwd=123456;"
                    },
                    {
                        "ConnectionString": "Server=readonly02;Port=3306;Database=liteorm;Uid=readonly;Pwd=123456;",
                        "PoolSize": 10,
                        "KeepAliveDuration": "00:30:00"
                    }
                ]
            }
        ]
    }
}
```

**Configuration Parameters:**

| Parameter | Default | Description |
| :--- | :--- | :--- |
| **Default** | - | Default data source name, used if entity does not specify a data source. |
| **Name** | - | Required, data source name. |
| **ConnectionString** | - | Required, physical connection string. |
| **Provider** | - | Required, fully qualified type name of DbConnection implementation (Assembly Qualified Name). |
| **PoolSize** | 16 | Basic connection pool capacity, idle connections exceeding this number will be released. |
| **MaxPoolSize** | 100 | Maximum concurrent connection limit to prevent exhausting database resources. |
| **KeepAliveDuration** | 10min | Connection idle survival time, idle connections will be physically closed after this time. |
| **ParamCountLimit** | 2000 | Maximum number of parameters supported by a single SQL, batch operations will be automatically batched when parameters exceed this limit to avoid triggering DB limits. |
| **SyncTable** | false | Whether to automatically detect entity classes and attempt to synchronize database table structure on startup. |
| **ReadOnlyConfigs** | - | Read-only database configuration |

#### ReadOnlyConfigs (Read-Only Slave Database Configuration)

LiteOrm supports configuring multiple read-only slave databases for each master data source, used for read-write separation, load balancing, or failover. Read-only configurations are placed in the `ReadOnlyConfigs` array of the corresponding data source object.

Notes:
- `ReadOnlyConfigs`: Optional array, each item is a read-only data source configuration object (can be empty).
- Each read-only item must at least contain `ConnectionString`, and `Provider` can also be specified when the read-only database uses a different driver from the master database.
- LiteOrm will prefer read-only configurations when executing read-only operations (such as SELECT queries), thereby reducing master database write pressure and achieving read scaling.
- Transactions will prefer master database connections to ensure data consistency.

### 2.3 Register LiteOrm

In `Program.cs`:

```csharp
// Generic Host / Console application
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()  // Auto scan AutoRegister attributes and initialize connection pool
    .Build();

// ASP.NET Core application
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
var app = builder.Build();
```

**Note:** LiteOrm uses `RegisterLiteOrm()` for registration, which internally registers `Autofac` as the root container, rather than using `AddLiteOrm()` to register services.

### 2.4 Define Entity

Example code:

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]
    public int? DeptId { get; set; }
}
```

### 2.5 Define Service

Example code:

```csharp
// Define service interface (recommended to explicitly declare Async interface)
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>,
                                IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
    // Custom methods can be added
}

// Implement service (base class automatically provides implementations for all methods)
public class UserService : EntityService<User, UserView>, IUserService
{
    // Inherits all basic CRUD and query methods
}
```

### 2.6 Use Service

Example code:

```csharp
// Insert
var user = new User { UserName = "admin", Age = 25, CreateTime = DateTime.Now };
userService.Insert(user);

// Query
var users = userService.Search(u => u.Age > 18);
var admin = userService.SearchOne(u => u.UserName == "admin");

// Update
user.Age = 30;
userService.Update(user);

// Delete
userService.Delete(user);

// Async operations
await userService.InsertAsync(user);
var users = await userService.SearchAsync(u => u.Age > 18);
```

### 2.7 SessionManager Automatic Session Management

`SessionManager` is the core of LiteOrm's session, responsible for managing database connections and transaction contexts for the current request/task. The framework automatically manages `SessionManager.Current` through Autofac's lifecycle events, **no manual maintenance required**.

#### Automatic Management Mechanism

The LiteOrm framework automatically registers a lifecycle manager when `RegisterLiteOrm()` is called, ensuring:

1. **Automatic Creation** - When the DI container creates a child Scope, it automatically resolves `SessionManager` from that Scope and assigns it to `SessionManager.Current`
2. **Automatic Destruction** - When the Scope ends, it automatically destroys the `SessionManager` instance (triggers `Dispose()` to return all connections)
3. **Async Isolation** - Uses `AsyncLocal<T>` to ensure correct isolation of session contexts for each Scope in async calls

Developers do not need to write any lifecycle management code; the framework handles it completely automatically.

#### ASP.NET Core Application

Simply call `RegisterLiteOrm()` in `Program.cs` to enable all automatic management:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register LiteOrm - automatically manages SessionManager.Current lifecycle
builder.Host.RegisterLiteOrm();

var app = builder.Build();
app.UseRouting();
app.MapControllers();
app.Run();
```

Each HTTP request automatically gets an independent `SessionManager` instance, which is automatically released when the request ends, **no additional configuration required**.

#### Generic Host / Console Application

When creating a DI scope, the framework automatically manages `SessionManager.Current`:

```csharp
// Automatically creates new SessionManager when creating scope
using var scope = host.Services.CreateScope();

// SessionManager.Current is automatically set to the current Scope's instance
var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
var users = userService.Search(u => u.Age > 18);

// When scope ends, SessionManager is automatically destroyed
// All database connections are automatically returned to the connection pool
```

The same applies to background tasks, scheduled tasks, etc. Creating a Scope automatically gets a managed session.

#### Access Current Session

The current Scope's session can be accessed through the `SessionManager.Current` static property in any code (no assignment required):

```csharp
public class UserService : EntityService<User, UserView>, IUserService
{
    [Transaction]  // Declarative transaction
    public bool InsertBatch(List<User> users)
    {
        // SessionManager.Current is automatically the current Scope's instance
        foreach (var user in users)
        {
            Insert(user);
        }
        // Automatically rolls back on exception, commits on success
        return true;
    }

    // Manual transaction control example
    public void ManualTransaction()
    {
        var sessionManager = SessionManager.Current;  // Directly get current Scope's instance
        sessionManager.BeginTransaction();
        try
        {
            Insert(new User { UserName = "test" });
            sessionManager.Commit();
        }
        catch
        {
            sessionManager.Rollback();
            throw;
        }
    }

    // Access SQL logs
    public void DebugSql()
    {
        foreach (var sql in SessionManager.Current.SqlStack)
        {
            Console.WriteLine(sql);
        }
    }
}
```

> **Note:** 
> - `SessionManager.Current` is automatically managed with the DI Scope's lifecycle, no need to manually assign or call `Dispose()`
> - `SessionManager` instances of different Scopes are completely isolated, and correct isolation is ensured in async calls
> - To ensure correct lifecycle management, be sure to use `RegisterLiteOrm()` to register the framework (not `AddLiteOrm()`)
> - Within the DI Scope, `SessionManager.Current` is always available and points to the current Scope's instance

**Console Application:**
```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

**ASP.NET Core Application:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
var app = builder.Build();
```

---

## 3. File Structure

<a id="3-file-structure"></a>

### Project File Structure

```
LiteOrm/
├── Classes/                 # Core classes
│   ├── AssemblyAnalyzer.cs           # Assembly analyzer for scanning entities and services
│   ├── DataSourceProvider.cs         # Data source provider
│   ├── LiteOrmCoreInitializer.cs     # LiteOrm core initializer
│   ├── LiteOrmLambdaHandlerInitializer.cs  # Lambda handler initializer
│   ├── LiteOrmServiceExtensions.cs   # RegisterLiteOrm extension method
│   ├── LiteOrmSqlFunctionInitializer.cs   # SQL function initializer
│   ├── SessionManager.cs             # Session manager, handles connections and transactions
│   └── SqlGen.cs                     # SQL generator
├── DAO/                     # DAO implementations
│   ├── AutoLockDataReader.cs         # Auto-lock data reader
│   ├── BulkProviderFactory.cs        # Bulk operation provider factory
│   ├── DAOBase.cs                    # DAO base class
│   ├── DataDAO.cs                    # Data DAO (batch updates)
│   ├── DataViewDAO.cs                # DataTable DAO implementation
│   ├── DbCommandProxy.cs             # Database command proxy
│   ├── IBulkProvider.cs              # Bulk operation interface
│   ├── ObjectDAO.cs                  # Entity DAO implementation
│   └── ObjectViewDAO                 # View DAO implementation
├── DAOContext/              # DAO context
│   ├── DAOContext.cs                 # DAO context
│   ├── DAOContextPool.cs             # DAO context pool
│   └── DAOContextPoolFactory.cs      # DAO context pool factory
├── Service/                 # Service implementations
│   ├── EntityService.cs              # Entity service implementation
│   ├── EntityViewService.cs          # Entity view service implementation
│   ├── ServiceGenerateInterceptor.cs # Service generate interceptor
│   └── ServiceInvokeInterceptor.cs   # Service invoke interceptor
└── SqlBuilder/               # SQL builder implementations
    ├── MySqlBuilder.cs               # MySQL SQL builder
    ├── OracleBuilder.cs              # Oracle SQL builder
    ├── PostgreSqlBuilder.cs          # PostgreSQL SQL builder
    ├── SQLiteBuilder.cs              # SQLite SQL builder
    ├── SqlBuilder.cs                 # Default SQL builder
    ├── SqlBuilderFactory.cs          # SQL builder factory
    ├── SqlHandlerMap.cs              # SQL handler map
    ├── SqlHandlerMapExtensions.cs    # SQL handler map extensions
    └── SqlServerBuilder.cs           # SQL Server SQL builder

LiteOrm.Common/
├── Attributes/              # Attribute definitions (Table, Column, ForeignType, etc.)
├── Classes/                 # Utility classes
│   ├── Const.cs                      # Constant definitions
│   ├── ExprConvert.cs                # Expression converter
│   ├── ExprDisplayTextBuilder.cs     # Expression display text builder
│   ├── ListEqualityComparer.cs       # List equality comparer
│   ├── MutiReplacer.cs               # Multi-replacer
│   ├── PropertyAccessorExtention.cs  # Property accessor extension
│   ├── SqlValueStringBuilder.cs      # SQL value string builder
│   ├── StringArrayEqualityComparer.cs # String array equality comparer
│   ├── Util.cs                       # Utility class
│   ├── ValueEquality.cs              # Value equality comparison
│   └── ValueStringBuilder.cs         # Value string builder
├── Converter/               # Converters
│   ├── ExprJsonConverterFactory.cs   # Expression JSON converter factory
│   ├── ExprSqlConverter.cs           # Expression SQL converter
│   ├── ExprString.cs                 # Expression string
│   ├── IExprStringBuildContext.cs    # Expression string build context interface
│   └── LambdaExprConverter.cs        # Lambda expression converter
├── DAO/                     # Data access interfaces
│   ├── IDataViewDAO.cs               # Data view DAO interface
│   ├── IObjectDAO.cs                 # Entity DAO interface
│   ├── IObjectDAOAsync.cs            # Entity DAO async interface
│   ├── IObjectViewDAO.cs             # View DAO interface
│   └── ResultTypes.cs                # Result types
├── Expr/                    # Expression system
├── MetaData/                # Meta data
├── Model/                   # Model
│   ├── Interface.cs                  # Interface definitions
│   └── ObjectBase.cs                 # Object base class
├── Service/                 # Service interfaces
│   ├── IEntityService.cs             # Entity service interface
│   ├── IEntityServiceAsync.cs        # Entity service async interface
│   ├── IEntityViewService.cs         # Entity view service interface
│   ├── IEntityViewServiceAsync.cs    # Entity view service async interface
│   ├── LambdaExprExtensions.cs       # Lambda/IQueryable extension methods
│   ├── ServiceDescription.cs         # Service description
│   └── ServiceException.cs           # Service exception
├── SqlBuilder/              # SQL builder interfaces
│   ├── ISqlBuilder.cs                # SQL builder interface
│   ├── ISqlBuilderFactory.cs         # SQL builder factory interface
│   └── SqlBuildContext.cs            # SQL build context
└── SqlSegment/              # SQL segments (Select/Where/OrderBy, etc.)
```

---

## 4. Basic Definitions

<a id="4-basic-definitions"></a>

### 4.1 Entity Class Writing

Standard entity class:

```csharp
using LiteOrm.Common;

[Table("Users")]
public class User : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    [Column("DeptId")]
    [ForeignType(typeof(Department), Alias = "Dept")]  // Foreign key关联到部门表
    public int? DeptId { get; set; }
}
```

### 4.2 View Model Definition

View models are used for query operations and can include related table fields:

```csharp
// Department entity (foreign key association must be defined first)
[Table("Departments")]
public class Department : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;
}

// View model inherits from entity class
public class UserView : User
{
    // Use ForeignType's Alias to associate query department name
    // Corresponding to [ForeignType(typeof(Department), Alias = "Dept")] on User.DeptId
    [ForeignColumn("Dept", Property = "Name")]
    public string? DeptName { get; set; }
}
```

### 4.3 Sharding Support

Implement the IArged interface to support sharding:

```csharp
[Table("Log_{0}")]  // Table name template, {0} is filled by TableArgs
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime")]
    public DateTime CreateTime { get; set; }

    // Automatically route to Log_202401 format tables
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```

### 4.4 EntityService

#### 4.4.1 IEntityService Interface - Basic CRUD and Batch Operations

**File location:** `LiteOrm.Common/Service/IEntityService.cs`

```csharp
// Basic CRUD (defined in IEntityService<T>)
bool Insert(T entity)
bool Update(T entity)
bool Delete(T entity)
bool UpdateOrInsert(T entity)

// Delete by condition (using LogicExpr, defined in non-generic IEntityService)
int Delete(LogicExpr expr, params string[] tableArgs)

// Update by UpdateExpr (defined in non-generic IEntityService)
int Update(UpdateExpr expr, params string[] tableArgs)

// Delete by ID (defined in non-generic IEntityService)
bool DeleteID(object id, params string[] tableArgs)

// Batch operations (with transaction support, defined in IEntityService<T>)
void BatchInsert(IEnumerable<T> entities)
void BatchUpdate(IEnumerable<T> entities)
void BatchUpdateOrInsert(IEnumerable<T> entities)
void BatchDelete(IEnumerable<T> entities)

// Mixed batch operations (insert/update/delete, defined in IEntityService<T>)
void Batch(IEnumerable<EntityOperation<T>> entities)
```

#### 4.4.2 IEntityViewService - View Service Interface

**File location:** `LiteOrm.Common/Service/IEntityViewService.cs`

```csharp
// Get object by ID
T GetObject(object id, params string[] tableArgs)

// Query entity list
List<T> Search(Expr expr = null, params string[] tableArgs)

// Query single
T SearchOne(Expr expr, params string[] tableArgs)

// Iterate (streaming, no memory caching)
void ForEach(Expr expr, Action<T> func, params string[] tableArgs)

// Check existence
bool Exists(Expr expr, params string[] tableArgs)
bool ExistsID(object id, params string[] tableArgs)

// Count
int Count(Expr expr = null, params string[] tableArgs)

// Extension methods (Lambda form, from LambdaExprExtensions, located in LiteOrm.Common/DAO/LambdaExprExtensions.cs)
List<T> Search(Expression<Func<T, bool>> expression, string[] tableArgs = null)
List<T> Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null)
T SearchOne(Expression<Func<T, bool>> expression, string[] tableArgs = null)
bool Exists(Expression<Func<T, bool>> expression, params string[] tableArgs)
int Count(Expression<Func<T, bool>> expression, params string[] tableArgs)
```

#### 4.4.3 Async Interfaces

- `IEntityServiceAsync<T>` is the async version of `IEntityService<T>`
- `IEntityViewServiceAsync<T>` is the async version of `IEntityViewService<T>`
- Lambda extension methods: `SearchAsync`, `SearchOneAsync`, `ExistsAsync`, `CountAsync` (located in `LambdaExprExtensions`)

#### 4.4.4 EntityService - Service Base Class Implementation

The `EntityService<T, TView>` base class automatically implements `IEntityService<T>` and `IEntityViewService<TView>` and their async interfaces.

**File location:** `LiteOrm/Service/EntityService.cs`

```csharp
// When entity type and view type are the same (simplified version)
public interface IProductService : IEntityService<Product>, IEntityServiceAsync<Product>,
                                    IEntityViewService<Product>, IEntityViewServiceAsync<Product>
{
    // Custom methods can be added
}

public class ProductService : EntityService<Product>, IProductService
{
    // EntityService<T> is a convenience version of EntityService<T, T>
}

// When entity type and view type are different (recommended, supports related fields)
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>,
                                IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
    // Custom methods can be added
}

public class UserService : EntityService<User, UserView>, IUserService
{
    // Inherits all CRUD and query methods
}
```

`EntityService<T, TView>` is marked with `[AutoRegister]`, so it can be directly used through DI in a generic way without custom subclasses, suitable for simple scenarios that don't require extending custom methods:

```csharp
// Directly inject or resolve from DI, no need to define any service classes
var entityService = serviceProvider.GetRequiredService<IEntityService<User>>();
var viewService   = serviceProvider.GetRequiredService<IEntityViewService<UserView>>();

// Async versions同理
var entityServiceAsync = serviceProvider.GetRequiredService<IEntityServiceAsync<User>>();
var viewServiceAsync   = serviceProvider.GetRequiredService<IEntityViewServiceAsync<UserView>>();
```

### 4.5 DAO Usage

Some DAO query methods return wrapped result objects that support lazy execution and unified synchronous/asynchronous consumption:

- `EnumerableResult<T>`: Returned by query methods such as `ObjectViewDAO.Search`, wraps the execution result of `DbCommand`, implements both `IEnumerable<T>` and `IAsyncEnumerable<T>`, supports the following consumption methods:
  - Synchronous: `.ToList()`, `.FirstOrDefault()`, `.GetResult()`.
  - Asynchronous: `.ToListAsync()`, `.FirstOrDefaultAsync()`, `.GetResultAsync()`, `await foreach`.
  - Streaming: Implements `IAsyncEnumerable<T>` which can be used with `await foreach` to read row by row, suitable for asynchronous processing of large result sets.
  - Note: The underlying `DbDataReader` of `EnumerableResult<T>` can only be consumed once; if you need to traverse multiple times, please first call `.ToList()` or `.GetResult()` to cache the results in memory.

- `DataTableResult`: Returned by `DataViewDAO.Search`, can obtain `DataTable` through `.GetResult()` / `.GetResultAsync()`, also supports synchronous and asynchronous calls.

#### 4.5.1 ObjectDAO - Entity Data Access

**File location:** `LiteOrm/DAO/ObjectDAO.cs`

**Purpose**: Used for CRUD operations of entities, returns entity objects.

```csharp
// Insert single entity
bool Insert(T entity)
Task<bool> InsertAsync(T entity, CancellationToken cancellationToken = default)

// Update single entity
bool Update(T entity)
Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default)

// Insert or update (judged by primary key)
UpdateOrInsertResult UpdateOrInsert(T entity)
Task<UpdateOrInsertResult> UpdateOrInsertAsync(T entity, CancellationToken cancellationToken = default)

// Delete by entity
bool Delete(T entity)
Task<bool> DeleteAsync(T entity, CancellationToken cancellationToken = default)

// Delete by condition (using LogicExpr)
int Delete(LogicExpr expr)
Task<int> DeleteAsync(LogicExpr expr, CancellationToken cancellationToken = default)

// Delete by ID
bool DeleteByKeys(params object[] keys)
Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default)

// Batch delete IDs
void BatchDeleteByKeys(IEnumerable keys)
Task BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken cancellationToken = default)

// Batch insert
void BatchInsert(IEnumerable<T> entities)
Task BatchInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// Batch update (using single UPDATE statement)
void BatchUpdate(IEnumerable<T> entities)
Task BatchUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// Batch insert or update
void BatchUpdateOrInsert(IEnumerable<T> entities)
Task BatchUpdateOrInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)

// Batch delete
void BatchDelete(IEnumerable<T> entities)
Task BatchDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
```

#### 4.5.2 ObjectViewDAO - Entity View Query

**File location:** `LiteOrm/DAO/ObjectViewDAO.cs`

**Purpose**: Query view models, automatically JOIN related tables.

```csharp
// Query view models (automatic JOIN related tables)
EnumerableResult<T> Search(Expr expr = null)
Task<EnumerableResult<T>> SearchAsync(Expr expr, CancellationToken cancellationToken = default)

// Get object by primary key
EnumerableResult<T> GetObject(params object[] keys)
Task<EnumerableResult<T>> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)

// Check if object exists
ValueResult<bool> Exists(Expr expr)
Task<ValueResult<bool>> ExistsAsync(Expr expr, CancellationToken cancellationToken = default)

// Count
ValueResult<int> Count(Expr expr)
Task<ValueResult<int>> CountAsync(Expr expr, CancellationToken cancellationToken = default)
```

#### 4.5.3 DataDAO - Update by Condition

**File location:** `LiteOrm/DAO/DataDAO.cs`

**Purpose**: Provides batch field update operations for single tables without loading entity objects.

```csharp
// Batch update specified fields by condition (returns NonQueryResult, call GetResult() or GetResultAsync() to execute)
NonQueryResult UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr)

// Update specified fields by primary key
NonQueryResult UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)
```

**Example:**

```csharp
var dataDAO = serviceProvider.GetRequiredService<DataDAO<User>>();

// Set status to inactive for all users with Age > 60
dataDAO.UpdateAllValues(
    new[] { new KeyValuePair<string, object>("Status", "inactive") },
    Expr.Prop("Age") > 60
).GetResult();

// Update specified fields of a single record by primary key
dataDAO.UpdateValues(
    new[] { new KeyValuePair<string, object>("Age", 99) },
    userId
).GetResult();
```

#### 4.5.4 DataViewDAO - View Query (Returns DataTableResult)

**File location:** `LiteOrm/DAO/DataViewDAO.cs`

**Purpose**: Returns results in DataTable format, supports aggregate queries and GroupBy.

```csharp
// Query returns DataTableResult
DataTableResult Search(Expr expr)

// Specified field query
DataTableResult Search(string[] propertyNames, Expr expr)
```

**Note:** Aggregate queries (using GroupBy and aggregate functions such as COUNT/SUM/AVG/MAX/MIN) must use DataViewDAO, as EntityViewService does not support GroupBy.

---

## 5. Expr Detailed Explanation

<a id="5-expr-expression-system"></a>

The core of LiteOrm is the Expr expression system. Lambda expressions are also parsed into Expr objects first, then converted to SQL.

### 5.1 Expr Structure

**File location:** `LiteOrm.Common/Expr/` and `LiteOrm.Common/SqlSegment/`

The Expr expression system is divided into two categories:
- **Logical expressions (LogicExpr and derivatives)** - Used for WHERE, HAVING and other condition fragments
- **Value type expressions (ValueTypeExpr and derivatives)** - Used for SELECT, value calculation, etc.
- **SQL segment expressions (SqlSegment)** - Used for FROM, WHERE, ORDER BY, GROUP BY and other SQL building

**Expr type hierarchy:**

```
Expr (Base class)
├── LogicExpr (Logical expressions for WHERE conditions)
│   ├── LogicBinaryExpr (Binary comparison: ==, >, <, LIKE, IN, IS NULL, etc.)
│   ├── LogicSet (Logical combination: AND / OR)
│   ├── NotExpr (NOT negation)
│   ├── ForeignExpr (EXISTS subquery)
│   └── LambdaExpr (Lambda deferred evaluation expression)
│
├── ValueTypeExpr (Value type expression base class)
│   ├── ValueExpr (Constant or variable value)
│   ├── PropertyExpr (Property/column reference)
│   ├── FunctionExpr (Function call)
│   ├── AggregateFunctionExpr (Aggregate functions: COUNT/SUM/AVG/MAX/MIN)
│   ├── ValueBinaryExpr (Math operations: +, -, *, /)
│   ├── ValueSet (Value set, e.g., CONCAT / LIST)
│   └── SelectExpr (SELECT statement, implements ISqlSegment)
│
└── SqlSegment interface & implementations (SQL query segment building)
    ├── FromExpr (FROM segment, specifies data source table or view)
    ├── WhereExpr (WHERE segment, condition filtering)
    ├── SelectExpr (SELECT segment, field selection)
    ├── OrderByExpr (ORDER BY segment, sorting)
    ├── GroupByExpr (GROUP BY segment, grouping)
    ├── HavingExpr (HAVING segment, group conditions)
    ├── SectionExpr (LIMIT/OFFSET segment, pagination)
    ├── UpdateExpr (UPDATE segment, batch field updates)
    └── DeleteExpr (DELETE segment, batch deletion)
```

**SQL segment expression (SqlSegment) explanation:**

`SqlSegment` type is used to build various SQL query segments, supporting chained API. Common usage:

```csharp
// Chained API - start from FromExpr, gradually add WHERE, ORDER BY, LIMIT, etc.
var fullQuery = Expr.From<User>()          // FromExpr
    .Where(condition)                       // WhereExpr
    .OrderBy(Expr.Prop("Id"))              // OrderByExpr
    .Section(0, 10);                       // SectionExpr

// GroupBy aggregate query
var aggregateQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Having(Expr.Prop("Id").Count() > 5)   // HavingExpr
    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("cnt"));

// Batch update
var updateExpr = new UpdateExpr()
    .Set("Status", "inactive")
    .Where(Expr.Prop("Age") > 60);

// Batch delete
var deleteExpr = new DeleteExpr()
    .Where(Expr.Prop("CreateTime") < DateTime.Now.AddYears(-1));
```

The SqlSegment interface defines a `SegmentType` property to identify the type of the current segment (From, Where, Select, etc.), and the framework handles them differently during SQL generation.

**Operator overloading explanation:**

Both `ValueTypeExpr` (including `PropertyExpr`) and `LogicExpr` overload common C# operators, allowing direct use of operators to concatenate conditions without calling methods.

| Operator | Type | Return Type | Semantics |
| :--- | :--- | :--- | :--- |
| `==` | `ValueTypeExpr` | `LogicExpr` | Equal (automatically handles null as IS NULL) |
| `!=` | `ValueTypeExpr` | `LogicExpr` | Not equal |
| `>` | `ValueTypeExpr` | `LogicExpr` | Greater than |
| `<` | `ValueTypeExpr` | `LogicExpr` | Less than |
| `>=` | `ValueTypeExpr` | `LogicExpr` | Greater than or equal |
| `<=` | `ValueTypeExpr` | `LogicExpr` | Less than or equal |
| `+` `-` `*` `/` | `ValueTypeExpr` | `ValueTypeExpr` | Four arithmetic operations |
| `-` (unary) | `ValueTypeExpr` | `ValueTypeExpr` | Negative sign |
| `&` | `LogicExpr` | `LogicExpr` | Logical AND, returns the other if either is null |
| `\|` | `LogicExpr` | `LogicExpr` | Logical OR, returns null if either is null |
| `!` | `LogicExpr` | `LogicExpr` | Logical NOT |

`ValueTypeExpr` also provides implicit conversion from C# base types to expressions: `string`, `int`, `long`, `double`, `decimal`, `bool`, `DateTime` can all be directly used as right operands.

```csharp
// Comparison operators: directly compare with C# constants
LogicExpr cond1 = Expr.Prop("Age") > 18;         // Right operand int implicit conversion
LogicExpr cond2 = Expr.Prop("Name") == "admin";   // Right operand string implicit conversion
LogicExpr cond3 = Expr.Prop("Score") != null;     // null automatically converted to IS NOT NULL

// Logical operators: concatenate multiple conditions
LogicExpr and  = cond1 & cond2;     // AND
LogicExpr or   = cond1 | cond2;     // OR
LogicExpr not  = !cond1;            // NOT

// Arithmetic operators: column references for mathematical operations
ValueTypeExpr expr = (Expr.Prop("Price") * 0.9) - Expr.Prop("Discount");  // Price * 0.9 - Discount
LogicExpr     cond = expr > 100;
```

> **Note:** 
> - The `&` / `|` operators of `LogicExpr` are used for logical AND/OR operations. If one operand is null, `&` returns the other (i.e., ignores null conditions), and `|` returns null (i.e., ignores the other condition)
> - `==` and `!=` have been overloaded to return `LogicExpr` type instead of `bool` in C# language, so when determining whether an `Expr` type variable is empty, use `is null` and `is not null`
> - Implementing `&&` and `||` operators requires implementing implicit conversion from `LogicExpr` to `bool`, but this would cause `if (expr == null)` to result incorrectly without reporting an error at compile time, so it is not implemented

### 5.2 Expr Construction

#### 5.2.1 Lambda Expression Construction

Convert Lambda expressions to Expr through extension methods, supporting two common ways:
```csharp
// Lambda to Expr (most common way)
LogicExpr expr = Expr.Lambda<User>(u => u.Age > 18 && u.UserName.Contains("admin"));
// Generates SQL: WHERE (age > 18 AND username LIKE '%admin%')

// Use simple Lambda expression form (use when no sorting and pagination is needed)
var users = userService.Search(
    u => u.Age > 18 && u.UserName.Contains("admin")
);


// Use IQueryable form (recommended, supports sorting and pagination)
var users = userService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);

// Multiple condition merging (multiple Where automatically merged as AND)
var result = userService.Search(
    q => q.Where(u => u.Age > 18)
         .Where(u => u.UserName != null)
         .Where(u => u.UserName.Contains("admin"))
);

// Async version
var users = await userService.SearchAsync(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Id)
         .Skip(0)
         .Take(10)
);
```

#### 5.2.2 Manual Expression Construction


```csharp
// Property reference
PropertyExpr prop = Expr.Prop("age");
PropertyExpr prop2 = Expr.Prop(nameof(User.UserName));

// Comparison operations
LogicExpr cmp1 = Expr.Prop("age") > 18;
LogicExpr cmp2 = Expr.Prop("username") == "admin";
LogicExpr cmp3 = Expr.Prop("age") >= 18 && Expr.Prop("age") <= 60;

// LIKE/IN/NULL
LogicExpr like = Expr.Prop("username").Contains("admin");
LogicExpr startWith = Expr.Prop("username").StartsWith("admin");
LogicExpr endWith = Expr.Prop("username").EndsWith("admin");
LogicExpr inExpr = Expr.Prop("id").In(1, 2, 3);
LogicExpr notIn = Expr.Prop("id").NotIn(new[] { 4, 5, 6 });
LogicExpr isNull = Expr.Prop("email").IsNull();
LogicExpr isNotNull = Expr.Prop("email").IsNotNull();

// BETWEEN
LogicExpr between = Expr.Between("age", 18, 60);
// Equivalent writing
LogicExpr between2 = Expr.Prop("age").Between(18, 60);

// Logical combination
LogicExpr andExpr = Expr.And(cmp1, like);
LogicExpr orExpr = Expr.Or(cmp1, cmp2);

// EXISTS subquery (manually constructed, pass LogicExpr)
ForeignExpr existsExpr = Expr.Foreign<Department>(Expr.Prop("Id") == 1);
// Foreign key EXISTS with alias
ForeignExpr existsWithAlias = Expr.Foreign<Department>("Dept", Expr.Prop("Id").IsNotNull());
// Use Expr.Exists<T> in Lambda expressions (see section 6.5)

// Aggregate functions
AggregateFunctionExpr countExpr = Expr.Prop("id").Count();
AggregateFunctionExpr sumExpr = Expr.Prop("amount").Sum();
AggregateFunctionExpr avgExpr = Expr.Prop("price").Avg();
AggregateFunctionExpr maxExpr = Expr.Prop("score").Max();
AggregateFunctionExpr minExpr = Expr.Prop("score").Min();
```

#### 5.2.3 Query Segment Expression (SqlSegment)

```csharp
// Chained API (recommended)
var fullQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .Select(Expr.Prop("Id"), Expr.Prop("UserName"));

// With sorting and pagination
var pagedQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .OrderBy(Expr.Prop("Id"))
    .Section(0, 10);

// Aggregate query (must be executed through DataViewDAO)
var aggregateQuery = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("user_count"));
```

### 5.3 JSON Serialization and Deserialization

`Expr` objects support JSON serialization and deserialization, suitable for logging, configuration, or cross-process/network transmission scenarios. The framework has implemented custom JsonConverter for `Expr` hierarchy types, so you can usually directly use `System.Text.Json`:

```csharp
using System.Text.Json;

// Example: construct expression
Expr expr = Expr.From<User>().Where(Expr.Prop("Age") > 18);

// Serialization
string json = JsonSerializer.Serialize<Expr>(expr, new JsonSerializerOptions { WriteIndented = true });

// Deserialization
Expr deserialized = JsonSerializer.Deserialize<Expr>(json);

// Verification (Expr type implements equality comparison)
bool equal = expr.Equals(deserialized);
```

Notes:
- `GenericSqlExpr` only saves `Key` and `Arg` during serialization; it does not serialize runtime-registered generation delegates. After deserialization, it depends on the corresponding `GenericSqlExpr.Register` registration being completed at runtime, otherwise an exception will be thrown when generating SQL that the key cannot be found;
- `LambdaExpr` is converted to ordinary Expr during serialization, losing the type information and structure of the original Lambda expression; after deserialization, it cannot be restored to a Lambda expression and can only be used as an ordinary Expr;
- Types such as `FunctionExpr` have been marked with custom Converters, which can retain necessary type information and fields;

---

## 6. Basic Features

<a id="6-basic-features"></a>

### 6.1 Basic CRUD

<a id="61-basic-crud"></a>

```csharp
// Insert
var user = new User { UserName = "admin", Age = 25, CreateTime = DateTime.Now };
userService.Insert(user);

// Update
user.Age = 30;
userService.Update(user);

// Insert or update
var result = userService.UpdateOrInsert(user);

// Delete
userService.Delete(user);

// Delete by ID
userService.DeleteID(1);

// Batch operations
var users = new List<User>
{
    new User { UserName = "user1", Age = 20, CreateTime = DateTime.Now },
    new User { UserName = "user2", Age = 25, CreateTime = DateTime.Now }
};
userService.BatchInsert(users);

// Async operations
await userService.InsertAsync(user);
await userService.UpdateAsync(user);
await userService.DeleteAsync(user);
```

### 6.2 Query Methods

<a id="62-query-methods"></a>

LiteOrm provides three ways to construct query conditions, which can be used alone or in combination:

| Method | Type Safety | Suitable Scenarios | Dynamic Construction | Features |
| :--- | :--- | :--- | :--- | :--- |
| **Lambda** | ✅ Compile-time check | Regular queries, sorting and pagination | Limited (most concise when condition structure is fixed) | Linq style, easy to read |
| **Expr** | ❌ Runtime | Dynamic condition combination, serialized transmission | Strong (can be arbitrarily concatenated) | Flexible and powerful, suitable for complex queries |
| **ExprString** | ❌ Runtime | Special SQL optimization, database dialects | Weak (interpolation syntax) | Simple and direct |

> Lambda expressions do not guarantee the legality of the generated SQL. They are automatically converted to `Expr` objects during execution, and both ultimately follow the same SQL generation path with no performance difference.

#### 6.2.1 Lambda Method

```csharp
// Single condition
var users = userService.Search(u => u.Age > 18);

// Multiple conditions (AND)
var users = userService.Search(u => u.Age > 18 && u.UserName.Contains("admin"));

// IN query
var users = userService.Search(u => new[] { 1, 2, 3 }.Contains(u.Id));

// IS NULL
var users = userService.Search(u => u.DeptId == null);

// LIKE
var users = userService.Search(u => u.UserName.Contains("admin"));

// Sorting and pagination
var users = userService.Search(
    q => q.Where(u => u.Age > 18)
         .OrderBy(u => u.Age)
         .ThenByDescending(u => u.Id)
         .Skip(0)
         .Take(10)
);

// Async version
var users = await userService.SearchAsync(u => u.Age > 18);
```

#### 6.2.2 Expr Method

```csharp
// Single condition
var users = userService.Search(Expr.Prop("Age") > 18);

// Multiple conditions (AND)
var expr = Expr.Prop("Age") > 18 && Expr.Prop("UserName").Contains("admin");
var users = userService.Search(expr);

// IN query
var users = userService.Search(Expr.Prop("Id").In(1, 2, 3));

// IS NULL
var users = userService.Search(Expr.Prop("DeptId").IsNull());

// Async version
var users = await userService.SearchAsync(Expr.Prop("Age") > 18);
```

#### 6.2.3 ExprString (Raw SQL)

`.NET 8.0+` supports `ExprString` interpolated string syntax, which can mix `Expr` expressions and ordinary variable values into SQL fragments, and the framework will uniformly convert them to parameterized SQL. Both `ObjectViewDAO` and `DataViewDAO` support this, which is suitable for use inside `EntityViewService` and generally not provided for external calls.

Parameter processing rules:
- `Expr` expressions → converted to equivalent SQL fragments (you can insert only field expressions or complex expressions, for example `WHERE {Expr.Prop("Age") > 18}` will be converted to `WHERE Age > @p0`, while `WHERE {Expr.Prop("Age")} > 18` will be converted to `WHERE Age > 18`)
- Ordinary values (`int`, `string`, etc.) → automatically converted to named parameters like `@p0` to prevent SQL injection (for example `WHERE Age > {18}` is converted to `WHERE Age > @p0`)

> **Note:** Do not directly splice user input strings into SQL at runtime. If external calls are needed, it is recommended to use `GenericSqlExpr` (see Section 7.2). The underlying `DbDataReader` of `EnumerableResult<T>` can only be consumed once; if you need to traverse multiple times, please call `.ToList()` first.

**Example — `ObjectViewDAO` (default appends WHERE fragment):**

```csharp
// Default mode (isFull = false): framework automatically completes SELECT ... FROM ...
var ageExpr = Expr.Prop("Age") > 25;

// Synchronous consumption
var users = objectViewDAO.Search($"WHERE {ageExpr} AND UserName LIKE '张%'").ToList();

// Asynchronous consumption
var users = await objectViewDAO.Search($"WHERE {ageExpr}").ToListAsync();
// GetResultAsync() is completely equivalent to ToListAsync()
var users = await objectViewDAO.Search($"WHERE {ageExpr}").GetResultAsync();

// Streaming processing (IAsyncEnumerable<T>)
await foreach (var user in objectViewDAO.Search($"WHERE {ageExpr}"))
{
    Console.WriteLine(user.UserName);
}

// isFull = true: pass complete SQL, framework does not automatically complete
var users = await objectViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Prop("Age")} > 18 ORDER BY Id",
    isFull: true).ToListAsync();
```

**Example — `DataViewDAO` (returns `DataTableResult`):**

```csharp
int minAge = 20;

// Synchronous
DataTable dt = dataViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").GetResult();

// Asynchronous
DataTable dt = await dataViewDAO.Search($"WHERE {Expr.Prop("Age")} > {minAge}").GetResultAsync();

// isFull = true: can use {AllFields}, {From} placeholders (DataViewDAO will automatically replace)
DataTable dt = await dataViewDAO.Search(
    $"SELECT {{AllFields}} FROM {{From}} WHERE {Expr.Prop("Age")} > {minAge} ORDER BY Age DESC",
    isFull: true).GetResultAsync();
```

> **Note:** `ExprString` syntax requires .NET 8.0 or higher.

#### 6.2.4 Mixed Query Methods

Lambda, `Expr`, and `ExprString` can be used in any combination. Lambda is parsed into `Expr` at runtime, so the three can be directly concatenated using `&` / `|` operators.

```csharp
// Lambda condition + dynamically concatenated Expr condition
LogicExpr baseExpr = Expr.Lambda<User>(u => u.Age > 18);
if (deptId.HasValue)
    baseExpr = baseExpr & (Expr.Prop("DeptId") == deptId.Value);
if (!string.IsNullOrEmpty(keyword))
    baseExpr = baseExpr & Expr.Prop("UserName").Contains(keyword);

var users = await userService.SearchAsync(baseExpr);

// Embed pre-built Expr condition in IQueryable form
LogicExpr auditFilter = Expr.Lambda<User>(u => u.Status == "active") & Expr.Sql("YearFilter", 2024);

var users = await userService.SearchAsync(
    q => q.Where(auditFilter)
         .Where(u => u.Age > 18)    // Multiple Where automatically merged as AND
         .OrderByDescending(u => u.Id)
         .Skip(0).Take(20)
);

// Directly use Lambda expressions and ordinary variables in ExprString
var dt = await dataViewDAO.Search(
    $"SELECT Id, UserName FROM Users WHERE {Expr.Lambda<User>(u => u.Status == "active")} AND {baseExpr} ORDER BY Id LIMIT {pageSize} OFFSET {startIndex}",
    isFull: true).ToListAsync();
```

### 6.3 Aggregation & Joins

<a id="63-aggregation--joins"></a>

#### 6.3.1 Aggregation Queries (Requires DataViewDAO)

Aggregation queries need to be executed through `DataViewDAO`, and `EntityViewService` does not support `GroupBy`.

```csharp
var dataViewDAO = serviceProvider.GetRequiredService<DataViewDAO<User>>();

var selectExpr = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .GroupBy(Expr.Prop("DeptId"))
    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("user_count"));

// Synchronous
DataTable dtSync = dataViewDAO.Search(selectExpr).GetResult();

// Asynchronous
DataTable dtAsync = await dataViewDAO.Search(selectExpr).GetResultAsync();
```

#### 6.3.2 Association Queries

LiteOrm's association queries are implemented by defining entities and views:

```csharp
[Table("Orders")]
public class Order : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(User))]  // Foreign key association to User
    public int? UserId { get; set; }

    [Column("Amount")]
    public decimal Amount { get; set; }
}

public class OrderView : Order
{
    [ForeignColumn(typeof(User), Property = "UserName")]  // Bring out UserName from User table
    public string? UserName { get; set; }
}

```

JOIN is automatically generated during query:

```csharp
var orders = orderViewService.Search(o => o.Amount > 100);
var orders = await orderViewService.SearchAsync(o => o.Amount > 100);
```

#### 6.3.3 EXISTS Subqueries

LiteOrm supports using `Expr.Exists<T>` in Lambda expressions for efficient EXISTS subqueries:

```csharp
// Basic EXISTS query: query all users who have a department
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId));

// EXISTS combined with other conditions
var users = userService.Search(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId));

// Complex subquery conditions
var users = userService.Search(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"));

// NOT EXISTS: query users without a department
var orphans = userService.Search(u => !Expr.Exists<Department>(d => d.Id == u.DeptId));

// Async version
var users = await userService.SearchAsync(u => Expr.Exists<Department>(d => d.Id == u.DeptId));
```

EXISTS only checks for existence and does not return related table data, which is more performant than LEFT JOIN; suitable for "existence check" scenarios, not for situations where related data needs to be returned.

### 6.4 Sharding

<a id="64-sharding"></a>

#### 6.4.1 Sharding Entity Definition

Sharding entities need to implement the `IArged` interface, and sharding parameters are provided through the `TableArgs` property. The framework will automatically pass them to DAO to route to the correct shard. The `TableArgs` property should use immutable values for calculation, usually based on entity properties (such as date, organization) to calculate sharding parameters.

**Note:** The number of elements in the TableArgs array corresponds to the number of placeholders in the table name, not querying multiple shards
```csharp
// Define sharding entity (table name template Log_{yyyyMM})
[Table("Log_{0}")]
public class Log : IArged
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Content")]
    public string Content { get; set; }

    [Column("CreateTime", ColumnMode = ColumnMode.Final)]
    public DateTime CreateTime { get; set; }

    // TableArgs calculated from entity properties, not as database fields. If public, need to mark [Column(false)].
    string[] IArged.TableArgs => [CreateTime.ToString("yyyyMM")];
}
```
#### 6.4.2 Sharding Entity Writing

Write operations for sharding entities do not require additional processing. `TableArgs` will be automatically obtained from the entity, and batch operations will also be automatically grouped and executed in batches according to sharding parameters.

```csharp
// --- Write operations: no need to manually specify table name ---

// Single insert: automatically routed to Log_202601
var log = new Log { Content = "Login", CreateTime = new DateTime(2026, 1, 15) };
await logService.InsertAsync(log);

// Single update / delete: similarly automatically routed
log.Content = "Modified login";
await logService.UpdateAsync(log);
await logService.DeleteAsync(log);

// Batch insert: automatically grouped by TableArgs, written to Log_202601 and Log_202602 respectively
var logs = new List<Log>
{
    new Log { Content = "January log", CreateTime = new DateTime(2026, 1, 10) },
    new Log { Content = "February log", CreateTime = new DateTime(2026, 2, 5) },
};
await logService.BatchInsertAsync(logs);

```
#### 6.4.3 Sharding Entity Query

Query, delete by condition and other operations need to explicitly pass `tableArgs`.
```csharp
// --- Query operations: need to explicitly pass tableArgs ---

// Query logs for a specific month
var jan = await logService.SearchAsync(Expr.Prop("Content").Contains("Login"), tableArgs: ["202601"]);

// Get by ID
var one = await logService.GetObjectAsync(42, tableArgs: ["202601"]);

// Count / existence check
int count  = await logService.CountAsync(null, tableArgs: ["202601"]);
bool exists = await logService.ExistsIDAsync(42, tableArgs: ["202601"]);

// Delete by condition (need to pass tableArgs)
logService.Delete(Expr.Prop("CreateTime") < new DateTime(2026, 1, 31), "202601");

// Delete by ID
logService.DeleteID(42, "202601");
```
#### 6.4.4 `Expr` Sharding Method

`FromExpr` and `ForeignExpr` also support `TableArgs`, which can directly specify sharding parameters at the `Expr` level, suitable for query conditions that include multiple shards with different parameters.

```csharp
// FromExpr + TableArgs: Expr.From<T>() directly accepts tableArgs parameter
// Suitable for performing aggregation queries on shards through DataViewDAO
var selectExpr = Expr.From<Log>("202601")       // Points to Log_202601
    .Where(Expr.Prop("Content").Contains("Login"))
    .GroupBy(Expr.Prop("Content"))
    .Select(Expr.Prop("Content"), Expr.Prop("Id").Count().As("cnt"));

DataTable dt = dataViewDAO.Search(selectExpr).GetResult();

// ForeignExpr + TableArgs: EXISTS subquery can also point to shards
var existsExpr = Expr.Foreign<Log>(
    Expr.Prop("UserId") == Expr.Prop("User.Id"),   // Association condition
    "202601");                                      // Points to Log_202601

var users = userService.Search(existsExpr);

// With alias
var existsWithAlias = Expr.Foreign<Log>("lg",
    Expr.Prop("UserId") == Expr.Prop("User.Id"),
    "202601");

var users2 = userService.Search(existsWithAlias);
```
#### 6.4.5 Lambda Sharding Method

Lambda expressions also support sharding parameters, and extension methods will automatically pass them to DAO to route to the correct shard when converting to `Expr`.

**Note:** If the Lambda expression is in simple form (i.e., directly passing entity parameters instead of IQueryable), it must be directly passed to the `Search` extension method to support sharding. Converting it to `LogicExpr` and then passing it will not correctly shard. Generally, it is recommended to use the method of directly passing `tableArgs` in `Search` method as described in `6.4.3`, and only use this method when different sharding parameters are needed for related tables.
```csharp
// Directly pass tableArgs parameter in Lambda expression
var users = userService.Search(
    u => ((IArged)u).TableArgs == new string[] { "202601" } && u.Age > 18 // Points to shard Log_202601
);
```

### 6.5 Transaction Operations

<a id="65-transaction-operations"></a>

```csharp
[AutoRegister(Lifetime = ServiceLifetime.Scoped)]
[Intercept(typeof(ServiceInvokeInterceptor))]
public class BusinessService : IBusinessService
{
    private readonly IUserService _userService;
    private readonly ISalesService _salesService;

    public BusinessService(IUserService userService, ISalesService salesService)
    {
        _userService = userService;
        _salesService = salesService;
    }

    [Transaction]  // Declarative transaction: automatically rolls back on exception
    public async Task<bool> CreateOrderAsync(User user, SalesRecord sale)
    {
        user.CreateTime = DateTime.Now;
        await _userService.InsertAsync(user);  // ID automatically回填 after Insert
        sale.SalesUserId = user.Id;
        await _salesService.InsertAsync(sale);
        return true;
    }
}
```

### 6.6 SQL Debugging Tools

`SqlGen` can convert `Expr` to database SQL without executing the query, suitable for debugging and logging.

```csharp
var selectExpr = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18)
    .Select(Expr.Prop("Id"), Expr.Prop("UserName"));

var sqlGen = new SqlGen(typeof(User));
var sqlResult = sqlGen.ToSql(selectExpr);
Console.WriteLine(sqlResult);  // Output SQL and parameter list
```

You can also view recently executed SQL through `SessionManager.Current.SqlStack` during runtime:

```csharp
foreach (var sql in SessionManager.Current.SqlStack)
{
    Console.WriteLine(sql);
}
```

---

## 7. Extensions & Advanced Features

<a id="7-extensions--advanced-features"></a>

### 7.1 Service Layer

#### Define Service Interface

```csharp
public interface IUserService : IEntityService<User>, IEntityServiceAsync<User>,
                                IEntityViewService<UserView>, IEntityViewServiceAsync<UserView>
{
    // Custom methods can be added
}
```

#### Implement Service

```csharp
public class UserService : EntityService<User, UserView>, IUserService
{
    // Inherits all CRUD and query methods
}
```

### 7.2 GenericSqlExpr

`GenericSqlExpr` is a generic SQL expression that allows registration of custom SQL generation logic, suitable for complex SQL scenarios that are difficult to express with `Expr`.

**Registration:**

```csharp
// Register in application startup (e.g., Program.cs)
GenericSqlExpr.Register("YearFilter", (args, ctx) => {
    int year = (int)args[0];
    return new LogicBinaryExpr(
        new PropertyExpr("CreateTime"),
        new ValueExpr(new DateTime(year, 1, 1)),
        LogicBinaryOp.GreaterThanOrEqual
    ) & new LogicBinaryExpr(
        new PropertyExpr("CreateTime"),
        new ValueExpr(new DateTime(year + 1, 1, 1)),
        LogicBinaryOp.LessThan
    );
});
```

**Usage:**

```csharp
// Use in Lambda expressions
var users = userService.Search(u => Expr.Sql("YearFilter", 2024) && u.Status == "active");

// Use in Expr
var expr = Expr.Sql("YearFilter", 2024) & Expr.Prop("Status") == "active";
var users = userService.Search(expr);

// Use in ExprString
var dt = await dataViewDAO.Search($"WHERE {Expr.Sql("YearFilter", 2024)}").GetResultAsync();
```

### 7.3 Custom SQL Builders

LiteOrm supports custom SQL builders for specific database dialects. You can implement `ISqlBuilder` and register it through `SqlBuilderFactory`.

**Example:**

```csharp
// Custom SQL builder for a specific database
public class CustomSqlBuilder : SqlBuilder
{
    public override string Quote(string name)
    {
        return $"[{name}]";
    }

    public override string GenerateLimit(int skip, int take)
    {
        return $"OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY";
    }
}

// Register in application startup
SqlBuilderFactory.Register(typeof(MyCustomConnection), () => new CustomSqlBuilder());
```

---

## API Reference

### IEntityService<T> - Basic CRUD

```csharp
bool Insert(T entity)
bool Update(T entity)
bool Delete(T entity)
bool UpdateOrInsert(T entity)
void BatchInsert(IEnumerable<T> entities)
void BatchUpdate(IEnumerable<T> entities)
void BatchDelete(IEnumerable<T> entities)
```

### IEntityViewService<T> - Query Operations

```csharp
List<T> Search(Expr expr = null)
T SearchOne(Expr expr)
int Count(Expr expr = null)
bool Exists(Expr expr)
T GetObject(object id)
void ForEach(Expr expr, Action<T> action)
```

### Async Versions

All methods have async equivalents with `Async` suffix:
- `InsertAsync()`, `UpdateAsync()`, `DeleteAsync()`
- `SearchAsync()`, `SearchOneAsync()`, `CountAsync()`, `ExistsAsync()`
- `BatchInsertAsync()`, `BatchUpdateAsync()`, `BatchDeleteAsync()`

---

## 8. Performance

<a id="8-performance"></a>

### 8.1 Performance Comparison (BatchCount=100)

| Framework | Insert (ms) | Update (ms) | Upsert (ms) | Join Query (ms) |
| :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **3.74** | **4.68** | 5.54 | 0.97 |
| FreeSql | 4.36 | 4.86 | **4.84** | 0.94 |
| SqlSugar | 4.13 | 5.38 | 9.36 | 1.66 |
| Dapper | 13.24 | 16.49 | 18.59 | **0.89** |
| EF Core | 21.97 | 21.57 | 22.97 | 6.68 |

### 8.2 Performance Comparison (BatchCount=1000)

| Framework | Insert (ms) | Update (ms) | Upsert (ms) | Join Query (ms) |
| :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **10,711.9** | **16,472.2** | 16,733.4 | **6,061.1** |
| FreeSql | 17,707.5 | 30,842.5 | **14,769.0** | 6,520.9 |
| SqlSugar | 15,775.0 | 35,522.5 | 66,357.1 | 12,304.3 |
| Dapper | 120,213.5 | 132,356.8 | 136,051.1 | 6,556.1 |
| EF Core | 169,846.8 | 149,932.5 | 157,037.7 | 12,422.7 |

### 8.3 Performance Comparison (BatchCount=5000)

| Framework | Insert (ms) | Update (ms) | Upsert (ms) | Join Query (ms) |
| :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **40.27** | **68.07** | **60.71** | **39.06** |
| FreeSql | 72.49 | 133.94 | **58.18** | 41.22 |
| SqlSugar | 76.64 | 194.13 | 885.87 | 63.74 |
| Dapper | 690.75 | 659.91 | 677.14 | 39.94 |
| EF Core | 824.70 | 749.07 | 794.85 | 49.40 |

### 8.4 Memory Allocation Comparison (BatchCount=1000)

| Framework | Insert (KB) | Update (KB) | Upsert (KB) | Join Query (KB) |
| :--- | :--- | :--- | :--- | :--- |
| **LiteOrm** | **870.27** | **1,514.47** | **2,138.49** | **628.98** |
| FreeSql | 4,629.99 | 6,877.24 | 2,239.19 | 854.07 |
| SqlSugar | 4,571.36 | 7,677.75 | 35,952.45 | 9,226.19 |
| Dapper | 2,476.22 | 3,094.99 | 2,798.43 | 415.97 |
| EF Core | 18,118.07 | 15,149.28 | 14,803.48 | 2,198.79 |

> 📊 For detailed performance benchmark reports, see [LiteOrm.Benchmark](./LiteOrm.Benchmark/LiteOrm.Benchmark.OrmBenchmark-report-github.md)

---

Last updated: 2026-03-05

**[Back to top](#liteorm-api-reference)** | **[中文版本](./LITEORM_API_REFERENCE.md)**
