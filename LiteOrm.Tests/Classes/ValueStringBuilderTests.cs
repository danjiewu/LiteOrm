using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ValueStringBuilderTests
    {
        [Fact]
        public void Create_AppendAndToString_Works()
        {
            var builder = ValueStringBuilder.Create();
            try
            {
                builder.Append('A');
                builder.Append("BC");

                Assert.Equal("ABC", builder.ToString());
                Assert.Equal(3, builder.Length);
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void Clear_ResetsLength()
        {
            var builder = ValueStringBuilder.Create();
            try
            {
                builder.Append("Text");
                builder.Clear();

                Assert.Equal(0, builder.Length);
                Assert.Equal(string.Empty, builder.ToString());
            }
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public void Append_LongString_GrowsCapacity()
        {
            var builder = ValueStringBuilder.Create(4);
            try
            {
                builder.Append(new string('x', 20));

                Assert.Equal(20, builder.Length);
                Assert.True(builder.Capacity >= 20);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }
}
