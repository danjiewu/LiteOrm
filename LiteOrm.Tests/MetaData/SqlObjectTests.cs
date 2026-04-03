using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SqlObjectTests
    {
        [Fact]
        public void Name_SetValue_ReturnsAssignedValue()
        {
            var sqlObject = new TestSqlObject();

            sqlObject.SetName("User");

            Assert.Equal("User", sqlObject.Name);
        }

        [Fact]
        public void ToString_WhenNameSet_ReturnsName()
        {
            var sqlObject = new TestSqlObject();
            sqlObject.SetName("Orders");

            var result = sqlObject.ToString();

            Assert.Equal("Orders", result);
        }

        [Fact]
        public void Equals_WithSameTypeAndName_ReturnsTrue()
        {
            var left = new TestSqlObject();
            var right = new TestSqlObject();
            left.SetName("Same");
            right.SetName("Same");

            Assert.True(left.Equals(right));
            Assert.True(left == right);
        }

        [Fact]
        public void Equals_WithDifferentDerivedTypes_ReturnsFalse()
        {
            var left = new TestSqlObject();
            var right = new AnotherSqlObject();
            left.SetName("Same");
            right.SetName("Same");

            Assert.False(left.Equals(right));
            Assert.True(left != right);
        }

        [Fact]
        public void GetHashCode_WhenNameIsNull_ReturnsZero()
        {
            var sqlObject = new TestSqlObject();

            Assert.Equal(0, sqlObject.GetHashCode());
        }

        [Fact]
        public void GetHashCode_WhenNameSet_ReturnsNameHashCode()
        {
            var sqlObject = new TestSqlObject();
            sqlObject.SetName("HashMe");

            Assert.Equal("HashMe".GetHashCode(), sqlObject.GetHashCode());
        }

        private class TestSqlObject : SqlObject
        {
            public void SetName(string name)
            {
                Name = name;
            }
        }

        private class AnotherSqlObject : SqlObject
        {
            public void SetName(string name)
            {
                Name = name;
            }
        }
    }
}
