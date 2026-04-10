using System;
using System.Data.Common;

using LiteOrm;
using LiteOrm.Common;
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

            var tableView = provider.GetTableView(typeof(MultiForeignOrder));

            var ownerIdColumn = tableView.GetColumn(nameof(MultiForeignOrder.OwnerId));
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

            var tableView = provider.GetTableView(typeof(SingleForeignOrder));

            var ownerIdColumn = tableView.GetColumn(nameof(SingleForeignOrder.OwnerId));
            Assert.NotNull(ownerIdColumn);
            Assert.Single(ownerIdColumn.ForeignTables);
            Assert.Equal(typeof(MultiForeignUser), ownerIdColumn.ForeignTables[0].ForeignType);
            Assert.Equal("Owner", ownerIdColumn.ForeignTables[0].Alias);
        }

        private static AttributeTableInfoProvider CreateProvider()
        {
            var sqlBuilderFactory = new Mock<ISqlBuilderFactory>();
            sqlBuilderFactory
                .Setup(factory => factory.GetSqlBuilder(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(SqlBuilder.Instance);

            var dataSourceProvider = new Mock<IDataSourceProvider>();
            dataSourceProvider.SetupGet(provider => provider.DefaultDataSourceName).Returns("default");
            dataSourceProvider
                .Setup(provider => provider.GetDataSource(It.IsAny<string>()))
                .Returns(new DataSourceConfig
                {
                    Name = "default",
                    Provider = typeof(DbConnection).AssemblyQualifiedName!
                });

            return new AttributeTableInfoProvider(sqlBuilderFactory.Object, dataSourceProvider.Object);
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
    }
}
