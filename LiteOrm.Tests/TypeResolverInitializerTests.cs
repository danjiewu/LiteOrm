using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Tests
{
    public class TypeResolverInitializerTests
    {
        private interface IPayload { }
        private sealed class PayloadA : IPayload { }
        private sealed class PayloadB : IPayload { }
        private sealed class NotAPayload { }

        private static readonly Assembly SelfAssembly = typeof(TypeResolverInitializerTests).Assembly;

        private void Cleanup(params string[] names)
        {
            foreach (var name in names)
                TypeResolverHelper.Unregister(name);
        }

        [Fact]
        public void Scan_FromAssembly_FilterConcreteAssignableTo_RegistersWithDefaultShortName()
        {
            // 测试程序集里也可能有其它实现 IPayload 的类型，这里只断言已知两个
            try
            {
                var count = TypeResolverInitializer
                    .Scan(SelfAssembly)
                    .WhereConcreteAssignableTo<IPayload>()
                    .Register();

                Assert.True(count >= 2);
                Assert.Equal(typeof(PayloadA), TypeResolverHelper.FindType("PayloadA"));
                Assert.Equal(typeof(PayloadB), TypeResolverHelper.FindType("PayloadB"));
                Assert.Equal("PayloadA", TypeResolverHelper.GetName(typeof(PayloadA)));
                Assert.Equal("PayloadB", TypeResolverHelper.GetName(typeof(PayloadB)));
            }
            finally
            {
                Cleanup("PayloadA", "PayloadB");
            }
        }

        [Fact]
        public void Scan_FilterAndNamedBy_RegistersCustomName()
        {
            try
            {
                var count = TypeResolverInitializer
                    .Scan(SelfAssembly)
                    .WhereConcreteAssignableTo<IPayload>()
                    .NamedBy(t => "Dto#" + t.Name)
                    .Register();

                Assert.True(count >= 2);
                Assert.Equal(typeof(PayloadA), TypeResolverHelper.FindType("Dto#PayloadA"));
                Assert.Equal(typeof(PayloadB), TypeResolverHelper.FindType("Dto#PayloadB"));
                Assert.Equal("Dto#PayloadA", TypeResolverHelper.GetName(typeof(PayloadA)));
                Assert.Equal("Dto#PayloadB", TypeResolverHelper.GetName(typeof(PayloadB)));
            }
            finally
            {
                Cleanup("Dto#PayloadA", "Dto#PayloadB");
            }
        }

        [Fact]
        public void Scan_Types_ExplicitOnly_RegistersExactlyProvided()
        {
            try
            {
                var count = TypeResolverInitializer
                    .ScanTypes(new[] { typeof(PayloadA), typeof(PayloadB), typeof(NotAPayload) })
                    .Where(t => t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) == false
                                && t != typeof(NotAPayload))
                    .Register();

                Assert.Equal(2, count);
                Assert.Equal(typeof(PayloadA), TypeResolverHelper.FindType("PayloadA"));
                Assert.Null(TypeResolverHelper.FindType("NotAPayload"));
            }
            finally
            {
                Cleanup("PayloadA", "PayloadB");
            }
        }

        [Fact]
        public void Scan_Types_WithoutFilter_RegistersAllProvided()
        {
            try
            {
                var count = TypeResolverInitializer
                    .ScanTypes(new[] { typeof(PayloadA), typeof(NotAPayload) })
                    .Register();

                Assert.Equal(2, count);
                Assert.Equal(typeof(PayloadA), TypeResolverHelper.FindType("PayloadA"));
                Assert.Equal(typeof(NotAPayload), TypeResolverHelper.FindType("NotAPayload"));
            }
            finally
            {
                Cleanup("PayloadA", "NotAPayload");
            }
        }

        [Fact]
        public void Scan_EnumerableAssembly_And_CombinedWhere_Works()
        {
            try
            {
                var builder = TypeResolverInitializer.Scan((IEnumerable<Assembly>)new[] { SelfAssembly })
                    .Where(t => t != typeof(NotAPayload))
                    .WhereConcreteAssignableTo<IPayload>();
                var count = builder.Register();

                Assert.True(count >= 2);
                Assert.Equal(typeof(PayloadB), TypeResolverHelper.FindType("PayloadB"));
            }
            finally
            {
                Cleanup("PayloadA", "PayloadB");
            }
        }
    }
}