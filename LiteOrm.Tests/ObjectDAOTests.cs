using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ObjectDAOTests : TestBase
    {
        public ObjectDAOTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task ObjectDAO_Insert_Update_UpdateOrInsert_DeleteByKeys_Delete_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = CreateUser("ObjectDaoSyncCrud", 20);

            Assert.True(dao.Insert(user));
            Assert.True(user.Id > 0);
            Assert.True(await viewDao.ExistsKey(user.Id).GetResultAsync(TestContext.Current.CancellationToken));

            user.Name = "ObjectDaoSyncCrud_Updated";
            user.Age = 25;
            Assert.True(dao.Update(user));

            var updated = await viewDao.GetObject(user.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("ObjectDaoSyncCrud_Updated", updated.Name);
            Assert.Equal(25, updated.Age);

            var existingResult = dao.UpdateOrInsert(user);
            Assert.Equal(UpdateOrInsertResult.Updated, existingResult);

            var insertedUser = CreateUser("ObjectDaoSyncCrud_Inserted", 30);
            var insertedResult = dao.UpdateOrInsert(insertedUser);
            Assert.Equal(UpdateOrInsertResult.Inserted, insertedResult);
            Assert.True(insertedUser.Id > 0);

            Assert.True(dao.DeleteByKeys(insertedUser.Id));
            Assert.False(await viewDao.ExistsKey(insertedUser.Id).GetResultAsync(TestContext.Current.CancellationToken));

            Assert.True(dao.Delete(user));
            Assert.False(await viewDao.ExistsKey(user.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_BatchMethods_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                CreateUser("ObjectDaoBatch_A", 18),
                CreateUser("ObjectDaoBatch_B", 28)
            };

            dao.BatchInsert(users);
            Assert.All(users, user => Assert.True(user.Id > 0));

            users[0].Age = 19;
            users[1].Age = 29;
            dao.BatchUpdate(users);

            var updatedUsers = await viewDao.Search(Expr.Prop("Name").StartsWith("ObjectDaoBatch_")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Contains(updatedUsers, user => user.Name == "ObjectDaoBatch_A" && user.Age == 19);
            Assert.Contains(updatedUsers, user => user.Name == "ObjectDaoBatch_B" && user.Age == 29);

            users[0].Name = "ObjectDaoBatch_A_Upserted";
            users[0].Age = 21;
            var inserted = CreateUser("ObjectDaoBatch_C", 33);
            dao.BatchUpdateOrInsert([users[0], inserted]);

            var upsertedUsers = await viewDao.Search(Expr.Prop("Name").Like("ObjectDaoBatch%")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Contains(upsertedUsers, user => user.Name == "ObjectDaoBatch_A_Upserted" && user.Age == 21);
            Assert.Contains(upsertedUsers, user => user.Name == "ObjectDaoBatch_C" && user.Age == 33);

            dao.BatchDelete([users[0]]);
            Assert.False(await viewDao.ExistsKey(users[0].Id).GetResultAsync(TestContext.Current.CancellationToken));

            dao.BatchDeleteByKeys(new[] { new object[] { users[1].Id }, new object[] { inserted.Id } });
            Assert.False(await viewDao.ExistsKey(users[1].Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(inserted.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_DeleteAndUpdateExpr_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var exprUser = CreateUser("ObjectDaoExpr_Update", 40);
            var deleteUser = CreateUser("ObjectDaoExpr_Delete", 50);

            dao.Insert(exprUser);
            dao.Insert(deleteUser);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Name") == "ObjectDaoExpr_Update");
            updateExpr.Set(("Age", Expr.Prop("Age") + Expr.Const(2)));

            Assert.Equal(1, dao.Update(updateExpr));
            Assert.Equal(1, dao.Delete(Expr.Lambda<TestUser>(u => u.Name == "ObjectDaoExpr_Delete")));

            var updated = await viewDao.GetObject(exprUser.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal(42, updated.Age);
            Assert.False(await viewDao.ExistsKey(deleteUser.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_AsyncCrudMethods_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = CreateUser("ObjectDaoAsyncCrud", 22);

            Assert.True(await dao.InsertAsync(user, TestContext.Current.CancellationToken));
            Assert.True(user.Id > 0);

            user.Name = "ObjectDaoAsyncCrud_Updated";
            user.Age = 26;
            Assert.True(await dao.UpdateAsync(user, cancellationToken: TestContext.Current.CancellationToken));

            var updated = await viewDao.GetObject(user.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("ObjectDaoAsyncCrud_Updated", updated.Name);
            Assert.Equal(26, updated.Age);

            Assert.Equal(UpdateOrInsertResult.Updated, await dao.UpdateOrInsertAsync(user, TestContext.Current.CancellationToken));

            var inserted = CreateUser("ObjectDaoAsyncCrud_Inserted", 35);
            Assert.Equal(UpdateOrInsertResult.Inserted, await dao.UpdateOrInsertAsync(inserted, TestContext.Current.CancellationToken));
            Assert.True(inserted.Id > 0);

            Assert.True(await dao.DeleteByKeysAsync([inserted.Id], TestContext.Current.CancellationToken));
            Assert.True(await dao.DeleteAsync(user, TestContext.Current.CancellationToken));

            Assert.False(await viewDao.ExistsKey(inserted.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(user.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_AsyncBatchAndExprMethods_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestUser>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                CreateUser("ObjectDaoAsyncBatch_A", 31),
                CreateUser("ObjectDaoAsyncBatch_B", 32)
            };

            await dao.BatchInsertAsync(users, TestContext.Current.CancellationToken);
            Assert.All(users, user => Assert.True(user.Id > 0));

            users[0].Age = 41;
            users[1].Age = 42;
            await dao.BatchUpdateAsync(users, TestContext.Current.CancellationToken);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Name") == "ObjectDaoAsyncBatch_A");
            updateExpr.Set(("Age", Expr.Prop("Age") + Expr.Const(1)));
            Assert.Equal(1, await dao.UpdateAsync(updateExpr, TestContext.Current.CancellationToken));

            users[0].Name = "ObjectDaoAsyncBatch_A_Upserted";
            users[0].Age = 50;
            var inserted = CreateUser("ObjectDaoAsyncBatch_C", 43);
            await dao.BatchUpdateOrInsertAsync([users[0], inserted], TestContext.Current.CancellationToken);

            var allUsers = await viewDao.Search(Expr.Prop("Name").Like("ObjectDaoAsyncBatch%")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Contains(allUsers, user => user.Name == "ObjectDaoAsyncBatch_A_Upserted" && user.Age == 50);
            Assert.Contains(allUsers, user => user.Name == "ObjectDaoAsyncBatch_B" && user.Age == 42);
            Assert.Contains(allUsers, user => user.Name == "ObjectDaoAsyncBatch_C" && user.Age == 43);

            Assert.Equal(1, await dao.DeleteAsync(Expr.Lambda<TestUser>(u => u.Name == "ObjectDaoAsyncBatch_B"), TestContext.Current.CancellationToken));
            await dao.BatchDeleteAsync([users[0]], TestContext.Current.CancellationToken);
            await dao.BatchDeleteByKeysAsync(new[] { new object[] { inserted.Id } }, TestContext.Current.CancellationToken);

            Assert.False(await viewDao.ExistsKey(users[0].Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(users[1].Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(inserted.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_WithArgs_ShouldTargetShardTable()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestLog>>().WithArgs("202405");
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestLogView>>().WithArgs("202405");

            var user = CreateUser("ObjectDaoShardUser", 27);
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var log = new TestLog
            {
                Event = "ObjectDaoShard",
                Amount = 321,
                CreateTime = new DateTime(2024, 5, 20),
                Duration = TimeSpan.FromMinutes(15),
                UserID = user.Id
            };

            Assert.True(dao.Insert(log));
            Assert.True(log.Id > 0);

            var fetched = await viewDao.Search(Expr.Lambda<TestLogView>(l => l.Event == "ObjectDaoShard" && l.UserID == user.Id)).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(fetched);
            Assert.Equal("ObjectDaoShard", fetched.Event);
            Assert.Equal("ObjectDaoShardUser", fetched.UserName);
        }

        [Fact]
        public async Task ObjectDAO_ShortIdentityKeyCrudAndBatch_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestShortIdentityEntity>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestShortIdentityEntity>>();

            var entity = CreateShortIdentityEntity("ShortIdentity_Single", 1);
            Assert.True(dao.Insert(entity));
            Assert.True(entity.Id > 0);
            Assert.True(await viewDao.ExistsKey(entity.Id).GetResultAsync(TestContext.Current.CancellationToken));

            entity.Name = "ShortIdentity_Single_Updated";
            entity.Quantity = 2;
            Assert.True(dao.Update(entity));

            var updated = await viewDao.GetObject(entity.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("ShortIdentity_Single_Updated", updated.Name);
            Assert.Equal(2, updated.Quantity);

            var batchA = CreateShortIdentityEntity("ShortIdentity_Batch_A", 10);
            var batchB = CreateShortIdentityEntity("ShortIdentity_Batch_B", 20);
            dao.BatchInsert([batchA, batchB]);
            Assert.All([batchA, batchB], item => Assert.True(item.Id > 0));

            batchA.Quantity = 11;
            batchB.Quantity = 21;
            dao.BatchUpdate([batchA, batchB]);

            dao.BatchDelete([batchA]);
            dao.BatchDeleteByKeys(new object[] { batchB.Id, entity.Id });

            Assert.False(await viewDao.ExistsKey(batchA.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(batchB.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(entity.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_LongIdentityKeyCrudAndBatch_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestLongIdentityEntity>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestLongIdentityEntity>>();

            var entity = CreateLongIdentityEntity("LongIdentity_Single", 3);
            Assert.True(dao.Insert(entity));
            Assert.True(entity.Id > 0);
            Assert.True(await viewDao.ExistsKey(entity.Id).GetResultAsync(TestContext.Current.CancellationToken));

            entity.Name = "LongIdentity_Single_Updated";
            entity.Quantity = 4;
            Assert.True(dao.Update(entity));

            var updated = await viewDao.GetObject(entity.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("LongIdentity_Single_Updated", updated.Name);
            Assert.Equal(4, updated.Quantity);

            var batchA = CreateLongIdentityEntity("LongIdentity_Batch_A", 30);
            var batchB = CreateLongIdentityEntity("LongIdentity_Batch_B", 40);
            dao.BatchInsert([batchA, batchB]);
            Assert.All([batchA, batchB], item => Assert.True(item.Id > 0));

            batchA.Quantity = 31;
            batchB.Quantity = 41;
            dao.BatchUpdate([batchA, batchB]);

            dao.BatchDelete([batchA]);
            dao.BatchDeleteByKeys(new object[] { batchB.Id, entity.Id });

            Assert.False(await viewDao.ExistsKey(batchA.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(batchB.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(entity.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ObjectDAO_CompositeKeyCrudAndBatch_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<ObjectDAO<TestCompositeKeyEntity>>();
            var viewDao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestCompositeKeyEntity>>();

            var entity = CreateCompositeKeyEntity(100, "A", "CompositeKey_Single", 5);
            Assert.True(dao.Insert(entity));
            Assert.True(await viewDao.ExistsKey(entity.Code, entity.Id).GetResultAsync(TestContext.Current.CancellationToken));

            entity.Name = "CompositeKey_Single_Updated";
            entity.Quantity = 6;
            Assert.True(dao.Update(entity));

            var updated = await viewDao.GetObject(entity.Code, entity.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("CompositeKey_Single_Updated", updated.Name);
            Assert.Equal(6, updated.Quantity);

            var batchA = CreateCompositeKeyEntity(100, "B", "CompositeKey_Batch_A", 50);
            var batchB = CreateCompositeKeyEntity(100, "C", "CompositeKey_Batch_B", 60);
            dao.BatchInsert([batchA, batchB]);

            batchA.Quantity = 51;
            batchB.Quantity = 61;
            dao.BatchUpdate([batchA, batchB]);

            dao.BatchDelete([batchA]);
            dao.BatchDeleteByKeys(new[] { new object[] { batchB.Code, batchB.Id }, new object[] { entity.Code, entity.Id } });

            Assert.False(await viewDao.ExistsKey(batchA.Code, batchA.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(batchB.Code, batchB.Id).GetResultAsync(TestContext.Current.CancellationToken));
            Assert.False(await viewDao.ExistsKey(entity.Code, entity.Id).GetResultAsync(TestContext.Current.CancellationToken));
        }

        private static TestUser CreateUser(string name, int age)
        {
            return new TestUser
            {
                Name = name,
                Age = age,
                CreateTime = DateTime.Now
            };
        }

        private static TestShortIdentityEntity CreateShortIdentityEntity(string name, int quantity)
        {
            return new TestShortIdentityEntity
            {
                Name = name,
                Quantity = quantity
            };
        }

        private static TestLongIdentityEntity CreateLongIdentityEntity(string name, int quantity)
        {
            return new TestLongIdentityEntity
            {
                Name = name,
                Quantity = quantity
            };
        }

        private static TestCompositeKeyEntity CreateCompositeKeyEntity(int id, string code, string name, int quantity)
        {
            return new TestCompositeKeyEntity
            {
                Id = id,
                Code = code,
                Name = name,
                Quantity = quantity
            };
        }
    }
}
