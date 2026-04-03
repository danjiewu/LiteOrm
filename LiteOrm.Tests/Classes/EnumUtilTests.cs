using System.ComponentModel;

using LiteOrm;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class EnumUtilTests
    {
        [Fact]
        public void GetDisplayName_WithDescriptionAttribute_ReturnsDescription()
        {
            Assert.Equal("First Value", EnumUtil.GetDisplayName(SampleEnum.First));
        }

        [Fact]
        public void GetDisplayName_WithoutAttribute_ReturnsEnumName()
        {
            Assert.Equal(nameof(SampleEnum.Second), EnumUtil.GetDisplayName(SampleEnum.Second));
        }

        [Fact]
        public void Parse_Generic_WithDescriptionValue_ReturnsEnum()
        {
            Assert.Equal(SampleEnum.First, EnumUtil.Parse<SampleEnum>("First Value"));
        }

        [Fact]
        public void Parse_Generic_WithEnumNameIgnoringCase_ReturnsEnum()
        {
            Assert.Equal(SampleEnum.Second, EnumUtil.Parse<SampleEnum>("second"));
        }

        [Fact]
        public void Parse_ByType_WithEnumName_ReturnsEnum()
        {
            Assert.Equal(SampleEnum.Second, EnumUtil.Parse(typeof(SampleEnum), nameof(SampleEnum.Second)));
        }

        private enum SampleEnum
        {
            [Description("First Value")]
            First,
            Second
        }
    }
}
