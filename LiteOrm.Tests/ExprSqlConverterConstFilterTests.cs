using System;
using System.Collections.Generic;
using System.Data.Common;

using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ExprSqlConverterConstFilterTests
    {
        [Fact]
        public void ToPreparedSql_Select_AppendsMainConstFilter()
        {
            var provider = CreateProvider();

            RunWithProvider(provider, () =>
            {
                var expr = new SelectExpr { Source = new FromExpr(typeof(ConstFilterOrder)) };

                var sql = expr.ToPreparedSql(new SqlBuildContext() { SingleTable = false }, SqlBuilder.Instance);

                Assert.Contains("WHERE", sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(sql.Params, param => Equals(param.Value, ConstFilterState.Enabled));
            });
        }

        [Fact]
        public void ToPreparedSql_SelectWithMetadataJoin_AppendsJoinedConstFilterToOn()
        {
            var provider = CreateProvider();

            RunWithProvider(provider, () =>
            {
                var expr = new SelectExpr { Source = new FromExpr(typeof(ConstFilterJoinOrderView)) };

                var sql = expr.ToPreparedSql(new SqlBuildContext() { SingleTable = false }, SqlBuilder.Instance);

                Assert.Contains("JOIN", sql.Sql);
                Assert.Contains("Dept", sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(sql.Params, param => Equals(param.Value, ConstFilterState.Enabled));
            });
        }

        [Fact]
        public void ToPreparedSql_SelectWithExplicitJoin_AppendsJoinedConstFilterToOn()
        {
            var provider = CreateProvider();

            RunWithProvider(provider, () =>
            {
                var from = new FromExpr(typeof(PlainJoinOrder));
                from.Joins.Add(new TableJoinExpr(
                    new TableExpr(typeof(ConstFilterDepartment)) { Alias = "Dept" },
                    Expr.Prop("DepartmentId") == Expr.Prop("Dept", "Id")));
                var expr = new SelectExpr { Source = from };

                var sql = expr.ToPreparedSql(new SqlBuildContext() { SingleTable = false }, SqlBuilder.Instance);

                Assert.Contains("JOIN", sql.Sql);
                Assert.Contains("Dept", sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(sql.Params, param => Equals(param.Value, ConstFilterState.Enabled));
            });
        }

        [Fact]
        public void ToPreparedSql_Update_AppendsMainConstFilter()
        {
            var provider = CreateProvider();

            RunWithProvider(provider, () =>
            {
                var expr = new UpdateExpr
                {
                    Table = new TableExpr(typeof(ConstFilterOrder)),
                    Sets = new List<(PropertyExpr, ValueTypeExpr)> { (Expr.Prop("Name"), Expr.Value("Updated")) },
                    Where = Expr.Prop("Id") == 1
                };

                var sql = expr.ToPreparedSql(new SqlBuildContext() { SingleTable = false }, SqlBuilder.Instance);

                Assert.Contains("WHERE", sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(sql.Params, param => Equals(param.Value, ConstFilterState.Enabled));
            });
        }

        [Fact]
        public void ToPreparedSql_Delete_AppendsMainConstFilter()
        {
            var provider = CreateProvider();

            RunWithProvider(provider, () =>
            {
                var expr = new DeleteExpr(new TableExpr(typeof(ConstFilterOrder)), Expr.Prop("Id") == 1);

                var sql = expr.ToPreparedSql(new SqlBuildContext() { SingleTable = false }, SqlBuilder.Instance);

                Assert.Contains("WHERE", sql.Sql);
                Assert.Contains("State", sql.Sql);
                Assert.Contains(sql.Params, param => Equals(param.Value, ConstFilterState.Enabled));
            });
        }

        private static void RunWithProvider(TableInfoProvider provider, Action action)
        {
            TableInfoProvider currentProvider = TableInfoProvider.Default;
            try
            {
                TableInfoProvider.Default = provider;
                action();
            }
            finally
            {
                TableInfoProvider.Default = currentProvider;
            }
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

        private enum ConstFilterState
        {
            Disabled = 0,
            Enabled = 1
        }

        [Table("ConstFilterOrders")]
        private class ConstFilterOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string Name { get; set; } = string.Empty;

            [Column("State", Constant = "Enabled")]
            public ConstFilterState State { get; set; }
        }

        [Table("PlainJoinOrders")]
        private class PlainJoinOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("DepartmentId")]
            public int DepartmentId { get; set; }
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

        private class ConstFilterJoinOrderView : ConstFilterJoinOrder
        {
            [ForeignColumn(typeof(ConstFilterDepartment), Property = nameof(ConstFilterDepartment.State))]
            public ConstFilterState State { get; set; }
        }

        [Table("ConstFilterDepartments")]
        private class ConstFilterDepartment
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("State", Constant = ConstFilterState.Enabled)]
            public ConstFilterState State => ConstFilterState.Enabled;
        }
    }
}
