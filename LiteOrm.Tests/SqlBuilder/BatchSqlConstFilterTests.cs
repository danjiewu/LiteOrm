using LiteOrm.Common;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证批量语句生成器能把 DAO 传入的固定筛选条件插入到方言语句的正确位置。
    /// 附加条件本身由 DAO 渲染（见 <c>DAOBase.MakeConstFilterCondition</c>），这里只关心插入点。
    /// </summary>
    public class BatchSqlConstFilterTests
    {
        private const string Filter = "<FILTER>";
        private const int BatchSize = 2;

        /// <summary>
        /// 参与批量语句生成的方言（含继承基础方言的国产 / 兼容数据库）。
        /// </summary>
        public static IEnumerable<object[]> Builders()
        {
            yield return new object[] { SqlBuilder.Instance };
            yield return new object[] { SqlServerBuilder.Instance };
            yield return new object[] { MySqlBuilder.Instance };
            yield return new object[] { PostgreSqlBuilder.Instance };
            yield return new object[] { OracleBuilder.Instance };
            yield return new object[] { SQLiteBuilder.Instance };
            yield return new object[] { DamengBuilder.Instance };
            yield return new object[] { KingbaseESBuilder.Instance };
            yield return new object[] { GaussDBBuilder.Instance };
            yield return new object[] { OceanBaseBuilder.Instance };
            yield return new object[] { TiDBBuilder.Instance };
            yield return new object[] { GreatDBBuilder.Instance };
        }

        [Fact]
        public void BatchTargetTableAlias_StatementsWithoutAlias_ReturnNull()
        {
            Assert.Null(SqlBuilder.Instance.BatchTargetTableAlias);
            Assert.Null(SQLiteBuilder.Instance.BatchTargetTableAlias);
        }

        [Fact]
        public void BatchTargetTableAlias_AliasedStatements_MatchAliasInSql()
        {
            Assert.Equal("T", SqlServerBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("T", MySqlBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("u", PostgreSqlBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("t", OracleBuilder.Instance.BatchTargetTableAlias);
            // 国产 / 兼容数据库继承基础方言的别名
            Assert.Equal("T", OceanBaseBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("T", TiDBBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("u", KingbaseESBuilder.Instance.BatchTargetTableAlias);
            Assert.Equal("t", DamengBuilder.Instance.BatchTargetTableAlias);
        }

        [Theory]
        [MemberData(nameof(Builders))]
        public void BuildBatchUpdateSql_WithConstFilter_IncludesFilterOncePerStatement(SqlBuilder builder)
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string withoutFilter = builder.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize);
            Assert.DoesNotContain(Filter, withoutFilter);

            string withFilter = builder.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            // 基础方言每个批次记录生成一条 UPDATE，其余方言合并为单条语句
            int expectedCount = ReferenceEquals(builder, SqlBuilder.Instance) ? BatchSize : 1;
            Assert.Equal(expectedCount, withFilter.Split(Filter).Length - 1);
        }

        [Fact]
        public void BuildBatchUpdateSql_Base_PutsFilterInEachWhereClause()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = SqlBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"\"Id\" = @2 AND {Filter}", sql);
            Assert.Contains($"\"Id\" = @5 AND {Filter}", sql);
        }

        [Fact]
        public void BuildBatchUpdateSql_SqlServer_PutsFilterInJoinOn()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = SqlServerBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"ON (T.\"Id\" = S.k0 AND {Filter})", sql);
        }

        [Fact]
        public void BuildBatchUpdateSql_MySql_PutsFilterBeforeSetClause()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = MySqlBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"AND {Filter}\nSET ", sql);
        }

        [Fact]
        public void BuildBatchUpdateSql_PostgreSql_AppendsFilterToWhereClause()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = PostgreSqlBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.EndsWith($"WHERE u.\"id\" = v.k0 AND {Filter}", sql);
        }

        [Fact]
        public void BuildBatchUpdateSql_Oracle_PutsFilterInMergeOnClause()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = OracleBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"ON (t.\"ID\" = s.\"ID\")\n", sql);
            Assert.Contains("WHEN MATCHED THEN", sql);
            Assert.Contains($"WHERE {Filter}", sql);
        }

        [Fact]
        public void BuildBatchUpdateSql_SQLite_PutsFilterOutsideExistsSubquery()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string sql = SQLiteBuilder.Instance.BuildBatchUpdateSql(table.Name!, table.UpdatableColumns, table.Keys.ToArray(), BatchSize, Filter);

            Assert.EndsWith($") AND {Filter};", sql);
            // 写进 EXISTS 子查询会与 batch_data 的同名列产生歧义
            Assert.DoesNotContain($"batch_data WHERE ", sql);
        }

        [Fact]
        public void BuildBatchDeleteSql_WithConstFilter_AppendsFilter()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string withoutFilter = SqlBuilder.Instance.BuildBatchDeleteSql(table.Name!, table.Keys.ToArray(), BatchSize);
            Assert.DoesNotContain(Filter, withoutFilter);

            string sql = SqlBuilder.Instance.BuildBatchDeleteSql(table.Name!, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"DELETE FROM \"BatchFilterModels\" WHERE \"Id\" IN (@0, @1) AND {Filter}", sql);
        }

        [Fact]
        public void BuildBatchIDExistsSql_WithConstFilter_AppendsFilter()
        {
            var table = CreateProvider().GetTableDefinition(typeof(BatchFilterModel))!;

            string withoutFilter = SqlBuilder.Instance.BuildBatchIDExistsSql(table.Name!, table.Keys.ToArray(), BatchSize);
            Assert.DoesNotContain(Filter, withoutFilter);

            string sql = SqlBuilder.Instance.BuildBatchIDExistsSql(table.Name!, table.Keys.ToArray(), BatchSize, Filter);

            Assert.Contains($"SELECT \"Id\" FROM \"BatchFilterModels\" WHERE \"Id\" IN (@0,@1) AND {Filter}", sql);
        }

        private static AttributeTableInfoProvider CreateProvider()
        {
            return new AttributeTableInfoProvider();
        }

        [Table("BatchFilterModels")]
        private class BatchFilterModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [Column("Name", AllowNull = true)]
            public string? Name { get; set; }

            [Column("State", AllowNull = true)]
            public string? State { get; set; }
        }
    }
}
