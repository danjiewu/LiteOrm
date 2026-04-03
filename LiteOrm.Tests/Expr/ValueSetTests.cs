using System.Linq;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ValueSetTests
    {
        [Fact]
        public void Constructor_WithParams_AddsItems()
        {
            var set = new ValueSet(new ValueExpr(1), new ValueExpr(2));

            Assert.Equal(2, set.Count);
            Assert.Equal("(1,2)", set.ToString());
        }

        [Fact]
        public void Add_Null_AddsExprNullSingleton()
        {
            var set = new ValueSet();

            set.Add(null);

            Assert.Single(set);
            Assert.Equal("(NULL)", set.ToString());
        }

        [Fact]
        public void Add_WithNestedSameJoinType_FlattensItems()
        {
            var nested = new ValueSet(ValueJoinType.List, new ValueExpr(2), new ValueExpr(3));
            var set = new ValueSet(ValueJoinType.List, new ValueExpr(1));

            set.Add(nested);

            Assert.Equal(3, set.Count);
            Assert.Equal("(1,2,3)", set.ToString());
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var set = new ValueSet(ValueJoinType.Concat, new ValueExpr("A"), new ValueExpr("B"));
            var clone = (ValueSet)set.Clone();

            Assert.Equal(set, clone);
            Assert.NotSame(set[0], clone[0]);
        }

        [Fact]
        public void AddRange_AppendsItemsInOrder()
        {
            var set = new ValueSet();

            set.AddRange(new ValueTypeExpr[] { new ValueExpr(1), new ValueExpr(2), new ValueExpr(3) });

            Assert.Equal(new[] { "1", "2", "3" }, set.Select(x => x.ToString()).ToArray());
        }
    }
}
