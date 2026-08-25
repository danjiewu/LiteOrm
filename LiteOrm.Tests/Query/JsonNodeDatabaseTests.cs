using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>带 JsonNode 列的实际表实体（Data 列以 DbValueType.Json 存储，SQLite 下为 TEXT）。</summary>
    [Table("TestJsonEntities")]
    public class TestJsonEntity
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Data")]
        public JsonNode? Data { get; set; }
    }

    /// <summary>
    /// JsonNode 表达式映射的实际数据库查询测试。
    /// 覆盖：写方向 JsonNode→JSON 字符串持久化、读方向字符串→JsonNode 还原，
    /// 以及索引器（JsonExtract）与 GetValue&lt;T&gt;()（JsonValue）在真实 SQL 中执行。
    /// 使用 SQLite 内存库（其 json_extract / json_value 原生函数）。
    /// </summary>
    [Collection("Database")]
    public class JsonNodeDatabaseTests : TestBase
    {
        public JsonNodeDatabaseTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task InsertQuery_IndexerGetValue_QueriesRealJsonColumn()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestJsonEntity>>();
            await dao.InsertAsync(new TestJsonEntity
            {
                Data = JsonNode.Parse("{\"name\":\"Lite\",\"age\":30,\"tags\":[\"a\",\"b\"]}")
            }, TestContext.Current.CancellationToken);

            var view = ServiceProvider.GetRequiredService<IObjectViewDAO<TestJsonEntity>>();
            var query = Expr.Query<TestJsonEntity, IQueryable<TestJsonEntity>>(q => q
                .Where(e => e.Data!["name"]!.GetValue<string>() == "Lite"));

            var rows = await view.Search(query).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(rows);
            Assert.Equal("Lite", rows[0].Data?["name"]?.GetValue<string>());

            // 反向确认：不匹配条件不应命中
            var none = await view.Search(Expr.Query<TestJsonEntity, IQueryable<TestJsonEntity>>(q => q
                .Where(e => e.Data!["name"]!.GetValue<string>() == "NotExist"))).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Empty(none);
        }

        [Fact]
        public async Task InsertQuery_NestedIndexer_BuildsNestedJsonPath()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestJsonEntity>>();
            await dao.InsertAsync(new TestJsonEntity
            {
                Data = JsonNode.Parse("{\"user\":{\"name\":\"Alice\"}}")
            }, TestContext.Current.CancellationToken);

            var view = ServiceProvider.GetRequiredService<IObjectViewDAO<TestJsonEntity>>();
            var query = Expr.Query<TestJsonEntity, IQueryable<TestJsonEntity>>(q => q
                .Where(e => e.Data!["user"]!["name"]!.GetValue<string>() == "Alice"));

            var rows = await view.Search(query).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(rows);
            Assert.Equal("Alice", rows[0].Data?["user"]?["name"]?.GetValue<string>());
        }

        [Fact]
        public async Task InsertRead_RoundTripsJsonNode()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestJsonEntity>>();
            var node = JsonNode.Parse("{\"price\":19.9,\"active\":true}");
            await dao.InsertAsync(new TestJsonEntity { Data = node }, TestContext.Current.CancellationToken);

            var view = ServiceProvider.GetRequiredService<IObjectViewDAO<TestJsonEntity>>();
            var all = await view.Search(Expr.Query<TestJsonEntity, IQueryable<TestJsonEntity>>(q => q
                .Where(e => e.Data != null))).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(all);
            Assert.Equal(19.9m, all[0].Data?["price"]!.GetValue<decimal>());
            Assert.True(all[0].Data?["active"]!.GetValue<bool>() == true);
        }
    }
}