using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ArrayJsonColumnTests : TestBase
    {
        public ArrayJsonColumnTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task ArrayColumns_RoundTrip_ViaJsonFallback()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ArrayJsonModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ArrayJsonModel>>();

            var model = new ArrayJsonModel
            {
                Tags = new[] { "tag1", "tag2" },
                Scores = new List<int> { 1, 2, 3 },
                Meta = "{\"enabled\":true}",
                Detail = "{\"count\":5}"
            };

            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(retrieved);
            Assert.Equal(new[] { "tag1", "tag2" }, retrieved.Tags);
            Assert.Equal(new List<int> { 1, 2, 3 }, retrieved.Scores);
            Assert.Equal("{\"enabled\":true}", retrieved.Meta);
            Assert.Equal("{\"count\":5}", retrieved.Detail);
        }

        [Fact]
        public async Task JsonColumn_ComplexObject_ShouldRoundTrip()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<ArrayJsonModel>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<ArrayJsonModel>>();

            var model = new ArrayJsonModel { Profile = new ProfileData { Nick = "nick", Level = 3 } };
            await service.InsertAsync(model, TestContext.Current.CancellationToken);

            var retrieved = await viewService.GetObjectAsync(model.Id, cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(retrieved);
            Assert.NotNull(retrieved.Profile);
            Assert.Equal("nick", retrieved.Profile.Nick);
            Assert.Equal(3, retrieved.Profile.Level);
        }

        [Table("ArrayJsonModels")]
        public class ArrayJsonModel
        {
            [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [Column("Tags", AllowNull = true)]
            public string[]? Tags { get; set; }

            [Column("Scores", AllowNull = true)]
            public List<int>? Scores { get; set; }

            [Column("Meta", DbType = DbValueType.Json, AllowNull = true)]
            public string? Meta { get; set; }

            [Column("Detail", DbType = DbValueType.Jsonb, AllowNull = true)]
            public string? Detail { get; set; }

            [Column("Profile", DbType = DbValueType.Json, AllowNull = true)]
            public ProfileData? Profile { get; set; }
        }

        public class ProfileData
        {
            public string? Nick { get; set; }
            public int Level { get; set; }
        }
    }
}
