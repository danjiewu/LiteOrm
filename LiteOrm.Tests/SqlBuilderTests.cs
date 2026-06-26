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
        }

        [Theory]
        [MemberData(nameof(AddColumnBuilders))]
        public void BuildAddColumnsSql_WithNullableDefaultValue_PreservesDefault(SqlBuilder builder)
        {
            var tableDefinition = CreateProvider(builder).GetTableDefinition(typeof(SqlBuilderDefaultValueModel));
            var nickNameColumn = tableDefinition.GetColumn(nameof(SqlBuilderDefaultValueModel.NickName));

            var sql = builder.BuildAddColumnsSql(tableDefinition.Name, new[] { nickNameColumn });

            Assert.Contains("DEFAULT 'guest'", sql);
            Assert.DoesNotContain("DEFAULT ''", sql);
        }

        [Fact]
        public void SQLiteBuildCreateTableSql_WithDefaultValue_PreservesDefault()
        {
            var tableDefinition = CreateProvider(SQLiteBuilder.Instance).GetTableDefinition(typeof(SqlBuilderDefaultValueModel));

            var sql = SQLiteBuilder.Instance.BuildCreateTableSql(tableDefinition.Name, tableDefinition.Columns);

            Assert.Contains(@"""NickName""", sql);
            Assert.Contains("DEFAULT 'guest'", sql);
        }

        [Fact]
        public void GetDbType_WithNullType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SqlBuilder.Instance.GetDbType(null!));
        }

        [Fact]
        public void OracleBuildCreateTableSql_WithByteAndUnsignedColumns_UsesNumber()
        {
            var tableDefinition = CreateProvider(OracleBuilder.Instance).GetTableDefinition(typeof(OracleNumericModel));

            var sql = OracleBuilder.Instance.BuildCreateTableSql(tableDefinition.Name, tableDefinition.Columns);

            Assert.Contains("NUMBER", sql);
            Assert.DoesNotContain("TINYINT", sql);
            Assert.DoesNotContain("UNSIGNED", sql);
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

        private static AttributeTableInfoProvider CreateProvider(SqlBuilder builder)
        {
            var sqlBuilderFactory = new Mock<ISqlBuilderFactory>();
            sqlBuilderFactory
                .Setup(factory => factory.GetSqlBuilder(It.IsAny<Type>(), It.IsAny<string>()))
                .Returns(builder);

            var dataSourceProvider = new Mock<IDataSourceProvider>();
            dataSourceProvider.SetupGet(provider => provider.DefaultDataSourceName).Returns("default");
            dataSourceProvider
                .Setup(provider => provider.GetDataSource(It.IsAny<string>()))
                .Returns(new DataSourceConfig
                {
                    Name = "default",
                    Provider = typeof(DbConnection).AssemblyQualifiedName!
                });

            var services = new ServiceCollection();
            services.AddSingleton(sqlBuilderFactory.Object);
            services.AddSingleton(dataSourceProvider.Object);
            return new AttributeTableInfoProvider(services.BuildServiceProvider());
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
    }
}
