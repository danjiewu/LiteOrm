using System.Data;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class CastExprTests
    {
        [Fact]
        public void Cast_WithDbType_CreatesFunctionExpr()
        {
            var expr = Expr.Prop("Age").Cast(DbValueType.Int32);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
            Assert.False(func.IsAggregate);
        }

        [Fact]
        public void Cast_WithDbType_FirstArgIsSourceExpr()
        {
            var ageExpr = Expr.Prop("Age");
            var expr = ageExpr.Cast(DbValueType.String);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Same(ageExpr, func.Args[0]);
        }

        [Fact]
        public void Cast_WithDbType_SecondArgIsValueExprWithDbType()
        {
            var expr = Expr.Prop("Score").Cast(DbValueType.Decimal);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.Decimal, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeString_SecondArgIsString()
        {
            var expr = Expr.Prop("Name").Cast(DbValueType.String);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.String, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeDateTime_SecondArgIsDateTime()
        {
            var expr = Expr.Prop("CreateTime").Cast(DbValueType.DateTime);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.DateTime, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeDouble_SecondArgIsDouble()
        {
            var expr = Expr.Prop("Amount").Cast(DbValueType.Double);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.Double, typeArg.Value);
        }

        [Fact]
        public void Cast_ChainedWithOtherExpr_Works()
        {
            var expr = Expr.Prop("Age").Cast(DbValueType.Int32) > new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(expr);
            Assert.Equal(LogicOperator.GreaterThan, bin.Operator);
            var castFunc = Assert.IsType<FunctionExpr>(bin.Left);
            Assert.Equal("CAST", castFunc.FunctionName);
        }

        [Fact]
        public void Cast_NullableProperty_Works()
        {
            var expr = Expr.Prop("OptionalValue").Cast(DbValueType.Int32);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
        }

        [Fact]
        public void Cast_ComputedExpr_Works()
        {
            var computed = Expr.Prop("Price") * Expr.Prop("Quantity");
            var expr = computed.Cast(DbValueType.Decimal);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.IsType<ValueBinaryExpr>(func.Args[0]);
        }

        [Fact]
        public void Cast_WithDbValueTypeByte_SecondArgIsByte()
        {
            var expr = Expr.Prop("Flag").Cast(DbValueType.Byte);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.Byte, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeBoolean_SecondArgIsBoolean()
        {
            var expr = Expr.Prop("IsActive").Cast(DbValueType.Boolean);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.Boolean, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeInt64_SecondArgIsInt64()
        {
            var expr = Expr.Prop("BigId").Cast(DbValueType.Int64);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbValueType.Int64, typeArg.Value);
        }

        [Fact]
        public void Cast_Clone_ProducesEqualExpr()
        {
            var original = Expr.Prop("Age").Cast(DbValueType.String);

            var clone = (FunctionExpr)original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.FunctionName, clone.FunctionName);
            Assert.Equal(original.Args.Count, clone.Args.Count);
        }
    }
}