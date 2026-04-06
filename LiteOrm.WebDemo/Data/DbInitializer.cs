using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var departmentService = services.GetRequiredService<IDemoDepartmentService>();
        var userService = services.GetRequiredService<IDemoUserService>();
        var orderService = services.GetRequiredService<IDemoOrderService>();
        var sessionService = services.GetRequiredService<IDemoAuthSessionService>();
        var passwordService = services.GetRequiredService<PasswordService>();

        var departmentCount = await departmentService.CountAsync();
        var userCount = await userService.CountAsync();
        var orderCount = await orderService.CountAsync();

        if (departmentCount > 0 && userCount > 0 && orderCount > 0)
        {
            return;
        }

        if (departmentCount > 0)
        {
            var sessions = await sessionService.SearchAsync();
            if (sessions.Count > 0)
            {
                await sessionService.BatchDeleteAsync(sessions);
            }
            return;
        }

        var departments = new[]
        {
            new DemoDepartment { Id = 1, Name = "Sales", Code = "SALES" },
            new DemoDepartment { Id = 2, Name = "Operations", Code = "OPS" },
            new DemoDepartment { Id = 3, Name = "Key Accounts", Code = "KA" }
        };
        await departmentService.BatchInsertAsync(departments);

        DemoUser CreateUser(int id, string userName, string displayName, string role, int departmentId, string password)
        {
            var hash = passwordService.Hash(password);
            return new DemoUser
            {
                Id = id,
                UserName = userName,
                DisplayName = displayName,
                Role = role,
                DepartmentId = departmentId,
                PasswordHash = hash.Hash,
                PasswordSalt = hash.Salt,
                CreatedTime = DateTime.UtcNow.AddDays(-30 + id)
            };
        }

        var users = new[]
        {
            CreateUser(1, "admin", "Demo Admin", "Admin", 2, "admin123"),
            CreateUser(2, "alice", "Alice Chen", "Sales", 1, "demo123"),
            CreateUser(3, "bob", "Bob Wang", "Sales", 1, "demo123"),
            CreateUser(4, "cathy", "Cathy Liu", "Operations", 2, "demo123"),
            CreateUser(5, "david", "David Zhao", "AccountManager", 3, "demo123")
        };
        await userService.BatchInsertAsync(users);

        var now = DateTime.UtcNow;
        var statuses = DemoOrderStatuses.All;
        var customerPool = new[] { "Contoso", "Fabrikam", "Northwind", "Adventure Works", "Woodgrove" };
        var productPool = new[] { "Laptop", "Monitor", "Keyboard", "Dock", "Chair", "Camera" };
        var notes = new[] { "priority", "demo customer", "follow-up", "standard", "expedite", "bulk order" };

        var orders = Enumerable.Range(1, 24)
            .Select(index =>
            {
                var quantity = index % 5 + 1;
                var unitPrice = 80m + index * 15m;
                return new DemoOrder
                {
                    Id = index,
                    OrderNo = $"ORD-2026-{index:000}",
                    CustomerName = customerPool[(index - 1) % customerPool.Length],
                    ProductName = productPool[(index - 1) % productPool.Length],
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = quantity * unitPrice,
                    Status = statuses[(index - 1) % statuses.Length],
                    Note = notes[(index - 1) % notes.Length],
                    CreatedTime = now.AddDays(-index),
                    UpdatedTime = now.AddDays(-index).AddHours(index % 7),
                    CreatedByUserId = index % 4 switch
                    {
                        0 => 2,
                        1 => 3,
                        2 => 4,
                        _ => 5
                    }
                };
            })
            .ToArray();

        await orderService.BatchInsertAsync(orders);
    }
}
