# 自定义分页实现示例

本文档展示如何在 LiteOrm 中实现自定义分页策略，以 Oracle 11g 为例。

## 1. 自定义分页实现

以下是 `Oracle11gBuilder` 的完整实现，它继承自 `OracleBuilder` 并覆盖了分页逻辑：

```csharp
public class Oracle11gBuilder : OracleBuilder 
{
    /// <summary> 
    /// 获取 <see cref="Oracle11gBuilder"/> 的单例实例，适用于 Oracle 11g 及以上版本。 
    /// </summary> 
    public readonly static new Oracle11gBuilder Instance = new Oracle11gBuilder(); 
    
    /// <summary> 
    /// 将结构化的 SQL 片段组装成最终的 SELECT 语句 (Oracle 实现)。 
    /// 使用 ROW_NUMBER() OVER(...) 双层嵌套子查询实现分页，兼容所有 Oracle 版本。 
    /// </summary> 
    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result) 
    {
        bool hasPaging = subSelect.Take > 0; 

        if (hasPaging) 
        {
            // 外层：过滤 ROW_NUMBER() 范围 
            result.Append("SELECT * FROM (\n"); 
        }

        // 内层：实际数据查询 
        result.Append("SELECT "); 
        result.Append(subSelect.Select.AsSpan()); 

        if (hasPaging) 
        {
            // 内层：计算 ROW_NUMBER()，ORDER BY 移至 OVER 子句 
            result.Append(",ROW_NUMBER() OVER (ORDER BY "); 
            if (subSelect.OrderBy.Length > 0) 
                result.Append(subSelect.OrderBy.AsSpan()); 
            else 
                result.Append('1'); 
            result.Append(") AS \"RN__\""); 
        }

        if (subSelect.From.Length > 0) 
        {
            result.Append(" \nFROM "); 
            result.Append(subSelect.From.AsSpan()); 
        }

        if (subSelect.Where.Length > 0) 
        {
            result.Append(" \nWHERE "); 
            result.Append(subSelect.Where.AsSpan()); 
        }

        if (subSelect.GroupBy.Length > 0) 
        {
            result.Append(" \nGROUP BY "); 
            result.Append(subSelect.GroupBy.AsSpan()); 
        }

        if (subSelect.Having.Length > 0) 
        {
            result.Append(" \nHAVING "); 
            result.Append(subSelect.Having.AsSpan()); 
        }

        if (hasPaging) 
        {
            // 关闭内层子查询，提供别名供外层层引用 
            result.Append("\n) \"__T\"\n"); 
            // 按 ROW_NUMBER() 范围过滤（1-based，skip 条之后，共取 take 条） 
            result.Append("WHERE \"RN__\" > "); 
            result.Append(subSelect.Skip.ToString()); 
            result.Append(" AND \"RN__\" <= "); 
            result.Append((subSelect.Skip + subSelect.Take).ToString()); 
        }
        else 
        {
            if (subSelect.OrderBy.Length > 0) 
            {
                result.Append(" \nORDER BY "); 
                result.Append(subSelect.OrderBy.AsSpan()); 
            }
        }
    } 
}
```

## 2. 实现原理

### 2.1 分页机制

Oracle 11g 不支持 `LIMIT` 和 `OFFSET` 语法，因此需要使用 `ROW_NUMBER() OVER()` 函数来实现分页：

1. **内层查询**：计算每行的行号 `RN__`
2. **外层查询**：根据行号范围过滤结果

### 2.2 核心逻辑

- **无分页**：直接生成标准 SELECT 语句
- **有分页**：
  - 在内层查询中添加 `ROW_NUMBER() OVER(ORDER BY ...) AS RN__`
  - 将 ORDER BY 子句移至 OVER 子句中
  - 在外层查询中根据 `RN__` 字段过滤结果范围

## 3. 使用方法

### 3.1 注册自定义 SqlBuilder

有三种方式注册自定义的 `Oracle11gBuilder`：

#### 方法 1：全局替换默认 Oracle 构建器

```csharp
// 在应用启动时注册
using Oracle.ManagedDataAccess.Client;

// 根据连接类型注册
SqlBuilderFactory.Instance.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);

// 或者根据数据源名称注册
SqlBuilderFactory.Instance.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);
```

#### 方法 2：通过 RegisterLiteOrm 选项注册（推荐）

```csharp
// 在注册 LiteOrm 时指定自定义 SqlBuilder
using Oracle.ManagedDataAccess.Client;
using System.Reflection;

var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm(options =>
    {
        // 按数据源名称注册
        options.RegisterSqlBuilder("OracleDataSource", Oracle11gBuilder.Instance);
        
        // 或者按连接类型注册（全局替换）
        options.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);
    })
    .Build();
```

#### 方法 3：通过配置文件指定（推荐）

在 `appsettings.json` 中通过 `SqlBuilder` 字段直接指定自定义 SqlBuilder 的类型名：

```json
{
    "LiteOrm": {
        "Default": "OracleDataSource",
        "DataSources": [
            {
                "Name": "OracleDataSource",
                "ConnectionString": "Data Source=ORCL;User Id=user;Password=pass;",
                "Provider": "Oracle.ManagedDataAccess.Client.OracleConnection, Oracle.ManagedDataAccess",
                "SqlBuilder": "YourNamespace.Oracle11gBuilder, YourAssembly",
                "PoolSize": 20,
                "MaxPoolSize": 100
            }
        ]
    }
}
```

**说明**：
- `SqlBuilder` 字段的值格式为 `类型全名称, 程序集名称`
- 这种方式更加灵活，不需要在代码中手动注册 SqlBuilder
- 适用于需要在配置中动态指定不同 SqlBuilder 实现的场景

### 3.2 使用示例

#### 基本分页查询

```csharp
// 使用服务层
var pageResult = await userService.SearchAsync(
    q => q.Where(u => u.Status == 1)
          .OrderBy(u => u.Id)
          .Skip(10).Take(20)
);

// 直接使用 DAO
var users = await objectViewDAO.Search(
    Expr.From<User>()
        .Where(u => u.Status == 1)
        .OrderBy(nameof(User.Id))
        .Section(10, 20) // 跳过10条，取20条
).ToListAsync();
```

#### 复杂条件分页

```csharp
var query = Expr.From<User>()
    .Where(Expr.Prop("Age") > 18 & Expr.Prop("Status").In(1, 2, 3))
    .OrderByDescending("CreateTime")
    .Section(0, 10); // 第一页，10条记录

var result = await userService.SearchAsync(query);
```

## 4. 生成的 SQL 示例

### 4.1 无分页查询

```sql
SELECT "T0"."ID", "T0"."USERNAME", "T0"."EMAIL", "T0"."CREATETIME" 
FROM "USERS" "T0" 
WHERE "T0"."STATUS" = :0 
ORDER BY "T0"."ID"
```

### 4.2 有分页查询

```sql
SELECT * FROM (
SELECT "T0"."ID", "T0"."USERNAME", "T0"."EMAIL", "T0"."CREATETIME",ROW_NUMBER() OVER (ORDER BY "T0"."ID") AS "RN__"
FROM "USERS" "T0" 
WHERE "T0"."STATUS" = :0 
) "__T"
WHERE "__T"."RN__" > 10 AND "__T"."RN__" <= 30
```

**说明**：
- 生成的 SQL 默认会为主表添加别名 "T0"，并使用该别名限定所有列名
- 参数名采用纯数字格式（如 :0、:1 等），避免参数名冲突
- 列名和表名会根据数据库类型自动格式化：
  - Oracle: 使用双引号包裹并转为大写（如 "USERS"、"ID"）
  - SQL Server: 使用方括号包裹（如 [Users]、[Id]）
  - MySQL: 使用反引号包裹（如 `users`、`id`）
  - PostgreSQL: 使用双引号包裹并转为小写（如 "users"、"id"）
- 这种格式化确保了 SQL 语句在不同数据库中的兼容性和正确性

## 5. 性能优化建议

1. **索引优化**：确保 ORDER BY 字段上有适当的索引
2. **减少数据传输**：只选择必要的列，避免 SELECT *
3. **合理设置分页大小**：根据实际需求调整 Take 值
4. **使用绑定参数**：避免 SQL 注入并提高性能

## 6. 兼容性说明

- **Oracle 11g+**：完全兼容
- **Oracle 10g**：需要确保 ROW_NUMBER() 函数可用
- **其他数据库**：需要实现对应的自定义构建器

## 7. 扩展其他数据库

可以参考 `Oracle11gBuilder` 的实现，为其他数据库创建自定义分页策略：

### 7.1 SQL Server 2008 及以下版本

```csharp
public class SqlServer2008Builder : SqlServerBuilder
{
    public readonly static new SqlServer2008Builder Instance = new SqlServer2008Builder();
    
    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // 实现 TOP + ROW_NUMBER() 分页
        // ...
    }
}
```

### 7.2 PostgreSQL

```csharp
public class CustomPostgreSqlBuilder : PostgreSqlBuilder
{
    public readonly static new CustomPostgreSqlBuilder Instance = new CustomPostgreSqlBuilder();
    
    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // 实现自定义分页逻辑
        // ...
    }
}
```

## 8. 常见问题

### 8.1 分页查询性能问题

**问题**：大数据量分页查询速度慢

**解决方案**：
- 确保 ORDER BY 字段有索引
- 考虑使用覆盖索引
- 对于非常大的表，可以考虑使用书签分页

### 8.2 排序问题

**问题**：分页结果排序不正确

**解决方案**：
- 确保 ORDER BY 子句包含唯一字段
- 当没有指定 ORDER BY 时，会使用 `ORDER BY 1` 作为默认排序

## 9. 总结

通过实现自定义的 `SqlBuilder`，可以为不同数据库版本和场景提供最优的分页策略，从而提高查询性能和兼容性。LiteOrm 的模块化设计使得这种扩展非常简单直观。
