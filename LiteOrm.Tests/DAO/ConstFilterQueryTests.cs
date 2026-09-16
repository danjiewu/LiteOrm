using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 在真实数据库（SQLite）上验证 <c>[Column(Constant = ...)]</c> 收敛出的表固定筛选条件在查询与条件读写路径上的实际行为：
    /// 表达式查询（含关联表的 <c>JOIN ... ON</c>）、聚合与存在性判断、条件更新与删除、<see cref="DataDAO{T}"/> 的按条件更新、
    /// 异步批量写入，以及运行时替换 <see cref="TableDefinition.ConstFilter"/> 后的生效范围。
    /// <para>
    /// 模型 <see cref="ConstFilterOrder"/> 只“看得见” State = Enabled 的行；<see cref="ConstFilterOrderWithDept"/> 通过
    /// <see cref="ConstFilterDept"/> 关联到带固定筛选的部门表。
    /// </para>
    /// </summary>
    [Collection("Database")]
    public class ConstFilterQueryTests : TestBase
    {
        public ConstFilterQueryTests(DatabaseFixture fixture) : base(fixture) { }

        private ObjectDAO<ConstFilterOrder> ObjectDao => ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterOrder>>();
        private ObjectViewDAO<ConstFilterOrder> ViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ConstFilterOrder>>();
        private DataDAO<ConstFilterOrder> DataDao => ServiceProvider.GetRequiredService<DataDAO<ConstFilterOrder>>();
        private ObjectDAO<ConstFilterDept> DeptDao => ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterDept>>();
        private ObjectDAO<ConstFilterOrderWithDept> OrderWithDeptDao => ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterOrderWithDept>>();
        private ObjectViewDAO<ConstFilterOrderWithDeptInnerView> InnerViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ConstFilterOrderWithDeptInnerView>>();
        private ObjectViewDAO<ConstFilterOrderWithDeptLeftView> LeftViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ConstFilterOrderWithDeptLeftView>>();

        [Fact]
        public async Task QueryPaths_RespectConstFilter()
        {
            var ct = TestContext.Current.CancellationToken;
            var visible = NewOrder("QueryVisible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("QueryHidden", ConstFilterOrderState.Disabled);

            // 无条件查询只返回可见行
            var all = await ViewDao.Search().ToListAsync(ct);
            var only = Assert.Single(all);
            Assert.Equal(visible.Id, only.Id);

            // 条件命中被切片挡住的行时查不出结果
            Assert.Empty(await ViewDao.Search(Expr.Prop(nameof(ConstFilterOrder.Name)) == hidden.Name!).ToListAsync(ct));
            Assert.Single(await ViewDao.Search(Expr.Prop(nameof(ConstFilterOrder.Name)) == visible.Name!).ToListAsync(ct));

            // 聚合与存在性判断同样只算可见行
            Assert.Equal(1, await ViewDao.Count(Expr.Prop(nameof(ConstFilterOrder.Id)) > 0).GetResultAsync(ct));
            Assert.True(await ViewDao.Exists(Expr.Prop(nameof(ConstFilterOrder.Name)) == visible.Name!).GetResultAsync(ct));
            Assert.False(await ViewDao.Exists(Expr.Prop(nameof(ConstFilterOrder.Name)) == hidden.Name!).GetResultAsync(ct));
        }

        [Fact]
        public async Task ConditionDeleteAndUpdate_RespectConstFilter()
        {
            var ct = TestContext.Current.CancellationToken;
            var visible = NewOrder("CondVisible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("CondHidden", ConstFilterOrderState.Disabled);

            // 条件删除：模型看不见的行删不掉
            Assert.Equal(0, ObjectDao.Delete(Expr.Prop(nameof(ConstFilterOrder.Id)) == hidden.Id));

            // 条件更新：同样改不到
            var updateHidden = Expr.Update<ConstFilterOrder>()
                .Set(nameof(ConstFilterOrder.Name), Expr.Const("CondUpdated"))
                .Where(Expr.Prop(nameof(ConstFilterOrder.Id)) == hidden.Id);
            Assert.Equal(0, ObjectDao.Update(updateHidden));

            var rows = await ReadRawOrdersAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Id == hidden.Id && row.Name == "CondHidden");

            // 可见行照旧可改可删
            var updateVisible = Expr.Update<ConstFilterOrder>()
                .Set(nameof(ConstFilterOrder.Name), Expr.Const("CondUpdated"))
                .Where(Expr.Prop(nameof(ConstFilterOrder.Id)) == visible.Id);
            Assert.Equal(1, ObjectDao.Update(updateVisible));
            Assert.Equal(1, ObjectDao.Delete(Expr.Prop(nameof(ConstFilterOrder.Id)) == visible.Id));
        }

        [Fact]
        public async Task DataDAO_UpdateAllValues_RespectConstFilter()
        {
            var ct = TestContext.Current.CancellationToken;
            var visible = NewOrder("DataVisible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("DataHidden", ConstFilterOrderState.Disabled);

            var values = new List<KeyValuePair<string, object>>
            {
                new(nameof(ConstFilterOrder.Name), "DataUpdated")
            };
            int affected = await DataDao.UpdateAllValues(values, Expr.Prop(nameof(ConstFilterOrder.Id)) > 0).GetResultAsync(ct);

            // 只更新到可见的那一行
            Assert.Equal(1, affected);
            var rows = await ReadRawOrdersAsync(ct);
            Assert.Contains(rows, row => row.Id == visible.Id && row.Name == "DataUpdated");
            Assert.Contains(rows, row => row.Id == hidden.Id && row.Name == "DataHidden");
        }

        [Fact]
        public async Task AsyncWrites_RespectConstFilter()
        {
            var ct = TestContext.Current.CancellationToken;
            var visible = NewOrder("AsyncVisible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("AsyncHidden", ConstFilterOrderState.Disabled);

            // 单条更新与按主键删除都碰不到切片外的行
            visible.Name = "AsyncVisibleUpdated";
            hidden.Name = "AsyncHiddenUpdated";
            Assert.True(await ObjectDao.UpdateAsync(visible, null, ct));
            Assert.False(await ObjectDao.UpdateAsync(hidden, null, ct));
            Assert.False(await ObjectDao.DeleteByKeysAsync(new object[] { hidden.Id }, ct));
            Assert.Equal(0, await ObjectDao.DeleteAsync(Expr.Prop(nameof(ConstFilterOrder.Id)) == hidden.Id, ct));

            // 批量更新与批量删除同样只作用于可见行
            ObjectDao.BatchUpdate(new[] { visible, hidden });
            ObjectDao.BatchDeleteByKeys(new object[] { visible.Id, hidden.Id });

            var rows = await ReadRawOrdersAsync(ct);
            // 切片外的行既没被更新也没被删除，切片内的行已被删除
            var remaining = Assert.Single(rows);
            Assert.Equal(hidden.Id, remaining.Id);
            Assert.Equal("AsyncHidden", remaining.Name);
        }

        [Fact]
        public async Task InsertAndBatchInsert_IgnoreConstFilter()
        {
            var ct = TestContext.Current.CancellationToken;
            var visible = NewOrder("InsertVisible", ConstFilterOrderState.Enabled);

            // 插入不带切片条件：切片外的值也能写进去
            var hiddenById = new ConstFilterOrder { Name = "InsertHidden", State = ConstFilterOrderState.Disabled };
            Assert.True(ObjectDao.Insert(hiddenById));
            ObjectDao.BatchInsert(new[]
            {
                new ConstFilterOrder { Name = "BatchInsertHidden1", State = ConstFilterOrderState.Disabled },
                new ConstFilterOrder { Name = "BatchInsertHidden2", State = ConstFilterOrderState.Disabled }
            });

            var rows = await ReadRawOrdersAsync(ct);
            Assert.Equal(4, rows.Count);

            // 写进去之后模型依然看不见它们
            var visibleRows = await ViewDao.Search().ToListAsync(ct);
            var only = Assert.Single(visibleRows);
            Assert.Equal(visible.Id, only.Id);
            Assert.False(await ViewDao.ExistsKey(hiddenById.Id).GetResultAsync(ct));
        }

        [Fact]
        public async Task RuntimeConstFilterChange_AppliesToQueries()
        {
            var ct = TestContext.Current.CancellationToken;
            var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(ConstFilterOrder))!;
            var originalFilter = tableDefinition.ConstFilter;
            try
            {
                var enabled = NewOrder("SwapEnabled", ConstFilterOrderState.Enabled);
                var disabled = NewOrder("SwapDisabled", ConstFilterOrderState.Disabled);

                Assert.Single(await ViewDao.Search().ToListAsync(ct));

                // 运行时把切片换成「只看 Disabled」
                tableDefinition.ConstFilter = Expr.Prop(nameof(ConstFilterOrder.State)) == Expr.Const(ConstFilterOrderState.Disabled);

                var rows = await ViewDao.Search().ToListAsync(ct);
                var only = Assert.Single(rows);
                Assert.Equal(disabled.Id, only.Id);
                Assert.Equal(0, ObjectDao.Delete(Expr.Prop(nameof(ConstFilterOrder.Id)) == enabled.Id));
            }
            finally
            {
                tableDefinition.ConstFilter = originalFilter;
            }
        }

        [Fact]
        public async Task JoinedConstFilter_FiltersInnerJoinedRows()
        {
            var ct = TestContext.Current.CancellationToken;
            var enabledDept = NewDept("JoinEnabledDept", ConstFilterOrderState.Enabled);
            var disabledDept = NewDept("JoinDisabledDept", ConstFilterOrderState.Disabled);
            NewOrderWithDept("JoinOrderEnabled", enabledDept.Id);
            NewOrderWithDept("JoinOrderDisabled", disabledDept.Id);

            // 内联接 + JOIN ... ON 上的固定筛选：条件不成立的关联行整条被排除
            var rows = await InnerViewDao.Search().ToListAsync(ct);
            var only = Assert.Single(rows);
            Assert.Equal("JoinOrderEnabled", only.Name);
            Assert.Equal("JoinEnabledDept", only.DeptName);
        }

        [Fact]
        public async Task JoinedConstFilter_HidesLeftJoinedColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var enabledDept = NewDept("LeftEnabledDept", ConstFilterOrderState.Enabled);
            var disabledDept = NewDept("LeftDisabledDept", ConstFilterOrderState.Disabled);
            NewOrderWithDept("LeftOrderEnabled", enabledDept.Id);
            var hiddenOrder = NewOrderWithDept("LeftOrderDisabled", disabledDept.Id);

            // 左联接保留主表行，但被固定筛选挡住的关联列取不到值
            var rows = await LeftViewDao.Search().ToListAsync(ct);
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Name == "LeftOrderEnabled" && row.DeptName == "LeftEnabledDept");
            Assert.Contains(rows, row => row.Name == "LeftOrderDisabled" && row.DeptName == null);

            // DAO 按主键的读取路径走的是模型自带的 From 子句，关联表的固定筛选不在这条路径上：
            // 这里锁住现状，改动 From 的生成方式后本断言需要同步调整。
            var byKey = await LeftViewDao.GetObject(hiddenOrder.Id).FirstOrDefaultAsync(ct);
            Assert.NotNull(byKey);
            Assert.Equal("LeftDisabledDept", byKey!.DeptName);
        }

        [Fact]
        public async Task ReadOnlyConstantProperty_IsInsertedAndStaysVisible()
        {
            var ct = TestContext.Current.CancellationToken;
            var readOnlyDao = ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterReadOnlyOrder>>();
            var readOnlyViewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<ConstFilterReadOnlyOrder>>();
            var otherDao = ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterOrder>>();

            // 文档示例写法：切片属性只读（=> Enabled），插入时按属性取值写入，写进去的行天然落在切片内
            var order = new ConstFilterReadOnlyOrder { Name = "ReadOnly" };
            Assert.True(readOnlyDao.Insert(order));
            Assert.True(order.Id > 0);

            var rows = await readOnlyViewDao.Search().ToListAsync(ct);
            var only = Assert.Single(rows);
            Assert.Equal(order.Id, only.Id);
            Assert.Equal(ConstFilterOrderState.Enabled, only.State);

            // 对比：切片属性可写时，写入切片外的值会让行立刻对模型不可见
            var hidden = new ConstFilterOrder { Name = "ReadOnlyContrast", State = ConstFilterOrderState.Disabled };
            Assert.True(otherDao.Insert(hidden));
            Assert.False(await ViewDao.ExistsKey(hidden.Id).GetResultAsync(ct));
        }

        private ConstFilterOrder NewOrder(string name, ConstFilterOrderState state)
        {
            var order = new ConstFilterOrder { Name = name, State = state };
            Assert.True(ObjectDao.Insert(order));
            Assert.True(order.Id > 0);
            return order;
        }

        private ConstFilterDept NewDept(string name, ConstFilterOrderState state)
        {
            var dept = new ConstFilterDept { Name = name, State = state };
            Assert.True(DeptDao.Insert(dept));
            Assert.True(dept.Id > 0);
            return dept;
        }

        private ConstFilterOrderWithDept NewOrderWithDept(string name, int deptId)
        {
            var order = new ConstFilterOrderWithDept { Name = name, DeptId = deptId };
            Assert.True(OrderWithDeptDao.Insert(order));
            Assert.True(order.Id > 0);
            return order;
        }

        /// <summary>
        /// 绕过模型直接读原始行，用于断言被固定筛选挡住的行的真实内容。
        /// 固定筛选是模型层规则，完整 SQL 通道不注入条件。
        /// </summary>
        private async Task<List<ConstFilterOrder>> ReadRawOrdersAsync(System.Threading.CancellationToken ct)
        {
            var rows = ViewDao.Search($"SELECT [ID], [NAME], [STATE] FROM [DAOCONSTFILTERORDERS] ORDER BY [ID]", isFull: true);
            return await rows.ToListAsync(ct);
        }
    }

    /// <summary>
    /// 只读固定筛选属性（文档示例写法）：属性取值恒等于切片值，插入时按属性取值写入。
    /// </summary>
    [Table("DaoConstFilterReadOnlys")]
    public class ConstFilterReadOnlyOrder
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("State", Constant = ConstFilterOrderState.Enabled)]
        public ConstFilterOrderState State => ConstFilterOrderState.Enabled;
    }

    /// <summary>
    /// 带固定筛选列的部门模型：State 固定为 Enabled，State = Disabled 的行对模型不可见。
    /// </summary>
    [Table("DaoConstFilterDepts")]
    public class ConstFilterDept
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("State", Constant = ConstFilterOrderState.Enabled)]
        public ConstFilterOrderState State { get; set; }
    }

    /// <summary>
    /// 关联到 <see cref="ConstFilterDept"/> 的订单模型，用于验证关联表固定筛选。
    /// </summary>
    [Table("DaoConstFilterOrdersWithDept")]
    public class ConstFilterOrderWithDept
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("DeptId")]
        [ForeignType(typeof(ConstFilterDept), Alias = "Dept")]
        public int DeptId { get; set; }
    }

    /// <summary>
    /// 内联接视图：切片外的部门会让订单整条查不出来。
    /// </summary>
    [TableJoin(typeof(ConstFilterDept), "DeptId", Alias = "Dept", JoinType = TableJoinType.Inner)]
    public class ConstFilterOrderWithDeptInnerView : ConstFilterOrderWithDept
    {
        [ForeignColumn("Dept", Property = nameof(ConstFilterDept.Name))]
        public string? DeptName { get; set; }
    }

    /// <summary>
    /// 左联接视图：切片外的部门保留订单行，但关联列取不到值。
    /// </summary>
    [TableJoin(typeof(ConstFilterDept), "DeptId", Alias = "Dept", JoinType = TableJoinType.Left)]
    public class ConstFilterOrderWithDeptLeftView : ConstFilterOrderWithDept
    {
        [ForeignColumn("Dept", Property = nameof(ConstFilterDept.Name))]
        public string? DeptName { get; set; }
    }
}
