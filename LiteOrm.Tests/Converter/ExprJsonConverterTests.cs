using System.Text.Json;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ExprJsonConverterTests
    {
        [Fact]
        public void SerializeAndDeserialize_ValueExpr_RoundTrips()
        {
            var expr = new ValueExpr(42) { IsConst = true };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Equal(42, valueExpr.Value);
        }

        [Fact]
        public void SerializeAndDeserialize_PropertyExpr_RoundTrips()
        {
            var expr = Expr.Prop("t", "Name");

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var propertyExpr = Assert.IsType<PropertyExpr>(result);
            Assert.Equal("t", propertyExpr.TableAlias);
            Assert.Equal("Name", propertyExpr.PropertyName);
        }

        [Fact]
        public void SerializeAndDeserialize_LogicBinaryExpr_RoundTrips()
        {
            Expr expr = Expr.Prop("Age") > 18;

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_TableExpr_RoundTrips()
        {
            Expr expr = new TableExpr(typeof(string)) { Alias = "t", TableArgs = new[] { "A" } };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.IsType<TableExpr>(result);
            Assert.Equal(expr, result);
        }
    }
}
