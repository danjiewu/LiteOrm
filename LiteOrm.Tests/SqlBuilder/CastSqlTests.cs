using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// CAST 表达式的各方言渲染测试。
    /// MySQL 的 CAST 只接受 SIGNED / UNSIGNED / CHAR / DECIMAL / DOUBLE 等固定类型名，
    /// 不接受 INT / BIGINT / VARCHAR / BIT，故 MySqlBuilder 覆盖了 GetSqlTypeName。
    /// 纯内存测试，无需数据库连接。
    /// </summary>
    public class CastSqlTests
    {
        private static string Render(SqlBuilder builder, DbValueType dbValueType)
        {
            return Expr.Prop("V").Cast(dbValueType).ToPreparedSql(new SqlBuildContext(builder)).Sql;
        }

        [Theory]
        [InlineData(DbValueType.Int16, "SIGNED")]
        [InlineData(DbValueType.Int32, "SIGNED")]
        [InlineData(DbValueType.Int64, "SIGNED")]
        [InlineData(DbValueType.UInt16, "UNSIGNED")]
        [InlineData(DbValueType.UInt32, "UNSIGNED")]
        [InlineData(DbValueType.UInt64, "UNSIGNED")]
        [InlineData(DbValueType.Byte, "UNSIGNED")]
        [InlineData(DbValueType.SByte, "UNSIGNED")]
        [InlineData(DbValueType.Boolean, "UNSIGNED")]
        [InlineData(DbValueType.Decimal, "DECIMAL")]
        [InlineData(DbValueType.Double, "DOUBLE")]
        [InlineData(DbValueType.Single, "FLOAT")]
        [InlineData(DbValueType.String, "CHAR")]
        [InlineData(DbValueType.Guid, "CHAR")]
        [InlineData(DbValueType.Json, "CHAR")]
        [InlineData(DbValueType.Date, "DATE")]
        [InlineData(DbValueType.DateTime, "DATETIME")]
        [InlineData(DbValueType.Time, "TIME")]
        public void Cast_MySql_RendersMySqlCastTypeName(DbValueType dbValueType, string expectedSqlType)
        {
            Assert.Equal($"CAST(`V` AS {expectedSqlType})", Render(MySqlBuilder.Instance, dbValueType));
        }

        [Theory]
        [InlineData(DbValueType.Int32)]
        [InlineData(DbValueType.Int64)]
        [InlineData(DbValueType.String)]
        [InlineData(DbValueType.Boolean)]
        public void Cast_MySql_NeverRendersUnsupportedTypeName(DbValueType dbValueType)
        {
            string sql = Render(MySqlBuilder.Instance, dbValueType);

            Assert.DoesNotContain("INT", sql);
            Assert.DoesNotContain("VARCHAR", sql);
            Assert.DoesNotContain("BIT", sql);
        }

        [Fact]
        public void Cast_MySql_WithArrayMask_StripsMaskBeforeMapping()
        {
            var arrayType = (DbValueType)((int)DbValueType.Int32 | (int)DbValueType.Array);

            Assert.Equal("CAST(`V` AS SIGNED)", Render(MySqlBuilder.Instance, arrayType));
        }

        [Theory]
        [InlineData("OceanBaseBuilder")]
        [InlineData("TiDBBuilder")]
        [InlineData("GreatDBBuilder")]
        public void Cast_MySqlFamily_BuildersInheritMySqlCastTypeNames(string builderName)
        {
            SqlBuilder builder = builderName switch
            {
                "OceanBaseBuilder" => OceanBaseBuilder.Instance,
                "TiDBBuilder" => TiDBBuilder.Instance,
                _ => GreatDBBuilder.Instance
            };

            Assert.Equal("CAST(`V` AS SIGNED)", Render(builder, DbValueType.Int32));
        }

        [Fact]
        public void Cast_GenericDialect_KeepsGenericTypeName()
        {
            Assert.Equal("CAST(\"V\" AS INT)", Render(SqlBuilder.Instance, DbValueType.Int32));
            Assert.Equal("CAST(\"V\" AS VARCHAR)", Render(SqlBuilder.Instance, DbValueType.String));
        }

        [Fact]
        public void Cast_SqlServerAndOracle_KeepGenericTypeName()
        {
            Assert.Equal("CAST(\"V\" AS INT)", Render(SqlServerBuilder.Instance, DbValueType.Int32));
            Assert.Equal("CAST(\"V\" AS INT)", Render(OracleBuilder.Instance, DbValueType.Int32));
        }
    }
}