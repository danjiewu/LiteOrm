using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class FromExprTests
    {
        [Fact]
        public void ToString_IncludesJoinClauses()
        {
            var expr = new FromExpr(typeof(TestUser));
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("DeptId") == Expr.Prop("Id"))
            {
                JoinType = TableJoinType.Left
            });

            var text = expr.ToString();

            Assert.Contains(nameof(TestUser), text);
        }

        [Fact]
        public void ToString_IgnoresNullJoinItems()
        {
            var expr = new FromExpr(typeof(TestUser));
            expr.Joins.Add(null);
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), null)
            {
                JoinType = TableJoinType.Inner
            });

            Assert.Equal($"{nameof(TestUser)}", expr.ToString());
        }
    }
}
