using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class LogicBinaryOperatorExtTests
    {
        [Fact]
        public void IsNot_WithNotEqual_ReturnsTrue()
        {
            Assert.True(LogicOperator.NotEqual.IsNot());
        }

        [Fact]
        public void IsNot_WithEqual_ReturnsFalse()
        {
            Assert.False(LogicOperator.Equal.IsNot());
        }

        [Fact]
        public void Positive_RemovesNotBit()
        {
            Assert.Equal(LogicOperator.Equal, LogicOperator.NotEqual.Positive());
        }

        [Fact]
        public void Opposite_TogglesNotBit()
        {
            Assert.Equal(LogicOperator.NotEqual, LogicOperator.Equal.Opposite());
            Assert.Equal(LogicOperator.Equal, LogicOperator.NotEqual.Opposite());
        }
    }
}
