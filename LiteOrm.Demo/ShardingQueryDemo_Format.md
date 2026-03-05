# ShardingQueryDemo 演示格式说明

## 📋 演示结构

### 重新组织后的格式

每个演示按照以下结构进行输出：

1. **演示标题** - 用 Box 风格的分隔符突出显示
2. **📋 场景说明** - 清晰描述本演示要展示的场景
3. **💾 示例数据** - 显示创建的测试数据
4. **📝 代码实现** - 展示使用的 C# 代码
5. **🔍 执行的 SQL** - 显示实际执行的 SQL 语句（从 `SessionManager.Current.SqlStack.Last()` 获取）
6. **✅ 查询结果** - 显示查询的返回结果

### 样例输出

```
╔════════════════════════════════════════════════════════════╗
║         分表查询演示 (Sharding Query Demo)                    ║
╚════════════════════════════════════════════════════════════╝

┌────────────────────────────────────────────────────────────┐
│ 演示1：基础分表查询 - Lambda 内部指定分表参数              │
└────────────────────────────────────────────────────────────┘

【📋 场景说明】
查询金额大于 40 的销售记录，指定查询 Sales_202412 分表

【💾 示例数据】
已插入 2 条销售记录到 Sales_202412 表：
  • Laptop: ¥1000 (2024-12-15)
  • Mouse: ¥50 (2024-12-20)

【📝 代码实现】
var sales = await salesViewService.SearchAsync(s =>
    s.TableArgs == new[] { "202412" } && s.Amount > 40
);

【🔍 执行的 SQL】
SELECT ... FROM Sales_202412 WHERE Amount > 40

【✅ 查询结果】
共返回 2 条记录：
  • Laptop: ¥1000 (销售员: Alice Smith)
  • Mouse: ¥50 (销售员: Alice Smith)
```

## 🎯 关键特性

### 1. SQL 追踪
```csharp
var executedSql = SessionManager.Current?.SqlStack?.Last() ?? "SQL 不可用";
PrintSection("🔍 执行的 SQL", executedSql);
```
- 从 `SessionManager.Current.SqlStack` 获取最后执行的 SQL
- 显示实际生成的 SQL 语句

### 2. 格式化输出
```csharp
private static void PrintSection(string title, string content)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"【{title}】");
    Console.ResetColor();
    Console.WriteLine(content);
}
```
- 统一的部分标题格式
- 彩色输出便于区分

### 3. 演示内容

#### 演示1：基础分表查询
- 展示 Lambda 内部指定 TableArgs
- 简单的 WHERE 条件

#### 演示2：显式指定不同月份
- 切换到不同的分表（202411 vs 202412）
- 验证分表隔离

#### 演示3：动态指定分表参数
- 使用变量动态指定分表
- 演示灵活性

#### 演示4：复杂查询
- 结合 WHERE、ORDER BY、LIMIT
- 展示完整的 Expr API 使用

## 💡 使用建议

### 运行演示
```csharp
var services = new ServiceCollection()
    // ... 配置 ...
    .BuildServiceProvider();

await ShardingQueryDemo.RunAsync(services);
```

### 查看 SQL 语句
演示会自动输出执行的 SQL，帮助理解查询的实际生成逻辑。

### 扩展演示
可以参考现有的 4 个演示模板，添加更多演示场景：
1. 复制现有演示方法
2. 修改场景说明和代码实现
3. 执行查询并获取 SQL
4. 输出结果

## ✨ 优点

- ✅ **清晰的结构** - 每个部分都有明确的标题和分隔
- ✅ **完整的信息** - 包含代码、SQL、结果等所有关键信息
- ✅ **易于对照** - 代码和 SQL 放在一起便于理解
- ✅ **可视化差异** - 彩色输出和 Box 分隔器使演示更清晰
- ✅ **便于扩展** - 统一的格式便于添加新的演示场景
