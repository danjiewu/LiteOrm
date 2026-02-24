using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ServiceTests : TestBase
    {
        public ServiceTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void DataSource_Configuration_ShouldBeCorrect()
        {
            var dataSourceProvider = ServiceProvider.GetRequiredService<IDataSourceProvider>();
            var config = dataSourceProvider.GetDataSource(null);

            Assert.NotNull(config);
            Assert.True(config.SyncTable);
        }

        [Fact]
        public async Task EntityService_InsertAndGetObject_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser
            {
                Name = "Test User",
                Age = 25,
                CreateTime = DateTime.Now
            };

            // Act
            bool inserted = await service.InsertAsync(user);
            var retrievedUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(inserted);
            Assert.NotNull(retrievedUser);
            Assert.Equal(user.Name, retrievedUser.Name);
            Assert.Equal(user.Age, retrievedUser.Age);
        }

        [Fact]
        public async Task EntityViewService_Search_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "User 2", Age = 30, CreateTime = DateTime.Now });

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // Act
            var users = await viewService.SearchAsync(u => u.Age > 25);

            // Assert
            Assert.Single(users);
            Assert.Equal("User 2", users[0].Name);
        }

        [Fact]
        public async Task EntityService_Update_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();
            var user = new TestUser { Name = "Original", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            user.Name = "Updated";
            bool updated = await service.UpdateAsync(user);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(updated);
            Assert.Equal("Updated", retrieved.Name);
        }

        [Fact]
        public async Task EntityService_Delete_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "To Delete", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            bool deleted = await service.DeleteAsync(user);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(deleted);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task EntityService_BatchInsert_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var users = Enumerable.Range(1, 10).Select(i => new TestUser
            {
                Name = $"Batch User {i}",
                Age = 20 + i,
                CreateTime = DateTime.Now
            }).ToList();

            // Act
            await service.BatchInsertAsync(users);
            var retrievedUsers = await viewService.SearchAsync(u => u.Name.StartsWith("Batch User"));

            // Assert
            Assert.Equal(10, retrievedUsers.Count);
            foreach (var user in retrievedUsers)
            {
                Assert.Contains("Batch User", user.Name);
            }
        }

        [Fact]
        public async Task EntityViewService_JoinQuery_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "IT Department" };
            await deptService.InsertAsync(dept);

            var user = new TestUser
            {
                Name = "John Joined",
                Age = 30,
                CreateTime = DateTime.Now,
                DeptId = dept.Id
            };
            await userService.InsertAsync(user);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.NotNull(viewUser);
            Assert.Equal("John Joined", viewUser.Name);
            Assert.Equal("IT Department", viewUser.DeptName);
        }

        [Fact]
        public async Task Expr_ComplexQueries_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "Alice", Age = 25, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "Bob", Age = 30, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "Charlie", Age = 35, CreateTime = DateTime.Now });
            await service.InsertAsync(new TestUser { Name = "David", Age = 40, CreateTime = DateTime.Now });

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            // Act & Assert

            // 1. Expr.In
            var inList = await viewService.SearchAsync(Expr.Prop("Name").In("Alice", "Bob"));
            Assert.Equal(2, inList.Count);

            // 2. Expr.Between
            var betweenList = await viewService.SearchAsync(Expr.Prop("Age").Between(30, 35));
            Assert.Equal(2, betweenList.Count);

            // 3. Expr.Like
            var likeList = await viewService.SearchAsync(Expr.Prop("Name").Like("Cha%"));
            Assert.Single(likeList);
            Assert.Equal("Charlie", likeList[0].Name);

            // 4. Combined And/Or
            var combinedList = await viewService.SearchAsync(
                (Expr.Prop("Age") < 30) | (Expr.Prop("Age") >= 40)
            );
            Assert.Equal(2, combinedList.Count);

            // 5. Lambda complex
            var lambdaList = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Age > 30 && u.Name!.Contains("i")));
            // Charlie(35), David(40) -> both contain 'i'
            Assert.Equal(2, lambdaList.Count);
        }

        [Fact]
        public async Task Hierarchical_Query_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var root = new TestDepartment { Name = "Headquarters" };
            await deptService.InsertAsync(root);

            var sub1 = new TestDepartment { Name = "HR", ParentId = root.Id };
            var sub2 = new TestDepartment { Name = "IT", ParentId = root.Id };
            await deptService.InsertAsync(sub1);
            await deptService.InsertAsync(sub2);

            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartment>>();

            // Act
            var subDepts = await viewService.SearchAsync(Expr.Exp<TestDepartment>(d => d.ParentId == root.Id));

            // Assert
            Assert.Equal(2, subDepts.Count);
            Assert.Contains(subDepts, d => d.Name == "HR");
            Assert.Contains(subDepts, d => d.Name == "IT");
        }

        [Fact]
        public async Task EntityService_BatchOperations_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var users = new List<TestUser>
            {
                new TestUser { Name = "Batch 1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Batch 2", Age = 20, CreateTime = DateTime.Now }
            };

            // Act - Batch Insert
            await service.BatchInsertAsync(users);
            var inserted = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.Equal(2, inserted.Count);

            // Act - Batch Update
            foreach (var u in inserted) u.Age += 5;
            await service.BatchUpdateAsync(inserted);
            var updated = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.All(updated, u => Assert.True(u.Age == 15 || u.Age == 25));

            // Act - Batch Delete
            await service.BatchDeleteAsync(updated);
            var deletedCount = await viewService.CountAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Batch")));
            Assert.Equal(0, deletedCount);
        }

        [Fact]
        public async Task EntityService_BatchInsert_WithIdentity_ShouldPopulateIds()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var users = new List<TestUser>();
            for (int i = 1; i <= 5; i++)
            {
                users.Add(new TestUser { Name = $"BatchId {i}", Age = 20 + i, CreateTime = DateTime.Now });
            }

            // Act
            await service.BatchInsertAsync(users);

            // Assert
            Assert.All(users, u => Assert.True(u.Id > 0));
            // Verify sequential IDs
            for (int i = 1; i < users.Count; i++)
            {
                Assert.Equal(users[i - 1].Id + 1, users[i].Id);
            }

            // Cleanup
            await service.BatchDeleteAsync(users);
        }

        [Fact]
        public async Task EntityService_BatchUpdateOrInsert_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // 1. Initial insert
            var users = new List<TestUser>
            {
                new TestUser { Name = "Upsert A", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Upsert B", Age = 20, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // 2. Prepare mixed batch: one update, one new
            var existingUser = users[0];
            existingUser.Age = 15; // Changed

            var newUser = new TestUser { Name = "Upsert C", Age = 30, CreateTime = DateTime.Now };

            var batch = new List<TestUser> { existingUser, newUser };

            // Act
            await service.BatchUpdateOrInsertAsync(batch);

            // Assert
            var allUsers = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name!.StartsWith("Upsert")));
            Assert.Equal(3, allUsers.Count);

            var retrievedA = allUsers.FirstOrDefault(u => u.Name == "Upsert A");
            Assert.NotNull(retrievedA);
            Assert.Equal(15, retrievedA.Age);

            var retrievedC = allUsers.FirstOrDefault(u => u.Name == "Upsert C");
            Assert.NotNull(retrievedC);
            Assert.True(retrievedC.Id > 0);

            // Cleanup
            await service.BatchDeleteAsync(allUsers);
        }

        [Fact]
        public async Task EntityService_UpdateValues_ShouldWork()

        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValue", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValuesAsync(updateValues, Expr.Exp<TestUser>(u => u.Name == "UpdateValue"));
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task EntityViewService_SearchOne_Exists_Count_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await service.InsertAsync(new TestUser { Name = "Unique", Age = 50, CreateTime = DateTime.Now });

            // Act
            var one = await viewService.SearchOneAsync(Expr.Exp<TestUser>(u => u.Name == "Unique"));
            bool exists = await viewService.ExistsAsync(Expr.Exp<TestUser>(u => u.Name == "Unique"));
            int count = await viewService.CountAsync(Expr.Exp<TestUser>(u => u.Age >= 50));

            // Assert
            Assert.NotNull(one);
            Assert.True(exists);
            Assert.True(count >= 1);
        }

        [Fact]
        public async Task EntityViewService_SearchWithOrder_Section_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await service.BatchInsertAsync(new[]
            {
                new TestUser { Name = "Order 1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "Order 2", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "Order 3", Age = 30, CreateTime = DateTime.Now }
            });

            // Act - Order
            var ordered = await viewService.SearchAsync(
                Expr.Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false))
            );

            // Act - Section
            var section = await viewService.SearchAsync(
                Expr.Where<TestUser>(u => u.Name!.StartsWith("Order")).OrderBy(("Age", false)).Section(0, 2)
            );

            // Assert
            Assert.Equal("Order 3", ordered[0].Name);
            Assert.Equal(2, section.Count);
            Assert.Equal("Order 3", section[0].Name);
        }

        [Fact]
        public async Task EntityViewService_TestDepartmentView_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestDepartmentView>>();

            var root = new TestDepartment { Name = "Root" };
            await service.InsertAsync(root);

            var child = new TestDepartment { Name = "Child", ParentId = root.Id };
            await service.InsertAsync(child);

            // Act
            var view = await viewService.SearchOneAsync(Expr.Exp<TestDepartmentView>(d => d.Id == child.Id));

            // Assert
            Assert.NotNull(view);
            Assert.Equal("Root", view.ParentName);
        }

        [Fact]
        public async Task EntityService_UpdateOrInsert_Batch_ForEach_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var user = new TestUser { Name = "Upsert Me", Age = 30, CreateTime = DateTime.Now };

            // Act - UpdateOrInsert (Insert)
            bool inserted = await service.UpdateOrInsertAsync(user);
            Assert.True(inserted);
            Assert.True(user.Id > 0);

            // Act - UpdateOrInsert (Update)
            user.Age = 35;
            bool updated = await service.UpdateOrInsertAsync(user);
            Assert.True(updated);
            var retrieved = await viewService.GetObjectAsync(user.Id);
            Assert.Equal(35, retrieved?.Age);

            // Act - Batch (Mixed)
            var newUser = new TestUser { Name = "Mixed 1", Age = 10, CreateTime = DateTime.Now };
            var ops = new List<EntityOperation<TestUser>>
            {
                new EntityOperation<TestUser> { Entity = newUser, Operation = OpDef.Insert },
                new EntityOperation<TestUser> { Entity = user, Operation = OpDef.Delete }
            };
            await service.BatchAsync(ops);

            var mixedRetrieved = await viewService.SearchOneAsync(Expr.Exp<TestUser>(u => u.Name == "Mixed 1"));
            var deletedRetrieved = await viewService.GetObjectAsync(user.Id);

            Assert.NotNull(mixedRetrieved);
            Assert.Null(deletedRetrieved);

            // Act - ForEachAsync
            int forEachCount = 0;
            await viewService.ForEachAsync(Expr.Exp<TestUser>(u => u.Name == "Mixed 1"), async u =>
            {
                forEachCount++;
                await Task.CompletedTask;
            });
            Assert.Equal(1, forEachCount);
        }

        [Fact]
        public async Task ForeignExpr_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept = new TestDepartment { Name = "Foreign Dept" };
            await deptService.InsertAsync(dept);

            await userService.InsertAsync(new TestUser { Name = "User In Dept", DeptId = dept.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User Outside", DeptId = -1, CreateTime = DateTime.Now });

            // Act
            // ʹ �� ForeignExpr ���й�����ѯ (���� EXISTS �Ӳ�ѯ)
            // ����������������Ϊ "Foreign Dept" ���û�
            var users = await viewService.SearchAsync(Expr.Foreign("Dept", Expr.Prop("Name") == "Foreign Dept"));

            // Assert
            Assert.Single(users);
            Assert.Equal("User In Dept", users[0].Name);
        }

        [Fact]
        public async Task ForeignExpr_Combined_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var dept1 = new TestDepartment { Name = "Dept 1" };
            await deptService.InsertAsync(dept1);
            var dept2 = new TestDepartment { Name = "Dept 2" };
            await deptService.InsertAsync(dept2);

            await userService.InsertAsync(new TestUser { Name = "User A", Age = 20, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User B", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now });
            await userService.InsertAsync(new TestUser { Name = "User C", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now });

            // Act
            // ��������Ϊ 30 ������������Ϊ "Dept 1" ���û�
            var users = await viewService.SearchAsync(
                (Expr.Prop("Age") == 30) & Expr.Foreign("Dept", Expr.Prop("Name") == "Dept 1")
            );

            // Assert
            Assert.Single(users);
            Assert.Equal("User B", users[0].Name);
        }

        [Fact]
        public async Task CustomService_ShouldBeResolvable()
        {
            // Arrange
            var customService = ServiceProvider.GetRequiredService<ITestUserService>();
            string uniqueName = "Custom_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var testUser = new TestUser { Name = uniqueName, Age = 100, CreateTime = DateTime.Now };

            // Act
            await customService.InsertAsync(testUser);
            var latest = await customService.GetLatestUserAsync();

            // Assert
            Assert.NotNull(latest);
            // If GetLatestUserAsync returns our user, great. 
            // If it returns another one (only if serial mode fails), we specifically check if OUR user was inserted correctly.
            var retrieved = await customService.SearchOneAsync(u => u.Name == uniqueName);
            Assert.NotNull(retrieved);
            Assert.Equal(100, retrieved.Age);

            // To ensure GetLatestUserAsync logic is also correct and returns at least ONE user
            Assert.NotNull(latest);
            Assert.True(latest.Id > 0);
            Assert.True(latest.Name == uniqueName);
        }

        [Fact]
        public async Task EntityViewService_MultiLevelJoin_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            var rootDept = new TestDepartment { Name = "Root Dept" };
            await deptService.InsertAsync(rootDept);

            var subDept = new TestDepartment { Name = "Sub Dept", ParentId = rootDept.Id };
            await deptService.InsertAsync(subDept);

            var user = new TestUser
            {
                Name = "MultiJoin User",
                Age = 25,
                CreateTime = DateTime.Now,
                DeptId = subDept.Id
            };
            await userService.InsertAsync(user);

            // Act
            var viewUser = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.NotNull(viewUser);
            Assert.Equal("MultiJoin User", viewUser.Name);
            Assert.Equal("Sub Dept", viewUser.DeptName);
            Assert.Equal("Root Dept", viewUser.ParentDeptName);

            // ���ݹ������ֶβ�ѯ (Act & Assert)

            // 1. ����һ���������ֶβ�ѯ
            var usersByDept = await viewService.SearchAsync(u => u.DeptName == "Sub Dept");
            Assert.Contains(usersByDept, u => u.Id == user.Id);

            // 2. ���ݶ����������ֶβ�ѯ (������)
            var usersByParentDept = await viewService.SearchAsync(u => u.ParentDeptName == "Root Dept");
            Assert.Contains(usersByParentDept, u => u.Id == user.Id);

            // 3. ��ϲ�ѯ
            var combinedUsers = await viewService.SearchAsync(u => u.DeptName == "Sub Dept" && u.ParentDeptName == "Root Dept");
            Assert.Single(combinedUsers);
            Assert.Equal(user.Id, combinedUsers[0].Id);

            // 4. Count ��֤
            int count = await viewService.CountAsync(u => u.ParentDeptName == "Root Dept");
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task EntityViewService_MultiTableJoin_SortingPagination_WithForeignColumn_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUserView>>();

            // 创建测试部门
            var dept1 = new TestDepartment { Name = "A Department" };
            var dept2 = new TestDepartment { Name = "B Department" };
            var dept3 = new TestDepartment { Name = "C Department" };
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);
            await deptService.InsertAsync(dept3);

            // 创建测试用户，分布在不同部门
            await userService.InsertAsync(new TestUser { Name = "User 1", Age = 20, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User 2", Age = 25, CreateTime = DateTime.Now, DeptId = dept2.Id });
            await userService.InsertAsync(new TestUser { Name = "User 3", Age = 30, CreateTime = DateTime.Now, DeptId = dept3.Id });
            await userService.InsertAsync(new TestUser { Name = "User 4", Age = 35, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User 5", Age = 40, CreateTime = DateTime.Now, DeptId = dept2.Id });

            // Act 1: 使用 ForeignColumn (DeptName) 作为查询条件和排序条件，同时分页
            var expr1 = Expr.Where<TestUserView>(u => u.DeptName != null)
                .OrderBy((nameof(TestUserView.DeptName), true))  // 按部门名称升序
                .OrderBy((nameof(TestUser.Age), false))          // 再按年龄降序
                .Section(0, 3);                                  // 分页，取前3条
            var users1 = await viewService.SearchAsync(expr1);

            // Assert 1
            Assert.Equal(3, users1.Count);
            // 验证排序顺序：A Department 的用户应该在前面，且同一部门内按年龄降序
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 35); // User 4
            Assert.Contains(users1, u => u.DeptName == "A Department" && u.Age == 20); // User 1
            Assert.Contains(users1, u => u.DeptName == "B Department" && u.Age == 40); // User 5

            // Act 2: 使用 ForeignColumn (ParentDeptName) 作为查询条件和排序条件
            // 首先创建有父部门的部门结构
            var parentDept = new TestDepartment { Name = "Parent Dept" };
            await deptService.InsertAsync(parentDept);

            var childDept1 = new TestDepartment { Name = "Child Dept 1", ParentId = parentDept.Id };
            var childDept2 = new TestDepartment { Name = "Child Dept 2", ParentId = parentDept.Id };
            await deptService.InsertAsync(childDept1);
            await deptService.InsertAsync(childDept2);

            // 创建属于子部门的用户
            await userService.InsertAsync(new TestUser { Name = "Child User 1", Age = 22, CreateTime = DateTime.Now, DeptId = childDept1.Id });
            await userService.InsertAsync(new TestUser { Name = "Child User 2", Age = 28, CreateTime = DateTime.Now, DeptId = childDept2.Id });

            // 使用 ParentDeptName 作为查询和排序条件
            var expr2 = Expr.Where<TestUserView>(u => u.ParentDeptName == "Parent Dept")
                .OrderBy(nameof(TestUserView.ParentDeptName))  // 按父部门名称升序
                .OrderBy(nameof(TestUserView.DeptName))        // 再按部门名称升序
                .OrderBy(nameof(TestUser.Age))                 // 再按年龄升序
                .Section(0, 5);                                         // 分页，取前5条
            var users2 = await viewService.SearchAsync(expr2);

            // Assert 2
            Assert.True(users2.Count >= 2); // 至少有2个用户
            // 验证所有结果的 ParentDeptName 都是 "Parent Dept"
            Assert.All(users2, u => Assert.Equal("Parent Dept", u.ParentDeptName));
        }

        [Fact]
        public async Task EntityService_Update_UpdateExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityService<TestUser>>();
            var asyncService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateExprTest", Age = 10, CreateTime = DateTime.Now };
            await asyncService.InsertAsync(user);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Source = Expr.From<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(5)), // 使用运算符重载和Expr.Const，Age = Age + 5
                    ("Name", new FunctionExpr("UPPER", new PropertyExpr("Name"))) // 使用UPPER函数，参数为Name属性
                },
                Where = Expr.Exp<TestUser>(u => u.Name == "UpdateExprTest")
            };
            int affected = service.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(15, retrieved?.Age); // Age + 5 = 15
            Assert.Equal("UPDATEEXPRTEST", retrieved?.Name); // UPPER("UpdateExprTest") = "UPDATEEXPRTEST"
        }

        [Fact]
        public async Task EntityService_UpdateAsync_UpdateExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateExprAsyncTest", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act - 使用FunctionExpr和PropertyExpr增加复杂度
            var updateExpr = new UpdateExpr
            {
                Source = Expr.From<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(10)), // 使用运算符重载和Expr.Const，Age = Age + 10
                    ("Name", new FunctionExpr("CONCAT", new PropertyExpr("Name"), "_Updated")) // 使用CONCAT函数，参数为Name属性和字符串
                },
                Where = Expr.Exp<TestUser>(u => u.Name == "UpdateExprAsyncTest")
            };
            int affected = await service.UpdateAsync(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(20, retrieved?.Age); // Age + 10 = 20
            Assert.Equal("UpdateExprAsyncTest_Updated", retrieved?.Name); // CONCAT("UpdateExprAsyncTest", "_Updated")
        }
    }
}
