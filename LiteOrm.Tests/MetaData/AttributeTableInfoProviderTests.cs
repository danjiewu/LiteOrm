using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

using LiteOrm;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class AttributeTableInfoProviderTests
    {
        [Fact]
        public void GetTableView_WithMultipleForeignTypeAttributes_BuildsMultipleJoinedTables()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(MultiForeignOrder))!;

            var ownerIdColumn = tableView.GetColumn(nameof(MultiForeignOrder.OwnerId))!;
            Assert.NotNull(ownerIdColumn);
            Assert.Equal(2, ownerIdColumn.ForeignTables.Count);
            Assert.Equal(typeof(MultiForeignUser), ownerIdColumn.ForeignTables[0].ForeignType);
            Assert.Equal("Owner", ownerIdColumn.ForeignTables[0].Alias);
            Assert.Equal(typeof(MultiForeignDepartment), ownerIdColumn.ForeignTables[1].ForeignType);
            Assert.Equal("OwnerDept", ownerIdColumn.ForeignTables[1].Alias);

            Assert.Contains(tableView.JoinedTables, jt => jt.Name == "Owner" && jt.TableDefinition.ObjectType == typeof(MultiForeignUser));
            Assert.Contains(tableView.JoinedTables, jt => jt.Name == "OwnerDept" && jt.TableDefinition.ObjectType == typeof(MultiForeignDepartment));
        }

        [Fact]
        public void GetTableView_WithSingleForeignTypeAttribute_PreservesSingleForeignCompatibility()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(SingleForeignOrder))!;

            var ownerIdColumn = tableView.GetColumn(nameof(SingleForeignOrder.OwnerId))!;
            Assert.NotNull(ownerIdColumn);
            Assert.Single(ownerIdColumn.ForeignTables);
            Assert.Equal(typeof(MultiForeignUser), ownerIdColumn.ForeignTables[0].ForeignType);
            Assert.Equal("Owner", ownerIdColumn.ForeignTables[0].Alias);
        }

        [Fact]
        public void GetTableView_WithTableJoinPrimeKeys_UsesPrimeKeyOverride()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(TableJoinPrimeKeyView))!;
            var joinedTable = Assert.Single(tableView.JoinedTables!);

            Assert.Equal("DeptByCode", joinedTable.Name);
            Assert.Equal(nameof(PrimeKeyDepartment.Code), joinedTable.ForeignPrimeKeys[0].Column.PropertyName);
            Assert.Equal(nameof(TableJoinPrimeKeyView.DepartmentCode), joinedTable.ForeignKeys[0].Column.PropertyName);

            var deptNameColumn = (ForeignColumn)tableView.GetColumn(nameof(TableJoinPrimeKeyView.DepartmentName))!;
            Assert.Equal(nameof(PrimeKeyDepartment.Name), deptNameColumn.TargetColumn!.Column.PropertyName);
        }

        [Fact]
        public void GetTableView_WithMissingTableJoinPrimeKey_ThrowsArgumentException()
        {
            var provider = CreateProvider();

            var ex = Assert.Throws<ArgumentException>(() => provider.GetTableView(typeof(InvalidTableJoinPrimeKeyView)));

            Assert.Contains("Prime key column", ex.Message);
        }

        [Fact]
        public void GetTableView_WithTableJoinAutoExpand_JoinsNestedForeignTable()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(AutoExpandUserView))!;

            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(AutoExpandUserProfile) && jt.TableDefinition.ObjectType == typeof(AutoExpandUserProfile));
            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(AutoExpandDepartment) && jt.TableDefinition.ObjectType == typeof(AutoExpandDepartment));

            var departmentNameColumn = (ForeignColumn)tableView.GetColumn(nameof(AutoExpandUserView.DepartmentName))!;
            Assert.Equal(nameof(AutoExpandDepartment.Name), departmentNameColumn.TargetColumn!.Column.PropertyName);
        }

        [Fact]
        public void GetTableView_WithTableJoinAutoExpand_RecursivelyJoinsNestedTableJoins()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(RecursiveJoinRootView))!;

            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(RecursiveJoinLevel1) && jt.TableDefinition.ObjectType == typeof(RecursiveJoinLevel1));
            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(RecursiveJoinLevel2) && jt.TableDefinition.ObjectType == typeof(RecursiveJoinLevel2));
            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(RecursiveJoinLevel3) && jt.TableDefinition.ObjectType == typeof(RecursiveJoinLevel3));

            // 每一级的外键都来自声明该关联的上级表，保证 JOIN 依赖顺序正确
            var level1 = tableView.JoinedTables.Single(jt => jt.Name == nameof(RecursiveJoinLevel1));
            var level2 = tableView.JoinedTables.Single(jt => jt.Name == nameof(RecursiveJoinLevel2));
            var level3 = tableView.JoinedTables.Single(jt => jt.Name == nameof(RecursiveJoinLevel3));
            Assert.Null(level1.ForeignKeys[0].Table);
            Assert.Same(level1, level2.ForeignKeys[0].Table);
            Assert.Same(level2, level3.ForeignKeys[0].Table);
            Assert.Equal(nameof(RecursiveJoinLevel1.Level2Id), level2.ForeignKeys[0].Column.PropertyName);
            Assert.Equal(nameof(RecursiveJoinLevel2.Level3Id), level3.ForeignKeys[0].Column.PropertyName);

            var level3Name = (ForeignColumn)tableView.GetColumn(nameof(RecursiveJoinRootView.Level3Name))!;
            Assert.Equal(nameof(RecursiveJoinLevel3.Name), level3Name.TargetColumn!.Column.PropertyName);
        }

        [Fact]
        public void GetTableView_WithCyclicAutoExpandTableJoins_DoesNotJoinRootTableAgain()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(CyclicJoinRootView))!;

            Assert.Contains(tableView.JoinedTables, jt => jt.Name == nameof(CyclicJoinOther));
            Assert.DoesNotContain(tableView.JoinedTables, jt => jt.TableDefinition.ObjectType == typeof(CyclicJoinRootView));
        }

        [Fact]
        public void GetTableDefinition_WithEnumNameConstant_BuildsConstFilter()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(EnumNameConstantOrder))!;

            var stateColumn = tableDef.GetColumn(nameof(EnumNameConstantOrder.State))!;
            Assert.Equal(ConstFilterState.Enabled, stateColumn.Constant);
            Assert.NotNull(tableDef.ConstFilter);
        }

        [Fact]
        public void GetTableDefinition_WithEnumIntConstant_BuildsConstFilter()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(EnumIntConstantOrder))!;

            var stateColumn = tableDef.GetColumn(nameof(EnumIntConstantOrder.State))!;
            Assert.Equal(ConstFilterState.Enabled, stateColumn.Constant);
            Assert.NotNull(tableDef.ConstFilter);
        }

        [Fact]
        public void GetTableDefinition_WithEnumMemberConstant_BuildsConstFilter()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(EnumMemberConstantOrder))!;

            var stateColumn = tableDef.GetColumn(nameof(EnumMemberConstantOrder.State))!;
            Assert.Equal(ConstFilterState.Enabled, stateColumn.Constant);
            Assert.NotNull(tableDef.ConstFilter);
        }

        [Fact]
        public void GetTableDefinition_WithNonEnumStringNumericConstant_ConvertsToPropertyType()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(IntStringConstantOrder))!;

            var statusColumn = tableDef.GetColumn(nameof(IntStringConstantOrder.Status))!;
            Assert.Equal(1, statusColumn.Constant);
            Assert.NotNull(tableDef.ConstFilter);
        }

        [Fact]
        public void GetTableDefinition_WithNonEnumBoolConstant_BuildsConstFilter()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(BoolConstantOrder))!;

            var deletedColumn = tableDef.GetColumn(nameof(BoolConstantOrder.IsDeleted))!;
            Assert.Equal(false, deletedColumn.Constant);
            Assert.NotNull(tableDef.ConstFilter);
        }

        [Fact]
        public void GetTableView_WithJoinedEnumConstant_BuildsAliasedJoinedConstFilter()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(ConstFilterJoinOrder))!;
            var joinedTable = Assert.Single(tableView.JoinedTables!);

            Assert.NotNull(joinedTable.ConstFilter);

            TableInfoProvider currentProvider = TableInfoProvider.Instance;
            try
            {
                TableInfoProvider.Set(() => provider);
                var context = new SqlBuildContext(SqlBuilder.Instance, tableView, "T", null) { SingleTable = false };
                context.AddTableAlias(joinedTable.Name!, joinedTable.TableDefinition);
                var sql = joinedTable.ConstFilter.ToPreparedSql(context);

                Assert.Contains(joinedTable.Name!, sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(((int)ConstFilterState.Enabled).ToString(), sql.Sql);
                Assert.Empty(sql.Params);
            }
            finally
            {
                TableInfoProvider.Set(() => currentProvider);
            }
        }

        [Fact]
        public void GetTableDefinition_WithAutoGenerateColumnsDisabled_OnlyExplicitColumnsMapped()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(StrictAutoColumnOrder))!;

            Assert.Equal(2, tableDef.Columns.Count);
            Assert.NotNull(tableDef.GetColumn(nameof(StrictAutoColumnOrder.Id)));
            Assert.NotNull(tableDef.GetColumn(nameof(StrictAutoColumnOrder.ExplicitName)));
            Assert.Null(tableDef.GetColumn(nameof(StrictAutoColumnOrder.ImplicitName)));
            Assert.Null(tableDef.GetColumn(nameof(StrictAutoColumnOrder.ImplicitCount)));
        }

        [Fact]
        public void GetTableView_WithAutoGenerateColumnsDisabled_KeepsForeignColumn()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(StrictAutoColumnView))!;

            Assert.NotNull(tableView.GetColumn(nameof(StrictAutoColumnView.DepartmentCode)));
            Assert.NotNull(tableView.GetColumn(nameof(StrictAutoColumnView.DepartmentName)));
            Assert.Null(tableView.GetColumn(nameof(StrictAutoColumnView.ImplicitNote)));
        }

        [Fact]
        public void GetTableDefinition_WithAutoGenerateColumnsDefault_MapsScalarProperties()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(DefaultAutoColumnOrder))!;

            Assert.NotNull(tableDef.GetColumn(nameof(DefaultAutoColumnOrder.Name)));
        }

        [Fact]
        public void GetTableView_FirstAccess_PublishesSameViewInstanceToDefinition()
        {
            var provider = CreateProvider();

            var tableView = provider.GetTableView(typeof(ConcurrencyProbeView))!;
            var tableDef = provider.GetTableDefinition(typeof(ConcurrencyProbeView))!;

            // 表定义回填的视图必须就是缓存返回的视图（修复前会生成两个 TableView 实例）
            Assert.Same(tableView, tableDef.View);
            // 属性名与列名不同时，必须按特性映射解析列名，不能退化为属性名
            Assert.Equal("UserName", tableView.GetColumn("username")!.Name);
        }

        [Fact]
        public void GetTableDefinition_FirstAccess_AttachesSameViewAsGetTableView()
        {
            var provider = CreateProvider();

            var tableDef = provider.GetTableDefinition(typeof(ConcurrencyProbeView))!;

            Assert.NotNull(tableDef.View);
            Assert.Same(tableDef.View, provider.GetTableView(typeof(ConcurrencyProbeView)));
        }

        [Fact]
        public void GetTableView_ConcurrentFirstAccess_ReturnsSingleCachedInstance()
        {
            var provider = CreateProvider();
            var views = new TableView[64];

            Parallel.For(0, views.Length, i => views[i] = provider.GetTableView(typeof(ConcurrencyProbeView))!);

            var expected = views[0];
            Assert.All(views, view => Assert.Same(expected, view));
            Assert.Same(expected, provider.GetTableDefinition(typeof(ConcurrencyProbeView))!.View);
            Assert.Equal("UserName", expected.GetColumn("username")!.Name);
        }

        [Fact]
        public void TableInfoProvider_Set_WithNullFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => TableInfoProvider.Set(null!));
        }

        private static AttributeTableInfoProvider CreateProvider()
        {
            return new AttributeTableInfoProvider();
        }

        /// <summary>
        /// 属性名（username）与列名（UserName）不同，用于验证并发构建视图后列解析仍走元数据映射。
        /// </summary>
        [Table("ConcurrencyProbeViews", SyncTable = SyncTableMode.Never)]
        private class ConcurrencyProbeView
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("UserName")]
            public string username { get; set; } = string.Empty;

            [Column("OwnerId")]
            [ForeignType(typeof(MultiForeignUser), Alias = "Owner")]
            public int OwnerId { get; set; }
        }

        [Table("Orders")]
        private class MultiForeignOrder
        {
            [Column("OwnerId")]
            [ForeignType(typeof(MultiForeignUser), Alias = "Owner")]
            [ForeignType(typeof(MultiForeignDepartment), Alias = "OwnerDept")]
            public int OwnerId { get; set; }
        }

        [Table("Orders")]
        private class SingleForeignOrder
        {
            [Column("OwnerId")]
            [ForeignType(typeof(MultiForeignUser), Alias = "Owner")]
            public int OwnerId { get; set; }
        }

        [Table("Users")]
        private class MultiForeignUser
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }
        }

        [Table("Departments")]
        private class MultiForeignDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }
        }

        [Table("PrimeKeyDepartments")]
        private class PrimeKeyDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Code")]
            public string Code { get; set; } = string.Empty;

            [Column("Name")]
            public string Name { get; set; } = string.Empty;
        }

        [Table("TableJoinPrimeKeyViews")]
        [TableJoin(typeof(PrimeKeyDepartment), nameof(TableJoinPrimeKeyView.DepartmentCode), Alias = "DeptByCode", PrimeKeys = nameof(PrimeKeyDepartment.Code))]
        private class TableJoinPrimeKeyView
        {
            [Column("DepartmentCode")]
            public string DepartmentCode { get; set; } = string.Empty;

            [ForeignColumn("DeptByCode", Property = nameof(PrimeKeyDepartment.Name))]
            public string? DepartmentName { get; set; }
        }

        [Table("InvalidTableJoinPrimeKeyViews", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(PrimeKeyDepartment), nameof(InvalidTableJoinPrimeKeyView.DepartmentCode), PrimeKeys = "MissingCode")]
        private class InvalidTableJoinPrimeKeyView
        {
            [Column("DepartmentCode")]
            public string DepartmentCode { get; set; } = string.Empty;
        }

        /// <summary>
        /// 通过类级 <see cref="TableJoinAttribute.AutoExpand"/> 关联用户档案，
        /// 再由档案自身的 <see cref="ForeignTypeAttribute"/> 关联部门。
        /// </summary>
        [Table("AutoExpandUserViews", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(AutoExpandUserProfile), nameof(AutoExpandUserView.ProfileId), AutoExpand = true)]
        private class AutoExpandUserView
        {
            [Column("ProfileId")]
            public int ProfileId { get; set; }

            [ForeignColumn(typeof(AutoExpandUserProfile), Property = nameof(AutoExpandUserProfile.DepartmentId))]
            public int DepartmentId { get; set; }

            [ForeignColumn(typeof(AutoExpandDepartment), Property = nameof(AutoExpandDepartment.Name))]
            public string? DepartmentName { get; set; }
        }

        [Table("AutoExpandUserProfiles", SyncTable = SyncTableMode.Never)]
        private class AutoExpandUserProfile
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("DepartmentId")]
            [ForeignType(typeof(AutoExpandDepartment))]
            public int DepartmentId { get; set; }
        }

        [Table("AutoExpandDepartments", SyncTable = SyncTableMode.Never)]
        private class AutoExpandDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// 类级 [TableJoin] 多级链：根视图 → Level1 → Level2 → Level3，中间两级靠 AutoExpand 逐级展开。
        /// </summary>
        [Table("RecursiveJoinRootViews", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(RecursiveJoinLevel1), nameof(RecursiveJoinRootView.Level1Id), AutoExpand = true)]
        private class RecursiveJoinRootView
        {
            [Column("Level1Id")]
            public int Level1Id { get; set; }

            [ForeignColumn(typeof(RecursiveJoinLevel3), Property = nameof(RecursiveJoinLevel3.Name))]
            public string? Level3Name { get; set; }
        }

        [Table("RecursiveJoinLevel1s", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(RecursiveJoinLevel2), nameof(RecursiveJoinLevel1.Level2Id), AutoExpand = true)]
        private class RecursiveJoinLevel1
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Level2Id")]
            public int Level2Id { get; set; }
        }

        [Table("RecursiveJoinLevel2s", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(RecursiveJoinLevel3), nameof(RecursiveJoinLevel2.Level3Id))]
        private class RecursiveJoinLevel2
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Level3Id")]
            public int Level3Id { get; set; }
        }

        [Table("RecursiveJoinLevel3s", SyncTable = SyncTableMode.Never)]
        private class RecursiveJoinLevel3
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// 关联环：根视图 → Other → 根视图，递归展开在主表类型处终止，不产生自连接。
        /// </summary>
        [Table("CyclicJoinRootViews", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(CyclicJoinOther), nameof(CyclicJoinRootView.OtherId), AutoExpand = true)]
        private class CyclicJoinRootView
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("OtherId")]
            public int OtherId { get; set; }
        }

        [Table("CyclicJoinOthers", SyncTable = SyncTableMode.Never)]
        [TableJoin(typeof(CyclicJoinRootView), nameof(CyclicJoinOther.RootId), AutoExpand = true)]
        private class CyclicJoinOther
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("RootId")]
            public int RootId { get; set; }
        }

        private enum ConstFilterState
        {
            Disabled = 0,
            Enabled = 1
        }

        [Table("EnumNameConstantOrders")]
        private class EnumNameConstantOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("State", Constant = "Enabled")]
            public ConstFilterState State { get; set; }
        }

        [Table("EnumIntConstantOrders")]
        private class EnumIntConstantOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("State", Constant = 1)]
            public ConstFilterState State { get; set; }
        }

        [Table("EnumMemberConstantOrders")]
        private class EnumMemberConstantOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("State", Constant = ConstFilterState.Enabled)]
            public ConstFilterState State { get; set; }
        }

        [Table("IntStringConstantOrders")]
        private class IntStringConstantOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Status", Constant = "1")]
            public int Status { get; set; }
        }

        [Table("BoolConstantOrders")]
        private class BoolConstantOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("IsDeleted", Constant = false)]
            public bool IsDeleted { get; set; }
        }

        [Table("ConstFilterJoinOrders")]
        private class ConstFilterJoinOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("DepartmentId")]
            [ForeignType(typeof(ConstFilterDepartment), Alias = "Dept")]
            public int DepartmentId { get; set; }
        }

        [Table("ConstFilterDepartments")]
        private class ConstFilterDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("State", Constant = "Enabled")]
            public ConstFilterState State { get; set; }
        }

        /// <summary>
        /// 关闭自动生成列的表：只有显式标注 <see cref="ColumnAttribute"/> 的属性才映射为列。
        /// </summary>
        [Table("StrictAutoColumnOrders", AutoGenerateColumns = false)]
        private class StrictAutoColumnOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("ExplicitName")]
            public string ExplicitName { get; set; } = string.Empty;

            public string ImplicitName { get; set; } = string.Empty;

            public int ImplicitCount { get; set; }
        }

        /// <summary>
        /// 未声明 <see cref="TableAttribute.AutoGenerateColumns"/> 的表，保持默认自动映射标量属性。
        /// </summary>
        [Table("DefaultAutoColumnOrders")]
        private class DefaultAutoColumnOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            public string? Name { get; set; }
        }

        /// <summary>
        /// 关闭自动生成列的视图：未标注 <see cref="ColumnAttribute"/> 的属性不映射，
        /// 标注 <see cref="ForeignColumnAttribute"/> 的属性仍作为外键列参与关联查询。
        /// </summary>
        [Table("StrictAutoColumnViews", AutoGenerateColumns = false)]
        [TableJoin(typeof(StrictAutoColumnDepartment), nameof(StrictAutoColumnView.DepartmentCode), Alias = "Dept", PrimeKeys = nameof(StrictAutoColumnDepartment.Code))]
        private class StrictAutoColumnView
        {
            [Column("DepartmentCode")]
            public string DepartmentCode { get; set; } = string.Empty;

            [ForeignColumn("Dept", Property = nameof(StrictAutoColumnDepartment.Name))]
            public string? DepartmentName { get; set; }

            public string? ImplicitNote { get; set; }
        }

        [Table("StrictAutoColumnDepartments")]
        private class StrictAutoColumnDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Code")]
            public string Code { get; set; } = string.Empty;

            [Column("Name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
