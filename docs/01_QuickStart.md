# 快速入门

本文介绍如何快速上手 LiteOrm，包括安装、配置和第一个示例。

## 1. 安装

通过 NuGet 安装 LiteOrm：

```bash
dotnet add package LiteOrm
```

LiteOrm 支持以下数据库：

- SQL Server 2012+
- MySQL 5.7+ / MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

## 2. 环境配置

### 2.1 配置连接字符串

在 `appsettings.json` 中添加 LiteOrm 配置：

```json
{
  "LiteOrm": {
    "Default": "DefaultConnection",
    "DataSources": [
      {
        "Name": "DefaultConnection",
        "ConnectionString": "Server=localhost;Database=TestDb;User Id=root;Password=123456;",
        "Provider": "MySqlConnector.MySqlConnection, MySqlConnector"
      }
    ]
  }
}
```

### 2.2 配置项说明

| 配置项 | 说明 |
|--------|------|
| `Default` | 默认数据源名称 |
| `DataSources[].Name` | 数据源标识，用于 `[Table]` 特性的 `DataSource` 参数 |
| `DataSources[].ConnectionString` | 数据库连接字符串 |
| `DataSources[].Provider` | 数据库连接类型全名，格式：`TypeName, AssemblyName` |
| `DataSources[].SqlBuilder` | SQL 构建器类型（可选，不填则按 Provider 自动匹配） |
| `DataSources[].PoolSize` | 连接池缓存的最大连接数，默认 16 |
| `DataSources[].MaxPoolSize` | 最大并发连接数限制，默认 100 |
| `DataSources[].ParamCountLimit` | SQL 参数数量上限，默认 2000 |

## 3. 服务注册

### 3.1 控制台应用

```csharp
var host = Host.CreateDefaultBuilder(args)
    .RegisterLiteOrm()
    .Build();
```

### 3.2 ASP.NET Core 应用

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.RegisterLiteOrm();
```

### 3.3 带选项注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterScope = true;
    options.Assemblies = new[] { typeof(MyService).Assembly };
});
```

## 4. 定义实体

```csharp
[Table("Users")]
public class User
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string? UserName { get; set; }

    [Column("Email")]
    public string? Email { get; set; }

    [Column("Age")]
    public int Age { get; set; }

    [Column("CreateTime")]
    public DateTime? CreateTime { get; set; }
}
```

### 特性说明

| 特性 | 说明 |
|------|------|
| `[Table("Name")]` | 指定表名，可选 `DataSource` 参数指定数据源 |
| `[Column("Name", IsPrimaryKey, IsIdentity)]` | 指定列名、主键、自增属性 |

## 5. 定义服务（可选）

```csharp
public interface IUserService
    : IEntityService<User>, IEntityServiceAsync<User>,
      IEntityViewService<User>, IEntityViewServiceAsync<User>
{ }

public class UserService : EntityService<User>, IUserService
{ }
```

## 6. 增删改查示例

### 6.1 插入

```csharp
var user = new User
{
    UserName = "admin",
    Email = "admin@test.com",
    Age = 30,
    CreateTime = DateTime.Now
};

await userService.InsertAsync(user);
Console.WriteLine($"插入后返回的ID: {user.Id}");
```

### 6.2 查询

```csharp
// 根据条件查询
var adults = await userService.SearchAsync(u => u.Age >= 18);

// 排序分页
var pagedUsers = await userService.SearchAsync(
    q => q.Where(u => u.Age >= 18)
          .OrderByDescending(u => u.CreateTime)
          .Skip(0).Take(10)
);

// 单条查询
var admin = await userService.SearchOneAsync(u => u.UserName == "admin");
```

### 6.3 更新

```csharp
admin.Email = "newemail@test.com";
await userService.UpdateAsync(admin);
```

### 6.4 删除

```csharp
await userService.DeleteAsync(admin);

// 按条件删除
await userService.DeleteAsync(u => u.Age < 18);
```

## 7. 下一步

- 关联查询：[关联查询](./05_Associations.md)
- 深入学习：[基础概念](./02_CoreConcepts.md)
- 查询详解：[查询指南](./03_QueryGuide.md)
- 完整操作：[增删改查](./04_CrudGuide.md)
