using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// TypeResolverHelper 类型名称匹配优化单元测试（纯内存，无需数据库）。
    /// 覆盖：忽略非实质性字符匹配、忽略大小写匹配、自定义注册的模糊匹配。
    /// </summary>
    public class TypeResolverHelperTests
    {
        [Fact]
        public void FindType_ExactName_StillMatches()
        {
            Assert.Equal(typeof(string), TypeResolverHelper.FindType("System.String"));
        }

        [Fact]
        public void FindType_WhitespaceInsideName_IsIgnored()
        {
            // 空格/制表/换行等非实质性字符应被剥离后再匹配
            Assert.Equal(typeof(string), TypeResolverHelper.FindType(" System . String "));
            Assert.Equal(typeof(string), TypeResolverHelper.FindType("\tSystem\t.\tString\r\n"));
        }

        [Fact]
        public void FindType_CaseMismatch_FallsBackToCaseInsensitive()
        {
            Assert.Equal(typeof(string), TypeResolverHelper.FindType("system.string"));
            Assert.Equal(typeof(string), TypeResolverHelper.FindType("SYSTEM.STRING"));
        }

        [Fact]
        public void FindType_WhitespacePlusCaseMismatch_Matches()
        {
            Assert.Equal(typeof(string), TypeResolverHelper.FindType("  sYsTeM . StRiNg  "));
        }

        [Fact]
        public void FindType_RegisteredName_Matches_CaseAndWhitespaceInsensitive()
        {
            const string key = "TypeResolverHelper_Registered_CI_Key";
            try
            {
                TypeResolverHelper.Register(key, typeof(InnerTarget));
                Assert.Equal(typeof(InnerTarget), TypeResolverHelper.FindType(key));
                // 大小写不同 + 空白差异也应命中
                Assert.Equal(typeof(InnerTarget), TypeResolverHelper.FindType("typeresolverhelper_registered_ci_key"));
                Assert.Equal(typeof(InnerTarget), TypeResolverHelper.FindType(" TypeResolverHelper_Registered_CI_Key "));
            }
            finally
            {
                TypeResolverHelper.Unregister(key);
            }
        }

        private sealed class InnerTarget
        {
        }
    }
}