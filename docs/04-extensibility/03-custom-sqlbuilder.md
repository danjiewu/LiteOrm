# 自定义 SqlBuilder / 方言扩展

当默认数据库方言不足以覆盖目标数据库版本或特殊 SQL 行为时，可以通过自定义 `SqlBuilder` 扩展 LiteOrm。

## 什么时候需要自定义 `SqlBuilder`

- 旧版本数据库不支持默认分页语法。
- 目标数据库的函数名、分页表达式或 SQL 片段与默认实现不一致。
- 你希望统一封装某个数据库版本的兼容逻辑。

## 常见扩展点

| 扩展点 | 作用 |
| --- | --- |
| `BuildSelectSql` | 自定义查询语句的最终拼装方式。 |
| `RegisterFunctionSqlHandler` | 注册自定义 SQL 函数翻译。 |
| 数据源级 `RegisterSqlBuilder(...)` | 将自定义方言绑定到指定数据源。 |

## Oracle 11g 分页示例

Oracle 11g 不支持现代 `LIMIT/OFFSET` 语法，可以通过继承 `OracleBuilder` 覆盖分页逻辑：

```csharp
public class Oracle11gBuilder : OracleBuilder
{
    public readonly static new Oracle11gBuilder Instance = new Oracle11gBuilder();

    public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
    {
        // 使用 ROW_NUMBER() OVER(...) 包装分页
    }
}
```

完整实现可参考 [自定义分页](../03-advanced-topics/05-custom-paging.md)。

## 注册方式

### 按数据源注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("LegacyOracle", Oracle11gBuilder.Instance);
});
```

### 按连接类型注册

```csharp
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder(typeof(OracleConnection), Oracle11gBuilder.Instance);
});
```

## 完整接入流程

```csharp
// 1. 编写自定义方言
public sealed class LegacyOracleBuilder : OracleBuilder
{
    public static readonly LegacyOracleBuilder Instance = new LegacyOracleBuilder();
}

// 2. 在启动阶段注册
builder.Host.RegisterLiteOrm(options =>
{
    options.RegisterSqlBuilder("LegacyOracle", LegacyOracleBuilder.Instance);
});

// 3. 在配置里把实体或数据源指向对应数据库
[Table("Users", DataSource = "LegacyOracle")]
public class User
{
}

// 4. 业务层继续按统一方式查询
var users = await userService.SearchAsync(q => q.Where(u => u.Status == 1).Skip(0).Take(20));
```

这个模式适合把兼容逻辑封装在基础设施层，业务代码不需要知道数据库版本差异。

## 扩展函数处理器

```csharp
MySqlBuilder.Instance.RegisterFunctionSqlHandler("DATE_FORMAT", (ref outSql, expr, context, sqlBuilder, outputParams) =>
{
    outSql.Append("DATE_FORMAT(");
    expr.Args[0].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(", ");
    expr.Args[1].ToSql(ref outSql, context, sqlBuilder, outputParams);
    outSql.Append(')');
});
```

如果函数来自 Lambda 或成员扩展，还需要在 `LambdaExprConverter` 中注册对应的转换逻辑。详见 [表达式扩展](./01-expression-extension.md)。

## 设计建议

- 优先复用现有 Builder，只覆盖差异行为。
- 数据库兼容逻辑尽量收敛在方言层，不要散落到业务代码。
- 旧数据库兼容通常先从分页和日期函数开始处理。

## 相关链接

- [返回目录](../README.md)
- [自定义分页](../03-advanced-topics/05-custom-paging.md)
- [表达式扩展](./01-expression-extension.md)
- [配置与注册](../01-getting-started/03-configuration-and-registration.md)

