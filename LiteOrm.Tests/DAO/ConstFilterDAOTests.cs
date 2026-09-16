using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证 <c>[Column(Constant = ...)]</c> 收敛出的表固定筛选条件同样作用于 DAO 的主键读写路径
    /// （GetObject / ExistsKey / Update / Delete / BatchUpdate / BatchDelete / BatchUpdateOrInsert），
    /// 而不仅是表达式查询路径。
    /// <para>
    /// 模型 <see cref="ConstFilterOrder"/> 只“看得见” State = Enabled 的行，
    /// 因此 State = Disabled 的行对 DAO 不可见，也不能被这些方法更新或删除。
    /// </para>
    /// </summary>
    [Collection("Database")]
    public class ConstFilterDAOTests : TestBase
    {
        public ConstFilterDAOTests(DatabaseFixture fixture) : base(fixture) { }

        private ObjectDAO<ConstFilterOrder> ObjectDao => ServiceProvider.GetRequiredService<ObjectDAO<ConstFilterOrder>>();
        private ObjectViewDAO<ConstFilterOrder> ViewDao => ServiceProvider.GetRequiredService<ObjectViewDAO<ConstFilterOrder>>();

        [Fact]
        public async Task GetObject_And_ExistsKey_SeeOnlyRowsMatchingConstFilter()
        {
            var visible = NewOrder("Visible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("Hidden", ConstFilterOrderState.Disabled);

            var found = await ViewDao.GetObject(visible.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(found);
            Assert.Equal("Visible", found.Name);
            Assert.Equal(ConstFilterOrderState.Enabled, found.State);

            Assert.Null(await ViewDao.GetObject(hidden.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken));

            Assert.True(await ViewDao.ExistsKey(visible.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await ViewDao.ExistsKey(hidden.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Update_DoesNotTouchRowsOutsideConstFilter()
        {
            var visible = NewOrder("BeforeVisible", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("BeforeHidden", ConstFilterOrderState.Disabled);

            visible.Name = "AfterVisible";
            Assert.True(ObjectDao.Update(visible));

            hidden.Name = "AfterHidden";
            Assert.False(ObjectDao.Update(hidden));

            var rows = await ReadRawRowsAsync();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Id == visible.Id && row.Name == "AfterVisible");
            Assert.Contains(rows, row => row.Id == hidden.Id && row.Name == "BeforeHidden");
        }

        [Fact]
        public async Task Delete_DoesNotRemoveRowsOutsideConstFilter()
        {
            var visible = NewOrder("ToDelete", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("ToKeep", ConstFilterOrderState.Disabled);

            Assert.False(ObjectDao.DeleteByKeys(hidden.Id));
            Assert.False(ObjectDao.Delete(hidden));
            Assert.True(ObjectDao.DeleteByKeys(visible.Id));

            var remaining = Assert.Single(await ReadRawRowsAsync());
            Assert.Equal(hidden.Id, remaining.Id);
            Assert.Equal("ToKeep", remaining.Name);
        }

        [Fact]
        public async Task BatchWrite_DoesNotTouchRowsOutsideConstFilter()
        {
            var toDelete = NewOrder("BatchA", ConstFilterOrderState.Enabled);
            var toUpdate = NewOrder("BatchB", ConstFilterOrderState.Enabled);
            var hidden = NewOrder("BatchC", ConstFilterOrderState.Disabled);

            toUpdate.Name = "BatchB_Updated";
            hidden.Name = "BatchC_Updated";
            ObjectDao.BatchUpdate([toUpdate, hidden]);

            ObjectDao.BatchDeleteByKeys(new object[] { toDelete.Id, hidden.Id });

            var rows = await ReadRawRowsAsync();
            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.Id == toUpdate.Id && row.Name == "BatchB_Updated");
            // 固定筛选之外的行既不参与更新也不参与删除
            Assert.Contains(rows, row => row.Id == hidden.Id && row.Name == "BatchC");
            Assert.DoesNotContain(rows, row => row.Id == toDelete.Id);
        }

        [Fact]
        public async Task BatchUpdateOrInsert_MatchingRowIsUpdatedInsteadOfDuplicated()
        {
            var order = NewOrder("Upsert", ConstFilterOrderState.Enabled);

            order.Name = "Upsert_Updated";
            ObjectDao.BatchUpdateOrInsert([order]);

            var remaining = Assert.Single(await ReadRawRowsAsync());
            Assert.Equal(order.Id, remaining.Id);
            Assert.Equal("Upsert_Updated", remaining.Name);
        }

        /// <summary>
        /// 声明了固定筛选的表不复用命令缓存，每次调用都新建命令，且新建的命令不占用缓存槽位。
        /// 因此运行时改掉 <see cref="TableDefinition.ConstFilter"/> 后，下一次调用立即按新条件执行，
        /// 而先前由常规（无固定筛选）调用建立的缓存命令原样保留，不会被固定筛选的参数覆盖。
        /// </summary>
        [Fact]
        public async Task ConstFilter_ChangedAtRuntime_TakesEffectWithoutPollutingCommandCache()
        {
            var tableDefinition = TableInfoProvider.Instance.GetTableDefinition(typeof(ConstFilterOrder))!;
            var originalFilter = tableDefinition.ConstFilter;
            var ct = TestContext.Current.CancellationToken;
            var cacheKey = (typeof(ConstFilterOrder), "GetObject");
            var preparedCommands = ViewDao.GetDaoContext().PreparedCommands;
            preparedCommands.TryRemove(cacheKey, out _);
            try
            {
                var enabled = NewOrder("RuntimeEnabled", ConstFilterOrderState.Enabled);
                var disabled = NewOrder("RuntimeDisabled", ConstFilterOrderState.Disabled);

                // 先去掉固定筛选，让常规路径建立缓存命令
                tableDefinition.ConstFilter = null;
                Assert.NotNull(await ViewDao.GetObject(disabled.Id).FirstOrDefaultAsync(ct));

                var cachedCommand = preparedCommands[cacheKey];
                // 缓存里存的是底层命令本身，代理按次创建
                Assert.IsNotType<DbCommandProxy>(cachedCommand);
                string cachedSql = cachedCommand.CommandText;
                int cachedParamCount = cachedCommand.Parameters.Count;

                // 运行时设置固定筛选条件
                tableDefinition.ConstFilter = Expr.Prop("State") == Expr.Const(ConstFilterOrderState.Disabled);

                Assert.Null(await ViewDao.GetObject(enabled.Id).FirstOrDefaultAsync(ct));
                Assert.NotNull(await ViewDao.GetObject(disabled.Id).FirstOrDefaultAsync(ct));

                // 固定筛选的命令另起一条路径，既不读缓存也不写缓存，常规命令缓存保持原样
                Assert.Same(cachedCommand, preparedCommands[cacheKey]);
                Assert.Equal(cachedSql, cachedCommand.CommandText);
                Assert.Equal(cachedParamCount, cachedCommand.Parameters.Count);

                // 去掉固定筛选后，常规路径继续使用缓存命令
                tableDefinition.ConstFilter = null;
                Assert.Same(cachedCommand, ViewDao.GetDaoContext().PreparedCommands[cacheKey]);
                Assert.NotNull(await ViewDao.GetObject(enabled.Id).FirstOrDefaultAsync(ct));
            }
            finally
            {
                tableDefinition.ConstFilter = originalFilter;
            }
        }

        private ConstFilterOrder NewOrder(string name, ConstFilterOrderState state)
        {
            var order = new ConstFilterOrder { Name = name, State = state };
            Assert.True(ObjectDao.Insert(order));
            Assert.True(order.Id > 0);
            return order;
        }

        /// <summary>
        /// 绕过模型直接读原始行，用于断言被固定筛选挡住的行的真实内容。
        /// 固定筛选是模型层规则，完整 SQL 通道不注入条件。
        /// </summary>
        private async Task<List<ConstFilterOrder>> ReadRawRowsAsync()
        {
            var rows = ViewDao.Search($"SELECT \"Id\", \"Name\", \"State\" FROM \"DaoConstFilterOrders\" ORDER BY \"Id\"", isFull: true);
            return await rows.ToListAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 固定筛选列取值。
    /// </summary>
    public enum ConstFilterOrderState
    {
        /// <summary>可见。</summary>
        Enabled = 1,
        /// <summary>对模型不可见。</summary>
        Disabled = 2
    }

    /// <summary>
    /// 带固定筛选列的订单模型：State 固定为 Enabled，State = Disabled 的行不可见。
    /// </summary>
    [Table("DaoConstFilterOrders")]
    public class ConstFilterOrder
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name", AllowNull = true)]
        public string? Name { get; set; }

        [Column("State", Constant = ConstFilterOrderState.Enabled)]
        public ConstFilterOrderState State { get; set; }
    }
}
