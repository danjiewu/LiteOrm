using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class GroupByExprTests
    {
        [Fact]
        public void ToString_IgnoresNullGroupByItems()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)))
            {
                GroupBys = new List<ValueTypeExpr> { null, Expr.Prop("DeptId") }
            };

            Assert.Equal($"{nameof(TestUser)} GROUP BY [DeptId]", expr.ToString());
        }
    }
}
