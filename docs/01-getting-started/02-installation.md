# 安装与环境要求

本文介绍 LiteOrm 的运行环境、数据库支持和安装方式。

## 环境要求

- `.NET 8.0+`
- `.NET Standard 2.0`（兼容 .NET Framework 4.6.1+）
- 依赖库：`Microsoft.Extensions.DependencyInjection`、`Castle.Core`

## 支持的数据库

- SQL Server 2012+
- &#x20;MySQL 8.0+
- Oracle 12c+
- PostgreSQL
- SQLite

> 对于旧版本数据库，如果默认分页语法不兼容，请参考 [自定义分页](../03-advanced-topics/05-custom-paging.md) 与 [自定义 SqlBuilder / 方言扩展](../04-extensibility/03-custom-sqlbuilder.md)。

## 通过 NuGet 安装

```bash
dotnet add package LiteOrm
```

## 安装后的下一步

1. 准备连接字符串和数据源配置。
2. 在宿主启动阶段调用 `RegisterLiteOrm()`。
3. 定义实体、服务或 DAO。
4. 使用 `SearchAsync`、`InsertAsync` 等 API 完成首个示例。

## 相关链接

- [返回目录](../README.md)
- [配置与注册](./03-configuration-and-registration.md)
- [第一个完整示例](./04-first-example.md)
- [配置项速查](../05-reference/01-configuration-reference.md)

