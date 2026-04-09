using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class UpdateExprTests
    {
        [Fact]
        public void ToString_WithNullSets_DoesNotThrow()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)))
            {
                Sets = null
            };

            Assert.Equal($"UPDATE {nameof(TestUser)}", expr.ToString());
        }
    }
}
