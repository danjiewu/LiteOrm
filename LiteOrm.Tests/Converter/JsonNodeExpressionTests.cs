using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// JsonNode 索引器与 GetValue&lt;T&gt;() 的表达式映射测试。
    /// 索引器 → JsonExtract（常量键拼接 JSON 路径，动态键经 Concat 参数拼接）；
    /// GetValue&lt;T&gt;() → JsonValue 标量提取。未映射的 JsonNode 方法不注册，保持默认函数名解析。
    /// 纯内存测试，无需数据库连接。
    /// </summary>
    public class JsonNodeExpressionTests
    {
        private class JsonTestEntity
        {
            public JsonNode Data { get; set; } = null!;

            // 动态键：方法调用不可编译期求值，用于触发 Concat 拼接路径
            public string CurrentKey() => "dynamic";
        }

        [Fact]
        public void Index_ConstantKey_MapsToJsonExtract()
        {
            Expression<Func<JsonTestEntity, JsonNode?>> expr = e => e.Data["name"];

            var fn = Assert.IsType<FunctionExpr>(LambdaExprConverter.ToValueExpr(expr));
            Assert.Equal("JsonExtract", fn.FunctionName);

            var baseExpr = Assert.IsType<PropertyExpr>(fn.Args[0]);
            Assert.Equal("Data", baseExpr.PropertyName);

            var path = Assert.IsType<ValueExpr>(fn.Args[1]);
            Assert.Equal("$.name", path.Value);
        }

        [Fact]
        public void Index_NestedConstantKeys_BuildsJoinedPath()
        {
            Expression<Func<JsonTestEntity, JsonNode?>> expr = e => e.Data["a"]!["b"]!["c"];

            var fn = Assert.IsType<FunctionExpr>(LambdaExprConverter.ToValueExpr(expr));
            Assert.Equal("JsonExtract", fn.FunctionName);

            var path = Assert.IsType<ValueExpr>(fn.Args[1]);
            Assert.Equal("$.a.b.c", path.Value);
        }

        [Fact]
        public void Index_DynamicKey_UsesConcatJoin()
        {
            Expression<Func<JsonTestEntity, JsonNode?>> expr = e => e.Data["a"]![e.CurrentKey()]!["c"];

            var fn = Assert.IsType<FunctionExpr>(LambdaExprConverter.ToValueExpr(expr));
            Assert.Equal("JsonExtract", fn.FunctionName);

            var path = Assert.IsType<ValueSet>(fn.Args[1]);
            Assert.Equal(ValueJoinType.Concat, path.JoinType);

            // 常量前缀 "$.a" 被保留，动态键以参数形式混入
            Assert.Contains(path, p => p is ValueExpr ve && Equals(ve.Value, "$.a"));
            Assert.Contains(path, p => p is FunctionExpr fe && fe.FunctionName == "CurrentKey");
        }

        [Fact]
        public void GetValue_OnIndexRow_MapsToJsonValue()
        {
            Expression<Func<JsonTestEntity, decimal>> expr = e => e.Data["price"]!.GetValue<decimal>();

            var outer = Assert.IsType<FunctionExpr>(LambdaExprConverter.ToValueExpr(expr));
            Assert.Equal("JsonValue", outer.FunctionName);

            // 直接从对象链构建单一路径，不再嵌套 JsonExtract
            var baseExpr = Assert.IsType<PropertyExpr>(outer.Args[0]);
            Assert.Equal("Data", baseExpr.PropertyName);

            var path = Assert.IsType<ValueExpr>(outer.Args[1]);
            Assert.Equal("$.price", path.Value);
        }
    }
}