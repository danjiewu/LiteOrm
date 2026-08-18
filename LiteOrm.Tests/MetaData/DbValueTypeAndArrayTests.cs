using System.Data;
using LiteOrm.Common;
using LiteOrm.Pgsql;
using Xunit;

namespace LiteOrm.Tests
{
    public class DbValueTypeAndArrayTests
    {
        [Fact]
        public void DbValueType_Array_IsArrayType()
        {
            Assert.True(DbValueType.Array.HasArray());
            Assert.False(DbValueType.String.HasArray());
            Assert.True((DbValueType.Int32 | DbValueType.Array).HasArray());
            Assert.True((DbValueType.String | DbValueType.Array).HasArray());
        }

        [Fact]
        public void DbValueTypeMap_ToDbType_MapsArrayAndJson()
        {
            Assert.Equal(DbType.Object, SqlBuilder.Instance.ToDbType(DbValueType.Array));
            Assert.Equal(DbType.String, SqlBuilder.Instance.ToDbType(DbValueType.Json));
            Assert.Equal(DbType.String, SqlBuilder.Instance.ToDbType(DbValueType.Jsonb));
            Assert.Equal(DbType.Int32, SqlBuilder.Instance.ToDbType(DbValueType.Int32));
            Assert.Equal(DbType.Int32, SqlBuilder.Instance.ToDbType(DbValueType.Int32 | DbValueType.Array));
            Assert.Equal(DbType.String, SqlBuilder.Instance.ToDbType(DbValueType.String | DbValueType.Array));
        }

        [Fact]
        public void DbValueTypeMap_FromDbType_RoundTripsScalars()
        {
            Assert.Equal(DbValueType.Int32, DbValueTypeMap.FromDbType(DbType.Int32));
            Assert.Equal(DbValueType.String, DbValueTypeMap.FromDbType(DbType.String));
            Assert.Equal(DbValueType.Object, DbValueTypeMap.FromDbType(DbType.Object));
        }

        [Fact]
        public void GetDbValueType_CollectionProperty_InfersArray()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(PgsqlArrayModel))!;
            var tags = table.GetColumn(nameof(PgsqlArrayModel.Tags))!;
            var scores = table.GetColumn(nameof(PgsqlArrayModel.Scores))!;

            Assert.Equal(DbValueType.String | DbValueType.Array, tags.GetDbValueType(SqlBuilder.Instance));
            Assert.Equal(DbValueType.Int32 | DbValueType.Array, scores.GetDbValueType(SqlBuilder.Instance));
        }

        [Fact]
        public void PostgreSqlBuilder_CreateTableSql_ArrayAndJsonColumns()
        {
            var table = new AttributeTableInfoProvider().GetTableDefinition(typeof(PgsqlArrayModel))!;
            var sql = PostgreSqlBuilder.Instance.BuildCreateTableSql(table.Name!, table.Columns);

            Assert.Contains("integer[]", sql);
            Assert.Contains("text[]", sql);
            Assert.Contains("JSONB", sql);
        }

        [Fact]
        public void PostgreSqlBuilder_AnyArray_BindsAsSingleParameter()
        {
            var query = new SelectExpr(
                Expr.From<PgsqlArrayModel>().Where(Expr.Prop(nameof(PgsqlArrayModel.Tags)).Any(new ValueExpr(new[] { "a", "b" }))),
                Expr.Prop(nameof(PgsqlArrayModel.Id)));

            var prepared = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, PostgreSqlBuilder.Instance);

            Assert.Contains("ANY(@0)", prepared.Sql);
            Assert.Single(prepared.Params);
        }

        [Table("PgsqlArrayModels")]
        private class PgsqlArrayModel
        {
            [Column("Id", IsPrimaryKey = true, AllowNull = false)]
            public int Id { get; set; }

            [Column("Tags", AllowNull = true)]
            public string[]? Tags { get; set; }

            [Column("Scores", AllowNull = true)]
            public int[]? Scores { get; set; }

            [Column("Meta", AllowNull = true, DbType = DbValueType.Jsonb)]
            public string? Meta { get; set; }
        }
    }
}
