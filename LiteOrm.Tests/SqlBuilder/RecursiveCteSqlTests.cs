using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;
using static LiteOrm.Common.Expr;

namespace LiteOrm.Tests
{
    public class RecursiveCteSqlTests
    {
        [Fact]
        public void ExplicitRecursive_Default_OnSqlBuilder_IsFalse()
        {
            Assert.False(SqlBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_SqlServerBuilder_IsFalse()
        {
            Assert.False(SqlServerBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_OracleBuilder_IsFalse()
        {
            Assert.False(OracleBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_DamengBuilder_IsFalse()
        {
            Assert.False(DamengBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_MySqlBuilder_IsTrue()
        {
            Assert.True(MySqlBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_TiDBBuilder_IsTrue()
        {
            Assert.True(TiDBBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_OceanBaseBuilder_IsTrue()
        {
            Assert.True(OceanBaseBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_GreatDBBuilder_IsTrue()
        {
            Assert.True(GreatDBBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_PostgreSqlBuilder_IsTrue()
        {
            Assert.True(PostgreSqlBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_KingbaseESBuilder_IsTrue()
        {
            Assert.True(KingbaseESBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_GaussDBBuilder_IsTrue()
        {
            Assert.True(GaussDBBuilder.Instance.ExplicitRecursive);
        }

        [Fact]
        public void ExplicitRecursive_SQLiteBuilder_IsTrue()
        {
            Assert.True(SQLiteBuilder.Instance.ExplicitRecursive);
        }

        /// <summary>
        /// ExplicitRecursive 为 true 的数据库：即使 CTE 不存在循环引用，也直接输出 WITH RECURSIVE。
        /// </summary>
        [Fact]
        public void ToSql_ExplicitRecursiveTrue_NonRecursiveCte_EmitsWithRecursive()
        {
            var cteDef = From<TestUser>().Select(Prop("Id").As("Id"));
            var query = cteDef.With("MyCTE").Select(Prop("Id"));

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SQLiteBuilder.Instance);

            Assert.Contains("WITH RECURSIVE", sql.Sql);
        }

        /// <summary>
        /// ExplicitRecursive 为 false 的数据库：只输出 WITH（不带 RECURSIVE）。
        /// </summary>
        [Fact]
        public void ToSql_ExplicitRecursiveFalse_NonRecursiveCte_EmitsWithOnly()
        {
            var cteDef = From<TestUser>().Select(Prop("Id").As("Id"));
            var query = cteDef.With("MyCTE").Select(Prop("Id"));

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SqlServerBuilder.Instance);

            Assert.Contains("WITH ", sql.Sql);
            Assert.DoesNotContain("WITH RECURSIVE", sql.Sql);
        }

        /// <summary>
        /// 自引用递归 CTE：ExplicitRecursive 为 true 的数据库输出 WITH RECURSIVE。
        /// </summary>
        [Fact]
        public void ToSql_SelfReferencingCte_ExplicitRecursiveTrue_EmitsWithRecursive()
        {
            var selfRef = new CommonTableExpr { Alias = "MyCTE" };
            var cteDef = selfRef.Select(Prop("Id").As("Id"));
            var cte = cteDef.With("MyCTE");
            var query = cte.Select(Prop("Id"));

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SQLiteBuilder.Instance);

            Assert.Contains("WITH RECURSIVE", sql.Sql);
        }

        /// <summary>
        /// 自引用递归 CTE：ExplicitRecursive 为 false 的数据库（如 SQL Server）只输出 WITH。
        /// </summary>
        [Fact]
        public void ToSql_SelfReferencingCte_ExplicitRecursiveFalse_EmitsWithOnly()
        {
            var selfRef = new CommonTableExpr { Alias = "MyCTE" };
            var cteDef = selfRef.Select(Prop("Id").As("Id"));
            var cte = cteDef.With("MyCTE");
            var query = cte.Select(Prop("Id"));

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SqlServerBuilder.Instance);

            Assert.Contains("WITH ", sql.Sql);
            Assert.DoesNotContain("WITH RECURSIVE", sql.Sql);
        }

        /// <summary>
        /// 多个非递归 CTE：ExplicitRecursive 为 true 时仍输出 WITH RECURSIVE。
        /// </summary>
        [Fact]
        public void ToSql_MultipleNonRecursiveCtes_ExplicitRecursiveTrue_EmitsWithRecursive()
        {
            var cte1Def = From<TestUser>().Select(Prop("Id").As("Id"));
            var cte2Def = From<TestUser>().Select(Prop("Name").As("Name"));

            var cte1 = cte1Def.With("Cte1");
            var cte2 = cte2Def.With("Cte2");

            var branch1 = cte1.Select(Prop("Id").As("Val"));
            var branch2 = cte2.Select(Prop("Name").As("Val"));
            var query = branch1.UnionAll(branch2);

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SQLiteBuilder.Instance);

            Assert.Contains("WITH RECURSIVE", sql.Sql);
        }

        /// <summary>
        /// 多个非递归 CTE：ExplicitRecursive 为 false 时只输出 WITH。
        /// </summary>
        [Fact]
        public void ToSql_MultipleNonRecursiveCtes_ExplicitRecursiveFalse_EmitsWithOnly()
        {
            var cte1Def = From<TestUser>().Select(Prop("Id").As("Id"));
            var cte2Def = From<TestUser>().Select(Prop("Name").As("Name"));

            var cte1 = cte1Def.With("Cte1");
            var cte2 = cte2Def.With("Cte2");

            var branch1 = cte1.Select(Prop("Id").As("Val"));
            var branch2 = cte2.Select(Prop("Name").As("Val"));
            var query = branch1.UnionAll(branch2);

            var sql = query.ToPreparedSql(new SqlBuildContext { SingleTable = false }, SqlServerBuilder.Instance);

            Assert.Contains("WITH ", sql.Sql);
            Assert.DoesNotContain("WITH RECURSIVE", sql.Sql);
        }
    }
}
