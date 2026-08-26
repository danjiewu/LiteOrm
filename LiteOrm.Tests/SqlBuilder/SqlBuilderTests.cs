using System;
using System.Collections.Generic;
using System.Data.Common;

using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace LiteOrm.Tests
{
    public class SqlBuilderTests
    {
        public static IEnumerable<object[]> AddColumnBuilders()
        {
            yield return new object[] { SqlBuilder.Instance };
            yield return new object[] { MySqlBuilder.Instance };
            yield return new object[] { PostgreSqlBuilder.Instance };
            yield return new object[] { OracleBuilder.Instance };
            yield return new object[] { SQLiteBuilder.Instance };
            // 国产 / 兼容数据库（继承自上方基础方言，确保测试覆盖其行为一致性）
            yield return new object[] { DamengBuilder.Instance };
            yield return new object[] { KingbaseESBuilder.Instance };
            yield return new object[] { GaussDBBuilder.Instance };
            yield return new object[] { OceanBaseBuilder.Instance };
            yield return new object[] { TiDBBuilder.Instance };
            yield return new object[] { GreatDBBuilder.Instance };
        }

        [Theory]
        [MemberData(nameof(AddColumnBuilders))]
        public void BuildAddColumnsSql_WithNullableDefaultValue_PreservesDefault(SqlBuilder builder)
        {
            var tableDefinition = CreateProvider(builder).GetTableDefinition(typeof(SqlBuilderDefaultValueModel))!;
            var nickNameColumn = tableDefinition.GetColumn(nameof(SqlBuilderDefaultValueModel.NickName))!;

            var sql = builder.BuildAddColumnsSql(tableDefinition.Name!, new[] { nickNameColumn! });

            Assert.Contains("DEFAULT 'guest'", sql);
            Assert.DoesNotContain("DEFAULT ''", sql);
        }

        [Fact]
        public void SQLiteBuildCreateTableSql_WithDefaultValue_PreservesDefault()
        {
            var tableDefinition = CreateProvider(SQLiteBuilder.Instance).GetTableDefinition(typeof(SqlBuilderDefaultValueModel))!;

            var sql = SQLiteBuilder.Instance.BuildCreateTableSql(tableDefinition.Name!, tableDefinition.Columns);

            Assert.Contains(@"""NickName""", sql);
            Assert.Contains("DEFAULT 'guest'", sql);
        }

        [Fact]
        public void GetDbValueType_WithNullType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SqlBuilder.Instance.GetDbValueType(null!));
        }

        [Fact]
        public void OracleBuildCreateTableSql_WithByteAndUnsignedColumns_UsesNumber()
        {
            var tableDefinition = CreateProvider(OracleBuilder.Instance).GetTableDefinition(typeof(OracleNumericModel))!;

            var sql = OracleBuilder.Instance.BuildCreateTableSql(tableDefinition.Name!, tableDefinition.Columns);

            Assert.Contains("NUMBER", sql);
            Assert.DoesNotContain("TINYINT", sql);
            Assert.DoesNotContain("UNSIGNED", sql);
        }

        [Fact]
        public void MySqlBuildCreateTableSql_WithCustomStartValue_AppendsAutoIncrementOption()
        {
            var tableDefinition = CreateProvider(MySqlBuilder.Instance).GetTableDefinition(typeof(IdentityStartValueModel))!;

            var sql = MySqlBuilder.Instance.BuildCreateTableSql(tableDefinition.Name!, tableDefinition.Columns);

            Assert.Contains("AUTO_INCREMENT", sql);
            Assert.Contains("AUTO_INCREMENT = 1000", sql);
        }

        [Fact]
        public void MySqlBuildCreateTableSql_WithDefaultStartValue_DoesNotAppendAutoIncrementOption()
        {
            var tableDefinition = CreateProvider(MySqlBuilder.Instance).GetTableDefinition(typeof(SqlBuilderDefaultValueModel))!;

            var sql = MySqlBuilder.Instance.BuildCreateTableSql(tableDefinition.Name!, tableDefinition.Columns);

            Assert.Contains("AUTO_INCREMENT", sql);
            Assert.DoesNotContain("AUTO_INCREMENT =", sql);
        }

        [Fact]
        public void DamengBuildCreateTableSql_WithCustomStartValueAndIncreasement_UsesIdentitySyntax()
        {
            var tableDefinition = CreateProvider(DamengBuilder.Instance).GetTableDefinition(typeof(IdentityStartValueModel))!;

            var sql = DamengBuilder.Instance.BuildCreateTableSql(tableDefinition.Name!, tableDefinition.Columns);

            Assert.Contains("IDENTITY(1000, 5)", sql);
        }

        [Theory]
        [InlineData("hello", false)]
        [InlineData("test_value", true)]
        [InlineData("100%", true)]
        [InlineData("a/b", true)]
        [InlineData("[test]", true)]
        [InlineData("normal text", false)]
        [InlineData("", false)]
        [InlineData("abc123", false)]
        public void NeedLikeEscape_ReturnsExpected(string value, bool expected)
        {
            Assert.Equal(expected, SqlBuilder.Instance.NeedLikeEscape(value));
        }

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("test_value", "test/_value")]
        [InlineData("100%", "100/%")]
        [InlineData("a/b", "a//b")]
        [InlineData("[test]", "/[test/]")]
        public void ToSqlLikeValue_EscapesCorrectly(string input, string expected)
        {
            Assert.Equal(expected, SqlBuilder.Instance.ToSqlLikeValue(input));
        }

        [Theory]
        [InlineData("hello", false)]
        [InlineData("test_value", true)]
        [InlineData("100%", true)]
        [InlineData("a/b", true)]
        [InlineData("normal text", false)]
        public void OracleNeedLikeEscape_ReturnsExpected(string value, bool expected)
        {
            Assert.Equal(expected, OracleBuilder.Instance.NeedLikeEscape(value));
        }

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("test_value", "test/_value")]
        [InlineData("100%", "100/%")]
        [InlineData("a/b", "a//b")]
        public void OracleToSqlLikeValue_EscapesCorrectly(string input, string expected)
        {
            Assert.Equal(expected, OracleBuilder.Instance.ToSqlLikeValue(input));
        }

        [Theory]
        [InlineData(null, true, "NULL")]
        [InlineData("", true, "''")]
        [InlineData("hello", true, "'hello'")]
        [InlineData(" ", true, "' '")]
        [InlineData("John Smith", true, "'John Smith'")]
        [InlineData("2026-08-26", true, "'2026-08-26'")]
        [InlineData("it's", true, "'it''s'")]
        [InlineData("a'b", true, "'a''b'")]
        [InlineData("a\\b", false, null)]
        [InlineData("trailing\\", false, null)]
        [InlineData("a\tb", false, null)]
        [InlineData("a\nb", false, null)]
        public void TryAppendSqlLiteral_DetectsSafeAndUnsafe(string? input, bool expectedResult, string? expectedOutput)
        {
            var sb = new ValueStringBuilder();
            bool result = SqlBuilder.Instance.TryAppendSqlLiteral(ref sb, input);
            Assert.Equal(expectedResult, result);
            string output = sb.ToString();
            sb.Dispose();
            Assert.Equal(expectedOutput, expectedResult ? output : null);

            var sb2 = new ValueStringBuilder();
            Assert.Equal(expectedResult, MySqlBuilder.Instance.TryAppendSqlLiteral(ref sb2, input));
            Assert.Equal(expectedOutput, expectedResult ? sb2.ToString() : null);
            sb2.Dispose();

            var sb3 = new ValueStringBuilder();
            Assert.Equal(expectedResult, SQLiteBuilder.Instance.TryAppendSqlLiteral(ref sb3, input));
            Assert.Equal(expectedOutput, expectedResult ? sb3.ToString() : null);
            sb3.Dispose();
        }

        [Theory]
        [InlineData("hello", 0)]     // safe string: inlined, no params
        [InlineData(" ", 0)]         // space: inlined, no params
        [InlineData("it's", 0)]      // single quote: inlined via '', no params
        [InlineData("a\\b", 1)]      // backslash: parameterized
        [InlineData("a\tb", 1)]      // tab: parameterized
        [InlineData("", 0)]           // empty string: inlined
        public void ConstString_RendersLiteralOrParam_DependingOnSafety(string value, int expectedParamCount)
        {
            var builder = SQLiteBuilder.Instance;
            var context = new SqlBuildContext(null, "T0", null);
            var sb = new ValueStringBuilder();
            var outputParams = new List<Param>();
            var expr = (Expr)new ValueExpr(value) { IsConst = true };
            expr.ToSql(ref sb, context, builder, outputParams);
            sb.Dispose();
            Assert.Equal(expectedParamCount, outputParams.Count);
        }

        private static AttributeTableInfoProvider CreateProvider(SqlBuilder builder)
        {
            return new AttributeTableInfoProvider();
        }

        [Table("SqlBuilderDefaultValueModels")]
        private class SqlBuilderDefaultValueModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true, AllowNull = false)]
            public int Id { get; set; }

            [Column("NickName", DefaultValue = "'guest'", AllowNull = true)]
            public string? NickName { get; set; }
        }

        [Table("OracleNumericModels")]
        private class OracleNumericModel
        {
            [Column("Id", IsPrimaryKey = true, AllowNull = false)]
            public int Id { get; set; }

            [Column("ByteValue", AllowNull = false)]
            public byte ByteValue { get; set; }

            [Column("UIntValue", AllowNull = false)]
            public uint UIntValue { get; set; }
        }

        [Table("IdentityStartValueModels")]
        private class IdentityStartValueModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true, IdentityStart = 1000, IdentityIncreasement = 5, AllowNull = false)]
            public int Id { get; set; }

            [Column("Name", AllowNull = true)]
            public string? Name { get; set; }
        }
    }
}
