using LiteOrm.Common;
using System.Linq.Expressions;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class LambdaExprTests
    {
        [Fact]
        public void Equals_DefaultInstances_ReturnsTrue()
        {
            var left = new LambdaExpr();
            var right = new LambdaExpr();

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void GetHashCode_DefaultInstance_DoesNotThrow()
        {
            var expr = new LambdaExpr();

            _ = expr.GetHashCode();
        }

        [Fact]
        public void Equals_SameLambdaContent_ReturnsTrue()
        {
            Expression<Func<TestUser, bool>> expression = u => u.Id > 1;
            var left = new LambdaExpr(expression);
            var right = new LambdaExpr(expression);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
    }
}
