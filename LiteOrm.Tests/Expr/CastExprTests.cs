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
            var expr = Expr.Prop("Age").Cast(DbType.Int32);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
            Assert.False(func.IsAggregate);
        }

        [Fact]
        public void Cast_WithDbType_FirstArgIsSourceExpr()
        {
            var ageExpr = Expr.Prop("Age");
            var expr = ageExpr.Cast(DbType.String);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Same(ageExpr, func.Args[0]);
        }

        [Fact]
        public void Cast_WithDbType_SecondArgIsValueExprWithDbType()
        {
            var expr = Expr.Prop("Score").Cast(DbType.Decimal);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.Decimal, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeString_SecondArgIsString()
        {
            var expr = Expr.Prop("Name").Cast(DbType.String);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.String, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeDateTime_SecondArgIsDateTime()
        {
            var expr = Expr.Prop("CreateTime").Cast(DbType.DateTime);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.DateTime, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeDouble_SecondArgIsDouble()
        {
            var expr = Expr.Prop("Amount").Cast(DbType.Double);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.Double, typeArg.Value);
        }

        [Fact]
        public void Cast_ChainedWithOtherExpr_Works()
        {
            var expr = Expr.Prop("Age").Cast(DbType.Int32) > new ValueExpr(18);

            var bin = Assert.IsType<LogicBinaryExpr>(expr);
            Assert.Equal(LogicOperator.GreaterThan, bin.Operator);
            var castFunc = Assert.IsType<FunctionExpr>(bin.Left);
            Assert.Equal("CAST", castFunc.FunctionName);
        }

        [Fact]
        public void Cast_NullableProperty_Works()
        {
            var expr = Expr.Prop("OptionalValue").Cast(DbType.Int32);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
        }

        [Fact]
        public void Cast_ComputedExpr_Works()
        {
            var computed = Expr.Prop("Price") * Expr.Prop("Quantity");
            var expr = computed.Cast(DbType.Decimal);

            var func = Assert.IsType<FunctionExpr>(expr);
            Assert.Equal("CAST", func.FunctionName);
            Assert.IsType<ValueBinaryExpr>(func.Args[0]);
        }

        [Fact]
        public void Cast_WithDbTypeByte_SecondArgIsByte()
        {
            var expr = Expr.Prop("Flag").Cast(DbType.Byte);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.Byte, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeBoolean_SecondArgIsBoolean()
        {
            var expr = Expr.Prop("IsActive").Cast(DbType.Boolean);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.Boolean, typeArg.Value);
        }

        [Fact]
        public void Cast_WithDbTypeInt64_SecondArgIsInt64()
        {
            var expr = Expr.Prop("BigId").Cast(DbType.Int64);

            var func = Assert.IsType<FunctionExpr>(expr);
            var typeArg = Assert.IsType<ValueExpr>(func.Args[1]);
            Assert.Equal(DbType.Int64, typeArg.Value);
        }

        [Fact]
        public void Cast_Clone_ProducesEqualExpr()
        {
            var original = Expr.Prop("Age").Cast(DbType.String);

            var clone = (FunctionExpr)original.Clone();

            Assert.Equal(original, clone);
            Assert.Equal(original.FunctionName, clone.FunctionName);
            Assert.Equal(original.Args.Count, clone.Args.Count);
        }
    }
}