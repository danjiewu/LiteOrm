# 并发控制与读写分离典型应用

并发、事务和读写路径经常出现在同一个故障现场：两个用户同时改一条记录，其中一次修改被静默覆盖；事务里读到的数据是旧值；写完立刻查又查不到。下面按场景给出处理方式。

## 场景 1：两个人同时改一条记录

**需求**：订单编辑页面打开期间，别人也改了同一条记录，后提交的人不能静默覆盖前一个人的修改。

**做法**：在实体上标记时间戳列，更新时把“读到的旧值”作为参数传进去：

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

```csharp
var user = await viewDao.GetObject(id).FirstOrDefaultAsync();
var oldVersion = user.Version;

user.Name = "New name";
user.Version = oldVersion + 1;          // 新值由调用方给出，框架不做自增

bool ok = await dao.UpdateAsync(user, oldVersion);
if (!ok) throw new ConcurrencyConflictException("记录已被他人修改，请重新加载。");
```

生成的两条语句形状：

```sql
-- 带时间戳：旧值进 WHERE
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ? AND Version = ?

-- 不带时间戳参数：只按主键更新，后写覆盖先写
UPDATE TestTimestampUsers SET Name = ?, Version = ? WHERE Id = ?
```

要点：

- 列类型不限，`int` 版本号、`rowversion`、时间戳都行。表里第一个 `IsTimestamp = true` 的列会成为 `TableDefinition.TimestampColumn`。
- 冲突结果是影响 0 行，方法返回 `false`，不抛异常。忽略返回值就等于放弃并发保护。
- 适用方法是 `Update(T, object? timestamp)` / `UpdateAsync(T, object? timestamp, CancellationToken)`，新值取自实体属性当前值，框架不做自增或替换。
- `UpdateOrInsert` 内部先查再写，不带时间戳条件，不适合“先读后改”的业务，它面向幂等写入。
- 传入时间戳但实体没有标记列时抛 `InvalidOperationException`。

## 场景 2：删除也要并发保护

**需求**：只有我看到的那一版记录才能删，别人改过之后我的删除应该失败。

**做法**：删除语句只按主键生成，不带时间戳条件，所以并发保护要自己加条件：

```csharp
using static LiteOrm.Common.Expr;

// 用带版本条件的批量删除
var affected = orderService.DeleteAll(
    Expr.Lambda<TestTimestampUser>(u => u.Id == id && u.Version == oldVersion));

if (affected == 0) throw new ConcurrencyConflictException("记录已被他人修改。");
```

要点：

- `Delete(entity)` / `DeleteID(id)` 都是按主键删，不使用时间戳列。
- 需要“先更新再删除”的业务，就在同一个事务里先做一次带时间戳的更新，成功后再删。
- `DeleteAll` 的条件里同样可以叠加数据范围条件，见[数据权限典型应用](./02-data-permission.md)。

## 场景 3：多步写入要原子

**需求**：转账、库存扣减这类操作要跨多条记录，中间失败必须整体回滚。

**做法**：声明式与手动两种写法：

```csharp
// 声明式：作用于服务方法
[Transaction]
public async Task TransferAsync(long fromId, long toId, decimal amount, CancellationToken cancellationToken = default)
{
    await accountService.UpdateAllAsync(
        Expr.Update<Account>(a => new Account { Balance = a.Balance - amount }, a => a.Id == fromId),
        null, cancellationToken);

    await accountService.UpdateAllAsync(
        Expr.Update<Account>(a => new Account { Balance = a.Balance + amount }, a => a.Id == toId),
        null, cancellationToken);
}
```

```csharp
// 隔离级别可按需指定
[Transaction(IsolationLevel = IsolationLevel.Serializable)]
public void RebuildIndex(long tenantId) { /* ... */ }
```

```csharp
// 手动：包住一段自定义逻辑
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    await fromService.UpdateAsync(from, cancellationToken);
    await toService.UpdateAsync(to, cancellationToken);
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

要点：

- 最后一条对审计有直接影响：服务 A 调用服务 B 时不会产生两条调用记录，链路标识要自己传，见[审计与变更追踪典型应用](./04-audit-and-change-tracking.md)。
- 服务方法内部再开后台任务写库，不继承当前事务边界。需要异步收尾的写入要么放进同一事务，要么显式另起作用域。

## 场景 4：读多写少，想用只读副本

**需求**：报表和列表查询压力大，希望读到只读副本上。

**做法**：只读副本挂在主库配置下面，运行期不需要改代码：

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

没有填写的连接池参数会继承主库配置。选择规则：

| 条件 | 走哪个连接 |
| --- | --- |
| 查询走视图 DAO（`ObjectViewDAO<T>`、`DataViewDAO<T>` 的 `IsView` 为 `true`） | 只读副本 |
| 写入走 `ObjectDAO<T>` | 主库 |
| 未配置 `ReadOnlyConfigs` | 回落主库 |
| 当前处于事务中 | 强制主库 |
| 同一会话内第二次查询 | 复用第一次选中的只读副本 |

要点：

- 多个只读副本之间按轮询分配，同一会话内选中的副本会缓存并复用，避免每次查询都换一台。
- 要不要读副本由 DAO 类型决定，所以写入实体和展示视图分开定义，读路径自然分流。
- 副本存在复制延迟，任何依赖“读到最新值”的逻辑都要显式走主库，见下一个场景。

## 场景 5：写完立刻读，读到的还是旧值

**需求**：提交修改后马上跳转到详情页，详情页显示的还是修改前的数据。

**做法**：把“读自己刚写的值”这一步固定到主库：

```csharp
await orderService.UpdateAsync(order);

// 方式一：把读取放进同一个事务，事务内读取强制回落主库
await SessionManager.Current!.ExecuteInTransactionAsync(async session =>
{
    var latest = await orderService.GetObjectAsync(order.Id);
});
```

```csharp
// 方式二：给关键读入口单独准备一个读主库的 DAO
public sealed class MasterOrderViewDAO : ObjectViewDAO<OrderView>
{
    public MasterOrderViewDAO(SessionManager sessionManager) : base(sessionManager) { }

    protected override bool IsView => false;   // 覆盖后该 DAO 的读取不再走只读副本
}
```

```csharp
services.AddScoped<ObjectViewDAO<OrderView>, MasterOrderViewDAO>();
```

要点：

- 三条路按业务容忍度选：读进同一事务、给关键入口准备读主库的 DAO、把关键流程改成异步确认。
- 副本选择由 DAO 的 `IsView` 决定（`DAOBase.IsView` 默认 `false`，`ObjectViewDAO<T>` 覆盖为 `true`），覆盖回 `false` 就回到主库，不需要改查询语句。
- 时间戳并发的旧值通常来自一次查询。这次查询落在只读副本上时，拿到的版本号可能比主库当前值旧，于是正常的更新会被判为冲突。需要在并发判断上保持精确时，把“读当前版本”这一步固定到主库，例如把关键更新收进一个带 `[Transaction]` 的服务方法，先读后写都在事务内完成。
- 数据权限里的归属校验同理，读副本的滞后会造成误判，见[数据权限典型应用](./02-data-permission.md)的场景 3。

## 场景 6：并发与事务的常见误区

| 误区 | 后果 |
| --- | --- |
| 认为时间戳冲突会抛异常 | 实际返回 `false`，忽略返回值就丢更新 |
| 期望框架自增版本号 | 新值来自实体属性，不自增 |
| 用 `UpdateOrInsert` 做并发控制 | 它不带时间戳条件 |
| 用删除做并发保护 | `DELETE` 只按主键 |
| 在事务里指望只读副本 | 事务内读取强制主库 |
| 把只读副本上的值用于乐观并发判断 | 副本滞后导致误判冲突 |
| 服务方法内部开后台任务写库 | 后台任务不继承当前事务边界 |

## 相关链接

- [返回目录](../README.md)
- [事务](../06-di/01-transactions.md)
- [配置参考](../05-reference/01-configuration-reference.md)
- [分表分库](../03-advanced-topics/02-sharding-and-tableargs.md)
- [审计与变更追踪典型应用](./04-audit-and-change-tracking.md)
- [数据权限典型应用](./02-data-permission.md)
