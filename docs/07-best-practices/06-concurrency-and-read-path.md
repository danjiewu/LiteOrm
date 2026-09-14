# 并发控制、事务与读取路径

这三件事经常一起出现在同一个故障现场：两个用户同时改一条记录，其中一次修改被静默覆盖；事务里读到的数据是旧值；写完立刻查又查不到。这一篇把时间戳并发、事务边界和只读副本路径讲清楚，并说明它们交叉时会出现什么。

## 1. 乐观并发：时间戳列

在实体上标记一个时间戳列：

```csharp
[Table("TestTimestampUsers")]
public class TestTimestampUser : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string? Name { get; set; }

    [Column("Version", IsTimestamp = true)]
    public int Version { get; set; }
}
```

列类型不限，`int` 版本号、`rowversion`、时间戳都行。表里第一个 `IsTimestamp = true` 的列会成为 `TableDefinition.TimestampColumn`。

更新时把“读到的旧值”作为参数传进去：

```csharp
var user = await viewDao.GetObject(id).FirstOrDefaultAsync();
user.Name = "New name";
user.Version = user.Version + 1;          // 新值由调用方给出，框架不做自增
bool ok = dao.Update(user, timestamp: 读到的旧值);
```

生成的两条语句形状：

```sql
-- 带时间戳：旧值进 WHERE
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ? AND Version = ?

-- 不带时间戳参数：只按主键更新，后写覆盖先写
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ?
```

行为细节：

| 项 | 说明 |
| --- | --- |
| 新值 | 取自实体属性当前值，框架不做自增或替换 |
| 旧值 | 取自传入的 `timestamp` 参数，进入 `WHERE` |
| 冲突结果 | 影响 0 行，方法返回 `false`，不抛异常 |
| 适用方法 | `Update(T, object? timestamp)` / `UpdateAsync(T, object? timestamp, CancellationToken)` |
| 不适用 | `UpdateOrInsert`（内部先查再写，不带时间戳条件）；`DELETE` 只按主键，不带时间戳条件 |
| 缺少时间戳列 | 传入时间戳但实体没有标记列时抛 `InvalidOperationException` |

拿到 `false` 之后要做什么由业务决定：重读一次再合并，或者直接告诉用户“数据已被他人修改”。这是乐观并发的语义，不是异常。

两点容易忽略：

- 删除没有并发保护。如果业务需要“只有我看到的那一版才能删”，要自己加条件删除（`DeleteAll` 传带时间戳的条件表达式），或者在应用层先做一次带时间戳的更新再删除。
- `UpdateOrInsert` 不做并发判断，它适合幂等写入场景，不适合“先读后改”的业务。

## 2. 事务边界

两种用法，声明式与手动：

```csharp
// 声明式：作用于服务方法
[Transaction]
public async Task TransferAsync(long fromId, long toId, decimal amount) { /* ... */ }

// 隔离级别可指定
[Transaction(IsolationLevel = IsolationLevel.Serializable)]
public void RebuildIndex(long tenantId) { /* ... */ }
```

```csharp
// 手动：包住一段自定义逻辑
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    await fromService.UpdateAsync(from);
    await toService.UpdateAsync(to);
});
```

边界规则：

| 规则 | 行为 |
| --- | --- |
| 已有事务时再次 `BeginTransaction` | 返回 `false` 并记警告，不嵌套新事务 |
| 声明式事务嵌套调用 | 内层复用外层，内层的隔离级别不生效 |
| 同一 `SessionManager` 内的多个数据源 | 全部纳入同一事务；只读连接跳过 |
| 事务中新建的数据源上下文 | 加入当前事务 |
| 事务中的查询 | 强制走主库，忽略只读副本设置 |
| 嵌套的服务调用 | 复用外层事务，不再触发 `OnInvoking` / `OnInvoked` |

最后一条对审计有直接影响：服务 A 调用服务 B 时不会产生两条调用记录，链路标识要自己传。

## 3. 读取路径与只读副本

只读副本挂在主库配置下面：

```json
{
  "LiteOrm": {
    "Default": "WriteDB",
    "DataSources": [
      {
        "Name": "WriteDB",
        "ConnectionString": "Server=master;...",
        "Provider": "...",
        "ReadOnlyConfigs": [
          { "ConnectionString": "Server=replica01;..." },
          { "ConnectionString": "Server=replica02;...", "PoolSize": 10 }
        ]
      }
    ]
  }
}
```

没有填写的连接池参数会继承主库配置。

选择规则：

| 条件 | 走哪个连接 |
| --- | --- |
| 查询走视图 DAO（`ObjectViewDAO<T>`、`DataViewDAO<T>` 的 `IsView` 为 `true`） | 只读副本 |
| 写入走 `ObjectDAO<T>` | 主库 |
| 未配置 `ReadOnlyConfigs` | 回落主库 |
| 当前处于事务中 | 强制主库 |
| 同一会话内第二次查询 | 复用第一次选中的只读副本 |

多个只读副本之间按轮询分配。同一会话内选中的副本会缓存并复用，避免每次查询都换一台。

## 4. 三者交叉时的两个坑

### 写完立刻读

写入走主库、读取走副本，两者之间可能有复制延迟。下面这段代码在延迟窗口内会读到旧值：

```csharp
await orderService.UpdateAsync(order);
var latest = await orderService.GetObjectAsync(order.Id);   // 走只读副本，可能还是旧数据
```

需要“写完读到自己的写”时，三条路：

1. 把读取放进同一个事务。事务内的读取会强制回落主库。
2. 读取时绕开视图 DAO，直接用 `ObjectDAO<T>` 所在的主库路径。
3. 业务上接受最终一致，把关键流程改成异步确认。

### 用副本上的旧值做并发判断

时间戳并发的旧值通常来自一次查询。如果这次查询落在只读副本上，拿到的时间戳可能比主库当前值旧，于是正常的更新会被判为冲突：

```csharp
var user = await viewService.GetObjectAsync(id);     // 只读副本，Version 可能滞后
user.Version = user.Version + 1;
var ok = await dao.UpdateAsync(user, 读到的旧值);     // 明明没人改，却返回 false
```

需要在并发判断上保持精确时，把“读当前版本”这一步固定到主库。常见做法是把关键更新收进一个带 `[Transaction]` 的服务方法，先读后写都在事务内完成。

## 5. 常见误区

| 误区 | 后果 |
| --- | --- |
| 认为时间戳冲突会抛异常 | 实际返回 `false`，忽略返回值就丢更新 |
| 期望框架自增版本号 | 新值来自实体属性，不自增 |
| 用 `UpdateOrInsert` 做并发控制 | 它不带时间戳条件 |
| 在事务里指望只读副本 | 事务内读取强制主库 |
| 把只读副本上的值用于乐观并发判断 | 副本滞后导致误判冲突 |
| 用删除做并发保护 | `DELETE` 只按主键 |
| 服务方法内部开后台任务写库 | 后台任务不继承当前事务边界 |

## 相关链接

- [返回目录](../README.md)
- [事务](../06-di/01-transactions.md)
- [分表分库](../03-advanced-topics/02-sharding-and-tableargs.md)
- [配置参考](../05-reference/01-configuration-reference.md)
- [审计与变更追踪](./03-audit-trail.md)
- [数据映射与值转换](../03-advanced-topics/11-data-mapping.md)
