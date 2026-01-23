using LiteOrm.Common;
using LiteOrm.Tests.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace LiteOrm.Tests
{
    public class ExprTests
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void TestSerialization<T>(T expr) where T : Expr
        {
            string json = JsonSerializer.Serialize<Expr>(expr, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;

            // Use Equals override
            Assert.True(expr.Equals(deserialized), $"Deserialized expr should be equal to original. JSON: {json}");
            
            // HashCode test is now robust after framework fix
            Assert.Equal(expr.GetHashCode(), deserialized.GetHashCode());
        }

        [Fact]
        public void ValueExpr_Tests()
        {
            // Equals
            Expr e1 = (Expr)123;
            Expr e2 = (Expr)123;
            Expr e3 = (Expr)456;
            Expr e4 = (Expr)123L; // Numeric equality check

            Assert.True(e1.Equals(e2));
            Assert.False(e1.Equals(e3));
            Assert.True(e1.Equals(e4));

            Expr s1 = (Expr)"test";
            Expr s2 = (Expr)"test";
            Expr s3 = (Expr)"other";
            Assert.True(s1.Equals(s2));
            Assert.False(s1.Equals(s3));

            Expr n1 = Expr.Null;
            Expr n2 = new ValueExpr(null);
            Assert.True(n1.Equals(n2));

            Expr list1 = new ValueExpr(new List<int> { 1, 2, 3 });
            Expr list2 = new ValueExpr(new int[] { 1, 2, 3 });
            Assert.True(list1.Equals(list2));

            // Serialization
            TestSerialization(e1);
            TestSerialization(e4);
            TestSerialization(s1);
            TestSerialization(n1);
            TestSerialization(list1);
            
            // Non-const value serialization
            Expr nonConst = new ValueExpr("dynamic") { IsConst = false };
            TestSerialization(nonConst);
        }

        [Fact]
        public void PropertyExpr_Tests()
        {
            // Equals
            Expr p1 = Expr.Property("Name");
            Expr p2 = Expr.Property("Name");
            Expr p3 = Expr.Property("Age");

            Assert.True(p1.Equals(p2));
            Assert.False(p1.Equals(p3));

            // Serialization
            TestSerialization(p1);
            TestSerialization(p3);
        }

        [Fact]
        public void BinaryExpr_Tests()
        {
            // Equals
            Expr b1 = Expr.Property("Age") > 18;
            Expr b2 = Expr.Property("Age") > 18;
            Expr b3 = Expr.Property("Age") >= 18;
            Expr b4 = Expr.Property("Name") == "John";

            Assert.True(b1.Equals(b2));
            Assert.False(b1.Equals(b3));
            Assert.False(b1.Equals(b4));

            // Serialization
            TestSerialization(b1);
            TestSerialization(b4);
            
            // Complex Binary
            Expr complex = (Expr.Property("Price") * 1.1) > 100;
            TestSerialization(complex);
        }

        [Fact]
        public void UnaryExpr_Tests()
        {
            // Equals
            Expr u1 = !Expr.Property("IsDeleted");
            Expr u2 = !Expr.Property("IsDeleted");
            Expr u3 = !Expr.Property("IsActive");

            Assert.True(u1.Equals(u2));
            Assert.False(u1.Equals(u3));

            // Serialization
            TestSerialization(u1);
        }

        [Fact]
        public void ExprSet_Tests()
        {
            // Equals
            Expr s1 = Expr.Property("Age") > 18 & Expr.Property("Name") == "John";
            Expr s2 = Expr.Property("Name") == "John" & Expr.Property("Age") > 18;
            Expr s3 = Expr.Property("Age") > 18 | Expr.Property("Name") == "John";

            Assert.True(s1.Equals(s2), "And set should be equal regardless of order");
            Assert.False(s1.Equals(s3), "And set should not be equal to Or set");

            // List type ExprSet (IN clause style)
            var inSet1 = new ExprSet(ExprJoinType.List, (Expr)1, (Expr)2, (Expr)3);
            var inSet2 = new ExprSet(ExprJoinType.List, (Expr)1, (Expr)2, (Expr)3);
            var inSet3 = new ExprSet(ExprJoinType.List, (Expr)3, (Expr)2, (Expr)1);

            Assert.True(inSet1.Equals(inSet2));
            Assert.False(inSet1.Equals(inSet3), "Order should matter for list JoinType");

            // Serialization
            TestSerialization(s1);
            TestSerialization(s3);
            TestSerialization(inSet1);
        }

        [Fact]
        public void LambdaExpr_Tests()
        {
            // Equals
            // LambdaExpr uses LambdaExprConverter.ToExpr() for equality check
            Expr l1 = Expr.Exp<TestUser>(u => u.Age > 18);
            Expr l2 = Expr.Exp<TestUser>(u => u.Age > 18);
            Expr l3 = Expr.Exp<TestUser>(u => u.Age > 20);

            Assert.True(l1.Equals(l2));
            Assert.False(l1.Equals(l3));

            // Serialization
            // Note: LambdaExpr serializes as its InnerExpr
            string json = JsonSerializer.Serialize<Expr>(l1, _jsonOptions);
            Expr deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions)!;
            
            // deserialized will NOT be LambdaExpr, but the converted BinaryExpr
            Assert.IsType<BinaryExpr>(deserialized);
            Assert.True(l1.Equals(deserialized));
        }

        [Fact]
        public void ForeignExpr_Tests()
        {
            // Equals
            Expr f1 = Expr.Foreign("DeptId", Expr.Property("Name") == "IT");
            Expr f2 = Expr.Foreign("DeptId", Expr.Property("Name") == "IT");
            Expr f3 = Expr.Foreign("DeptId", Expr.Property("Name") == "HR");
            Expr f4 = Expr.Foreign("ManagerId", Expr.Property("Name") == "IT");

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.False(f1.Equals(f4));

            // Serialization
            TestSerialization(f1);
        }

        [Fact]
        public void GenericSqlExpr_Tests()
        {
            // Register a dummy handler
            GenericSqlExpr.Register("TestKey", (ctx, builder, pms, arg) => "TEST SQL");

            // Equals
            Expr g1 = GenericSqlExpr.Get("TestKey", 123);
            Expr g2 = GenericSqlExpr.Get("TestKey", 123);
            Expr g3 = GenericSqlExpr.Get("TestKey", 456);
            Expr g4 = GenericSqlExpr.GetStaticSqlExpr("TestKey");

            Assert.True(g1.Equals(g2));
            Assert.False(g1.Equals(g3));
            Assert.False(g1.Equals(g4));

            // Serialization
            TestSerialization(g1);
            TestSerialization(g4);
        }

        [Fact]
        public void FunctionExpr_Tests()
        {
            // Equals
            Expr f1 = new FunctionExpr("Now");
            Expr f2 = new FunctionExpr("Now");
            Expr f3 = new FunctionExpr("Today");
            Expr f4 = new FunctionExpr("ABS", (Expr)(-10));
            Expr f5 = new FunctionExpr("ABS", (Expr)(-10));

            Assert.True(f1.Equals(f2));
            Assert.False(f1.Equals(f3));
            Assert.True(f4.Equals(f5));

            // Serialization
            TestSerialization(f1);
            TestSerialization(f4);
        }
    }
}
