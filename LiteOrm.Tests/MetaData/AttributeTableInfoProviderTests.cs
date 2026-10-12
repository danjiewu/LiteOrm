using System;
using System.Data.Common;

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

        private static AttributeTableInfoProvider CreateProvider()
        {
            return new AttributeTableInfoProvider();
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
