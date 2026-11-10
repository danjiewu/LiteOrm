using System.Collections.Generic;
using System.Linq;

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
        public void GetTableView_WithJoinedTableAutoExpand_RecursivelyResolvesNestedJoins()
        {
            var provider = new CommonTableInfoProvider();
            provider.Register(CreateRecursiveRootInfo());
            provider.Register(CreateRecursiveLevel1Info());
            provider.Register(CreateRecursiveLevel2Info());
            provider.Register(CreateRecursiveLevel3Info());

            var view = provider.GetTableView(typeof(AotRecursiveRootView))!;

            Assert.Contains(view.JoinedTables, jt => jt.Name == nameof(AotRecursiveLevel1) && jt.TableDefinition.ObjectType == typeof(AotRecursiveLevel1));
            Assert.Contains(view.JoinedTables, jt => jt.Name == nameof(AotRecursiveLevel2) && jt.TableDefinition.ObjectType == typeof(AotRecursiveLevel2));
            Assert.Contains(view.JoinedTables, jt => jt.Name == nameof(AotRecursiveLevel3) && jt.TableDefinition.ObjectType == typeof(AotRecursiveLevel3));

            var level1 = view.JoinedTables.Single(jt => jt.Name == nameof(AotRecursiveLevel1));
            var level2 = view.JoinedTables.Single(jt => jt.Name == nameof(AotRecursiveLevel2));
            var level3 = view.JoinedTables.Single(jt => jt.Name == nameof(AotRecursiveLevel3));
            Assert.Null(level1.ForeignKeys[0].Table);
            Assert.Same(level1, level2.ForeignKeys[0].Table);
            Assert.Same(level2, level3.ForeignKeys[0].Table);
            Assert.Equal(nameof(AotRecursiveLevel1.Level2Id), level2.ForeignKeys[0].Column.PropertyName);
            Assert.Equal(nameof(AotRecursiveLevel2.Level3Id), level3.ForeignKeys[0].Column.PropertyName);

            var level3Name = (ForeignColumn)view.GetColumn(nameof(AotRecursiveRootView.Level3Name))!;
            Assert.Equal(nameof(AotRecursiveLevel3.Name), level3Name.TargetColumn!.Column.PropertyName);
        }

        private static TableInfo CreateRecursiveRootInfo()
        {
            return new TableInfo(
                typeof(AotRecursiveRootView),
                "AotRecursiveRootViews",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotRecursiveRootView.Level1Id), ColumnName = "Level1Id" },
                    new ColumnInfo
                    {
                        PropertyName = nameof(AotRecursiveRootView.Level3Name),
                        ColumnName = "Level3Name",
                        ForeignColumn = new ForeignColumnInfo
                        {
                            ForeignType = typeof(AotRecursiveLevel3),
                            Property = nameof(AotRecursiveLevel3.Name)
                        }
                    }
                })
            {
                JoinedTables = new[]
                {
                    new JoinedTableInfo
                    {
                        TargetType = typeof(AotRecursiveLevel1),
                        ForeignKeys = nameof(AotRecursiveRootView.Level1Id),
                        AutoExpand = true
                    }
                }
            };
        }

        private static TableInfo CreateRecursiveLevel1Info()
        {
            return new TableInfo(
                typeof(AotRecursiveLevel1),
                "AotRecursiveLevel1s",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel1.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel1.Level2Id), ColumnName = "Level2Id" }
                })
            {
                JoinedTables = new[]
                {
                    new JoinedTableInfo
                    {
                        TargetType = typeof(AotRecursiveLevel2),
                        ForeignKeys = nameof(AotRecursiveLevel1.Level2Id),
                        AutoExpand = true
                    }
                }
            };
        }

        private static TableInfo CreateRecursiveLevel2Info()
        {
            return new TableInfo(
                typeof(AotRecursiveLevel2),
                "AotRecursiveLevel2s",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel2.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel2.Level3Id), ColumnName = "Level3Id" }
                })
            {
                JoinedTables = new[]
                {
                    new JoinedTableInfo
                    {
                        TargetType = typeof(AotRecursiveLevel3),
                        ForeignKeys = nameof(AotRecursiveLevel2.Level3Id)
                    }
                }
            };
        }

        private static TableInfo CreateRecursiveLevel3Info()
        {
            return new TableInfo(
                typeof(AotRecursiveLevel3),
                "AotRecursiveLevel3s",
                null,
                SyncTableMode.Never,
                new List<ColumnInfo>
                {
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel3.Id), ColumnName = "Id", IsPrimaryKey = true },
                    new ColumnInfo { PropertyName = nameof(AotRecursiveLevel3.Name), ColumnName = "Name" }
                });
        }

        private class AotRecursiveRootView
        {
            public int Level1Id { get; set; }

            public string? Level3Name { get; set; }
        }

        private class AotRecursiveLevel1
        {
            public int Id { get; set; }

            public int Level2Id { get; set; }
        }

        private class AotRecursiveLevel2
        {
            public int Id { get; set; }

            public int Level3Id { get; set; }
        }

        private class AotRecursiveLevel3
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;
        }

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
