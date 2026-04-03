using System;
using System.Linq;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class StringExprConverterTests
    {
        [Fact]
        public void Parse_WithNullText_ReturnsEqualNullExpression()
        {
            var expr = StringExprConverter.Parse(GetProperty(nameof(TestEntity.Name)), null);

            Assert.Equal(LogicOperator.Equal, expr.Operator);
            Assert.Equal("Name", ((PropertyExpr)expr.Left).PropertyName);
            Assert.True(expr.Right is null || expr.Right is ValueExpr { Value: null });
        }

        [Fact]
        public void Parse_WithGreaterThanText_ReturnsGreaterThanExpression()
        {
            var expr = StringExprConverter.Parse(GetProperty(nameof(TestEntity.Age)), ">18");

            Assert.Equal(LogicOperator.GreaterThan, expr.Operator);
            Assert.Equal(18, ((ValueExpr)expr.Right).Value);
        }

        [Fact]
        public void Parse_WithCommaSeparatedText_ReturnsInExpression()
        {
            var expr = StringExprConverter.Parse(GetProperty(nameof(TestEntity.Age)), "1,2,3");
            var values = Assert.IsType<object[]>(((ValueExpr)expr.Right).Value);

            Assert.Equal(LogicOperator.In, expr.Operator);
            Assert.Equal(new object[] { 1, 2, 3 }, values);
        }

        [Fact]
        public void Parse_WithBooleanShortText_ReturnsBooleanValue()
        {
            var expr = StringExprConverter.Parse(GetProperty(nameof(TestEntity.Enabled)), "Y");

            Assert.Equal(true, ((ValueExpr)expr.Right).Value);
        }

        [Fact]
        public void ToText_WithEqualAndNull_ReturnsEqualsPrefix()
        {
            Assert.Equal("=", StringExprConverter.ToText(LogicOperator.Equal, null));
        }

        [Fact]
        public void ToText_WithNotEqualAndString_ReturnsBangPrefix()
        {
            Assert.Equal("!abc", StringExprConverter.ToText(LogicOperator.NotEqual, "abc"));
        }

        [Fact]
        public void ToText_WithInOperator_ReturnsCommaSeparatedValues()
        {
            var text = StringExprConverter.ToText(LogicOperator.In, new object[] { 1, 2, 3 });

            Assert.Equal("1,2,3", text);
        }

        [Fact]
        public void ToText_WithContainsOperator_ReturnsPercentPrefix()
        {
            Assert.Equal("%abc", StringExprConverter.ToText(LogicOperator.Contains, "abc"));
        }

        private static PropertyInfo GetProperty(string name) => typeof(TestEntity).GetProperty(name)!;

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
            public bool Enabled { get; set; }
        }
    }
}
