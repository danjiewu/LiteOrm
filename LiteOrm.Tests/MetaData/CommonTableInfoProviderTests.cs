using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// <see cref="CommonTableInfoProvider"/> 的 AOT 元数据装配测试。
    /// </summary>
    public class CommonTableInfoProviderTests
    {
        [Fact]
        public void GetTableDefinition_WithColumnConstant_BuildsConstFilter()
        {
            var provider = new CommonTableInfoProvider();
            provider.Register(CreateTableInfo());

            var table = provider.GetTableDefinition(typeof(ConstantFilterEntity));

            Assert.NotNull(table);
            Assert.NotNull(table!.ConstFilter);
            Assert.False((bool)table.GetColumn(nameof(ConstantFilterEntity.IsDeleted))!.Constant!);
            // 整数常量按属性类型对齐成枚举
            Assert.Equal(RecordState.Enabled, (RecordState)table.GetColumn(nameof(ConstantFilterEntity.State))!.Constant!);
        }

        [Fact]
        public void GetTableDefinition_ColumnConstant_MatchesAttributeProvider()
        {
            var aotProvider = new CommonTableInfoProvider();
            aotProvider.Register(CreateTableInfo());
            var aotTable = aotProvider.GetTableDefinition(typeof(ConstantFilterEntity));

            var attributeTable = new AttributeTableInfoProvider().GetTableDefinition(typeof(ConstantFilterEntity));

            Assert.NotNull(aotTable);
            Assert.NotNull(attributeTable);
            Assert.Equal(
                attributeTable!.GetColumn(nameof(ConstantFilterEntity.IsDeleted))!.Constant,
                aotTable!.GetColumn(nameof(ConstantFilterEntity.IsDeleted))!.Constant);
            Assert.Equal(
                attributeTable.GetColumn(nameof(ConstantFilterEntity.State))!.Constant,
                aotTable.GetColumn(nameof(ConstantFilterEntity.State))!.Constant);
        }

        [Fact]
        public void GetTableDefinition_WithoutColumnConstant_LeavesConstFilterNull()
        {
            var provider = new CommonTableInfoProvider();
            provider.Register(new TableInfo(
                typeof(PlainEntity),
                "PlainEntities",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(PlainEntity.Id), ColumnName = "Id", IsPrimaryKey = true }
                }));

            var table = provider.GetTableDefinition(typeof(PlainEntity));

            Assert.NotNull(table);
            Assert.Null(table!.ConstFilter);
        }

        private static TableInfo CreateTableInfo()
        {
            return new TableInfo(
                typeof(ConstantFilterEntity),
                "ConstantFilterEntities",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(ConstantFilterEntity.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo { PropertyName = nameof(ConstantFilterEntity.IsDeleted), ColumnName = "IsDeleted", Constant = false },
                    new ColumnInfo { PropertyName = nameof(ConstantFilterEntity.State), ColumnName = "State", Constant = 1 }
                });
        }

        [Fact]
        public void GetTableView_WithJoinedTableAutoExpand_ResolvesNestedForeignColumn()
        {
            var provider = new CommonTableInfoProvider();
            provider.Register(CreateAutoExpandViewInfo());
            provider.Register(new TableInfo(
                typeof(AotAutoExpandUserProfile),
                "AotAutoExpandUserProfiles",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotAutoExpandUserProfile.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo
                    {
                        PropertyName = nameof(AotAutoExpandUserProfile.DepartmentId),
                        ColumnName = "DepartmentId",
                        ForeignTables = new[] { new ForeignTable { ForeignType = typeof(AotAutoExpandDepartment) } }
                    }
                }));
            provider.Register(new TableInfo(
                typeof(AotAutoExpandDepartment),
                "AotAutoExpandDepartments",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotAutoExpandDepartment.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo { PropertyName = nameof(AotAutoExpandDepartment.Name), ColumnName = "Name" }
                }));

            var view = provider.GetTableView(typeof(AotAutoExpandUserView))!;

            Assert.Contains(view.JoinedTables, jt => jt.Name == nameof(AotAutoExpandUserProfile) && jt.TableDefinition.ObjectType == typeof(AotAutoExpandUserProfile));
            Assert.Contains(view.JoinedTables, jt => jt.Name == nameof(AotAutoExpandDepartment) && jt.TableDefinition.ObjectType == typeof(AotAutoExpandDepartment));

            var departmentName = (ForeignColumn)view.GetColumn(nameof(AotAutoExpandUserView.DepartmentName))!;
            Assert.Equal(nameof(AotAutoExpandDepartment.Name), departmentName.TargetColumn!.Column.PropertyName);
        }

        private static TableInfo CreateAutoExpandViewInfo()
        {
            return new TableInfo(
                typeof(AotAutoExpandUserView),
                "AotAutoExpandUserViews",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotAutoExpandUserView.ProfileId), ColumnName = "ProfileId" },
                    new ColumnInfo
                    {
                        PropertyName = nameof(AotAutoExpandUserView.DepartmentName),
                        ColumnName = "DepartmentName",
                        ForeignColumn = new ForeignColumnInfo
                        {
                            ForeignType = typeof(AotAutoExpandDepartment),
                            Property = nameof(AotAutoExpandDepartment.Name)
                        }
                    }
                })
            {
                JoinedTables = new[]
                {
                    new JoinedTableInfo
                    {
                        TargetType = typeof(AotAutoExpandUserProfile),
                        ForeignKeys = nameof(AotAutoExpandUserView.ProfileId),
                        AutoExpand = true
                    }
                }
            };
        }

        private class AotAutoExpandUserView
        {
            public int ProfileId { get; set; }

            public string? DepartmentName { get; set; }
        }

        private class AotAutoExpandUserProfile
        {
            public int Id { get; set; }

            public int DepartmentId { get; set; }
        }

        private class AotAutoExpandDepartment
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

        [Table("ConstantFilterEntities", SyncTable = SyncTableMode.Never)]
        private class ConstantFilterEntity
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("IsDeleted", Constant = false)]
            public bool IsDeleted { get; set; }

            [Column("State", Constant = RecordState.Enabled)]
            public RecordState State { get; set; }
        }

        [Table("PlainEntities", SyncTable = SyncTableMode.Never)]
        private class PlainEntity
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }
        }

        private enum RecordState
        {
            Disabled = 0,
            Enabled = 1
        }
    }
}
