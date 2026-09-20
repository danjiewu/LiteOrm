# 计算列的实际应用

计算列（`ColumnMode.Computed`）不生成物理列、不参与插入/更新，查询时 `SELECT` 与条件引用都按表达式返回结果。它最实际的价值是把"派生值"收敛到一处定义，既不用多落一个冗余列、也不用在 C# 侧到处手写同一条计算。

下面以订单表为例，看它在实际场景里怎么用。

```csharp
[Table("SalesOrders")]
public class SaleOrder
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public long Id { get; set; }

    [Column("Quantity")]
    public int Quantity { get; set; }

    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [Column("DeptId", AllowNull = true)]
    public int? DeptId { get; set; }                 // null 表示全局共享数据

    // 场景 1：派生展示列 —— 小计不落库，读出来就是表达式结果
    [Column("LineTotal", Expression = "{Quantity} * {UnitPrice}", ColumnMode = ColumnMode.Computed)]
    public decimal LineTotal { get; set; }

    // 场景 3：可见性归一化 —— 把 null（全局共享）归一为 0，范围过滤时少一层特判
    [Column("VisibleDept", Expression = "COALESCE({DeptId}, 0)", ColumnMode = ColumnMode.Computed)]
    public int VisibleDept { get; set; }
}
```

## 场景 1：派生展示列

小计是 `Quantity * UnitPrice`，如果落成物理列，插入/更新都要手动同步，改算法还得改两份。声明为计算列后，`SELECT` 直接返回表达式结果，列表展示不用在 C# 侧再算，也不引入需要人工维护的冗余列。

```csharp
var row = await viewService.GetObjectAsync(id, ...);   // row.LineTotal 已经是表达式算出的值
```

## 场景 2：同一表达式用于过滤与排序

派生值不只能展示，还能直接进 `WHERE`。`LineTotal` 在查询条件里被展开为 `({Quantity} * {UnitPrice})`，无需把表达式再写一遍、也不用手拼字符串：

```csharp
var bigOrders = await viewService.SearchAsync(x => x.LineTotal >= 10000, ...);  // 大单筛选
var top       = await viewService.SearchAsync(x => x.UnitPrice > 0, orderBy: o => o.LineTotal, ...);  // 按金额排序
```

但因为条件里展开的是表达式，这类过滤走不了普通列索引。量大的大单筛选应在物理列上建索引，或干脆落成冗余列。

## 场景 3：可见性归一化，让范围过滤少一层特判

`DeptId = null` 表示全局共享数据、对所有人可见。范围过滤若要同时匹配"本部门 + 全局"，每个分支都得写 `DeptId == 部门 || DeptId == null`。用 `COALESCE({DeptId}, 0)` 归一为 `0` 后，只要 `VisibleDept == 0` 一个条件即可覆盖全局：

```csharp
var visible = Prop(nameof(SaleOrder.VisibleDept)) == user.DeptId
           | Prop(nameof(SaleOrder.VisibleDept)) == 0;
```

这条 `Expr` 既可以直接传给查询，也能塞进 `ConstFilter` 全局生效，见[数据权限](./data-permission.md)的方式二。归一化的起点（用 `0` 还是其他哨兵值）要避开真实的部门编号。

## 场景 4：只读计算属性走 Lambda 解析

有些派生值不便于用 `[Column]` 静态声明——要么逻辑动态、要么只想在 `Lambda` 里引用而不要列语义。这时可以注册 Lambda 成员处理器，把实体上的只读计算属性翻译成 SQL 表达式，之后在 `Lambda` 查询里直接使用：

```csharp
public class SaleOrder
{
    public DateTime CreateTime { get; set; }

    // 只读计算属性，不落库：距离下单的天数
    public int DaysAgo => (int)(DateTime.Now - CreateTime).TotalDays;
}

LambdaExprConverter.RegisterMemberHandler(typeof(SaleOrder), "DaysAgo", (node, converter) =>
{
    return new FunctionExpr("DATEDIFF", new FunctionExpr("DAY"), new PropertyExpr("CreateTime"), new FunctionExpr("CURRENT_DATE"));
});

// 之后即可在 Lambda 中使用
var recent = await viewService.SearchAsync(x => x.DaysAgo <= 7, ...);
```

与 `[Column(Computed)]` 的区别在「只读属性 vs 列」：前者不进列结构、不做 `SELECT` 字段，专门服务于动态/按需的 Lambda 条件；后者是真正的计算列，读回与条件都按表达式渲染。此方式适合 `RegisterMemberHandler` 按需注册，完整步骤见[表达式扩展 · 计算属性](../extensibility/expression-extension.md)。

## 何时不适合

- **要高频过滤/关联且靠索引**：计算列在 `WHERE` 展开为表达式，通常无法复用普通列索引，命中量大时应改回物理列并建索引。
- **跨方言字符串/函数逻辑**：字符串 `Expression` 支持 `{属性名}` 占位符，也可以直接写方言原始 SQL，但迁移到不同数据库要随方言同步调整。
- **需要参数化的动态逻辑**：Expr 树 `ExpressionExpr` 只允许不生成参数的固定表达式，带值的动态拼接会抛 `NotSupportedException`。

## 相关链接

- [返回目录](../README.md)
- [实体映射 · 计算列定义](../core-usage/entity-mapping.md)
- [数据权限](./data-permission.md)
- [多租户隔离](./tenant-isolation.md)