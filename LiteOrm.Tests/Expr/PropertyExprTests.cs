using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class PropertyExprTests
    {
        [Fact]
        public void Constructor_WithPropertyName_SetsPropertyName()
        {
            var expr = new PropertyExpr("Name");

            Assert.Equal("Name", expr.PropertyName);
        }

        [Fact]
        public void TableAlias_WithValidValue_SetsAlias()
        {
            var expr = new PropertyExpr("Name") { TableAlias = "u" };

            Assert.Equal("u", expr.TableAlias);
            Assert.Equal("[u].[Name]", expr.ToString());
        }

        [Fact]
        public void TableAlias_WithInvalidValue_ThrowsArgumentException()
        {
            var expr = new PropertyExpr("Name");

            Assert.Throws<ArgumentException>(() => expr.TableAlias = "bad alias");
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new PropertyExpr("Name") { TableAlias = "u" };
            var clone = (PropertyExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
        }
    }
}
