using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        public void SerializeAndDeserialize_ValueExpr_Null_RoundTrips()
        {
            var expr = new ValueExpr(null);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Null(valueExpr.Value);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_NonConst_RoundTrips()
        {
            var expr = new ValueExpr("hello") { IsConst = false };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_DateTime_RoundTripsWithTypeMarker()
        {
            var value = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
            Expr expr = new ValueExpr(value) { IsConst = true };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Contains("\"$datetime\"", json);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Equal(value, valueExpr.Value);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_DateTimeOffset_RoundTripsWithTypeMarker()
        {
            var value = new DateTimeOffset(2024, 1, 15, 10, 30, 45, TimeSpan.FromHours(8));
            Expr expr = new ValueExpr(value) { IsConst = false };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Contains("\"$datetimeoffset\"", json);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Equal(value, valueExpr.Value);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_TimeSpanArray_RoundTripsWithTypeMarkers()
        {
            var value = new[] { TimeSpan.FromMinutes(5), TimeSpan.FromHours(1) };
            Expr expr = new ValueExpr(value) { IsConst = true };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Contains("\"$timespan\"", json);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            var actual = Assert.IsType<List<object>>(valueExpr.Value);
            Assert.Equal(value.Length, actual.Count);
            Assert.Equal(value[0], Assert.IsType<TimeSpan>(actual[0]));
            Assert.Equal(value[1], Assert.IsType<TimeSpan>(actual[1]));
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_Guid_RoundTripsWithTypeMarker()
        {
            var value = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
            Expr expr = new ValueExpr(value) { IsConst = true };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Contains("\"$guid\"", json);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Equal(value, valueExpr.Value);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueExpr_ByteArray_RoundTripsWithTypeMarker()
        {
            var value = new byte[] { 1, 2, 3, 255 };
            Expr expr = new ValueExpr(value) { IsConst = false };

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Contains("\"$bytes\"", json);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.Equal(value, Assert.IsType<byte[]>(valueExpr.Value));
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
        public void SerializeAndDeserialize_PropertyExpr_NoAlias_RoundTrips()
        {
            var expr = Expr.Prop("Name");

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
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

        [Theory]
        [InlineData(LogicOperator.Equal)]
        [InlineData(LogicOperator.NotEqual)]
        [InlineData(LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.LessThan)]
        [InlineData(LogicOperator.GreaterThanOrEqual)]
        [InlineData(LogicOperator.LessThanOrEqual)]
        [InlineData(LogicOperator.Like)]
        [InlineData(LogicOperator.In)]
        public void SerializeAndDeserialize_LogicBinaryExpr_AllOperators_RoundTrips(LogicOperator op)
        {
            Expr expr = new LogicBinaryExpr(Expr.Prop("Field"), op, new ValueExpr(1));

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

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

        [Fact]
        public void SerializeAndDeserialize_AndExpr_RoundTrips()
        {
            var expr = new AndExpr(Expr.Prop("A") == 1, Expr.Prop("B") == 2, Expr.Prop("C") == 3);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_AndExpr_Empty_RoundTrips()
        {
            var expr = new AndExpr();

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_OrExpr_RoundTrips()
        {
            var expr = new OrExpr(Expr.Prop("A") == 1, Expr.Prop("B") == 2);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_NotExpr_RoundTrips()
        {
            var expr = new NotExpr(Expr.Prop("Id") == 1);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Theory]
        [InlineData(ValueOperator.Add)]
        [InlineData(ValueOperator.Subtract)]
        [InlineData(ValueOperator.Multiply)]
        [InlineData(ValueOperator.Divide)]
        [InlineData(ValueOperator.Concat)]
        public void SerializeAndDeserialize_ValueBinaryExpr_AllOperators_RoundTrips(ValueOperator op)
        {
            Expr expr = new ValueBinaryExpr(Expr.Prop("X"), op, new ValueExpr(1));

            var json = JsonSerializer.Serialize(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_FunctionExpr_RoundTrips()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_FunctionExpr_IsAggregate_RoundTrips()
        {
            var expr = new FunctionExpr("COUNT") { IsAggregate = true };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var func = Assert.IsType<FunctionExpr>(result);
            Assert.True(func.IsAggregate);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueSet_RoundTrips()
        {
            var expr = new ValueSet(Expr.Prop("A"), new ValueExpr(1), new ValueExpr("x"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_ValueSet_WithJoinType_RoundTrips()
        {
            var expr = new ValueSet(ValueJoinType.Concat, Expr.Prop("A"), Expr.Prop("B"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_UnaryExpr_RoundTrips()
        {
            var expr = new UnaryExpr(UnaryOperator.Nagive, new ValueExpr(42));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_UnaryExpr_Distinct_RoundTrips()
        {
            var expr = new UnaryExpr(UnaryOperator.Distinct, Expr.Prop("Id"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_GenericSqlExpr_RoundTrips()
        {
            var expr = new GenericSqlExpr("myKey") { Arg = "myArg" };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var sqlExpr = Assert.IsType<GenericSqlExpr>(result);
            Assert.Equal("myKey", sqlExpr.Key);
            Assert.Equal("myArg", sqlExpr.Arg?.ToString());
        }

        [Fact]
        public void SerializeAndDeserialize_ForeignExpr_RoundTrips()
        {
            var expr = new ForeignExpr(typeof(string), Expr.Prop("Id") == 1, "Shard1");

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var foreign = Assert.IsType<ForeignExpr>(result);
            Assert.Equal(typeof(string), foreign.Foreign);
            Assert.Equal("Shard1", foreign.TableArgs[0]);
        }

        [Fact]
        public void SerializeAndDeserialize_ForeignExpr_WithAutoRelated_RoundTrips()
        {
            var expr = new ForeignExpr(typeof(int)) { AutoRelated = true };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var foreign = Assert.IsType<ForeignExpr>(result);
            Assert.True(foreign.AutoRelated);
        }

        [Fact]
        public void SerializeAndDeserialize_WhereExpr_RoundTrips()
        {
            var expr = new WhereExpr(new FromExpr(typeof(string)), Expr.Prop("Id") == 1);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_GroupByExpr_RoundTrips()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(string)), Expr.Prop("DeptId"), Expr.Prop("Status"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_HavingExpr_RoundTrips()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_OrderByExpr_RoundTrips()
        {
            var expr = new OrderByExpr(new FromExpr(typeof(string)), new OrderByItemExpr(Expr.Prop("Name"), true));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_OrderByExpr_Descending_RoundTrips()
        {
            var expr = new OrderByExpr(new FromExpr(typeof(string)), new OrderByItemExpr(Expr.Prop("Age"), false));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_OrderByItemExpr_RoundTrips()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), false);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_SectionExpr_RoundTrips()
        {
            var expr = new SectionExpr(new FromExpr(typeof(string)), 10, 20);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_SelectExpr_RoundTrips()
        {
            var expr = new SelectExpr(new FromExpr(typeof(string)), Expr.Prop("Name").As("UserName"), Expr.Prop("Age").As("UserAge"));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_SelectItemExpr_RoundTrips()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"), "UserName");

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_UpdateExpr_RoundTrips()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(string)), Expr.Prop("Id") == 1);
            expr.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("newName")));

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_DeleteExpr_RoundTrips()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(string)), Expr.Prop("Id") == 1);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_TableJoinExpr_RoundTrips()
        {
            var expr = new TableJoinExpr(new TableExpr(typeof(int)), Expr.Prop("Id") == Expr.Prop("ForeignId"))
            {
                JoinType = TableJoinType.Inner
            };

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var join = Assert.IsType<TableJoinExpr>(result);
            Assert.Equal(TableJoinType.Inner, join.JoinType);
        }

        [Fact]
        public void SerializeAndDeserialize_FromExpr_WithJoins_RoundTrips()
        {
            var expr = new FromExpr(typeof(string));
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(int)), Expr.Prop("Id") == Expr.Prop("ForeignId"))
            {
                JoinType = TableJoinType.Left
            });

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_ComplexChainedQuery_RoundTrips()
        {
            var expr = Expr.From(typeof(string))
                .Where(Expr.Prop("Age") > 18)
                .OrderBy(Expr.Prop("Name").Asc())
                .Section(0, 10);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            Assert.Equal(expr, result);
        }

        [Fact]
        public void SerializeAndDeserialize_CommonTableExpr_RoundTrips()
        {
            var cteSelect = new SelectExpr(new FromExpr(typeof(string)), Expr.Prop("Id").As("Id")) { Alias = "MyCTE" };
            var expr = new CommonTableExpr(cteSelect);

            var json = JsonSerializer.Serialize<Expr>(expr);
            var result = JsonSerializer.Deserialize<Expr>(json);

            var cte = Assert.IsType<CommonTableExpr>(result);
            Assert.Equal("MyCTE", cte.Alias);
        }

        [Fact]
        public void SerializeAndDeserialize_DuplicateCommonTableAlias_UsesAliasOnlyAfterFirst()
        {
            var first = new SelectExpr(new FromExpr(typeof(string)), Expr.Prop("Id").As("Id")).With("MyCTE");
            var second = new SelectExpr(new FromExpr(typeof(string)), Expr.Prop("Id").As("Id")).With("MyCTE");
            var expr = first
                .Where(Expr.Prop("Id").In(second.Select(Expr.Prop("Id"))))
                .Select(Expr.Prop("Id"));

            var json = JsonSerializer.Serialize<Expr>(expr);

            Assert.Equal(1, Regex.Matches(json, "\"Alias\":\"MyCTE\"").Count);
            Assert.Contains("\"$cte\":\"MyCTE\"", json);

            var result = JsonSerializer.Deserialize<Expr>(json);
            Assert.Equal(expr, result);
        }

        [Fact]
        public void Serialize_DifferentCommonTableAliasDefinitions_ThrowsInvalidOperationException()
        {
            var first = new SelectExpr(new FromExpr(typeof(string)), Expr.Prop("Id").As("Id")).With("MyCTE");
            var second = new SelectExpr(
                new FromExpr(typeof(string)).Where(Expr.Prop("Age") > 18),
                Expr.Prop("Id").As("Id")).With("MyCTE");
            var expr = first
                .Where(Expr.Prop("Id").In(second.Select(Expr.Prop("Id"))))
                .Select(Expr.Prop("Id"));

            var ex = Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize<Expr>(expr));
            Assert.Contains("MyCTE", ex.Message);
        }
    }
}
