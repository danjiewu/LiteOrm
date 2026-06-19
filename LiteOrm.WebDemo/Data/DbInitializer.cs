using LiteOrm.WebDemo.Models;
using LiteOrm.Service;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var departmentService = services.GetRequiredService<IDemoDepartmentService>();
        var userService = services.GetRequiredService<IDemoUserService>();
        var orderService = services.GetRequiredService<IDemoOrderService>();

        var departmentCount = await departmentService.CountAsync();
        var userCount = await userService.CountAsync();
        var orderCount = await orderService.CountAsync();

        if (departmentCount > 0 && userCount > 0 && orderCount > 0)
        {
            return;
        }

        if (departmentCount == 0)
        {
            var departments = new[]
            {
                new DemoDepartment { Id = 1, Name = "Sales", Code = "SALES" },
                new DemoDepartment { Id = 2, Name = "Operations", Code = "OPS" },
                new DemoDepartment { Id = 3, Name = "Key Accounts", Code = "KA" }
            };
            await departmentService.BatchInsertAsync(departments);
        }

        if (userCount == 0)
        {
            var users = new[]
            {
                new DemoUser { Id = 1, UserName = "admin", DisplayName = "Demo Admin", Role = "Admin", DepartmentId = 2, PasswordHash = "n/a", PasswordSalt = "n/a", CreatedTime = DateTime.UtcNow.AddDays(-30) },
                new DemoUser { Id = 2, UserName = "alice", DisplayName = "Alice Chen", Role = "Sales", DepartmentId = 1, PasswordHash = "n/a", PasswordSalt = "n/a", CreatedTime = DateTime.UtcNow.AddDays(-28) },
                new DemoUser { Id = 3, UserName = "bob", DisplayName = "Bob Wang", Role = "Sales", DepartmentId = 1, PasswordHash = "n/a", PasswordSalt = "n/a", CreatedTime = DateTime.UtcNow.AddDays(-26) },
                new DemoUser { Id = 4, UserName = "cathy", DisplayName = "Cathy Liu", Role = "Operations", DepartmentId = 2, PasswordHash = "n/a", PasswordSalt = "n/a", CreatedTime = DateTime.UtcNow.AddDays(-24) },
                new DemoUser { Id = 5, UserName = "david", DisplayName = "David Zhao", Role = "AccountManager", DepartmentId = 3, PasswordHash = "n/a", PasswordSalt = "n/a", CreatedTime = DateTime.UtcNow.AddDays(-22) }
            };
            await userService.BatchInsertAsync(users);
        }

        if (orderCount == 0)
        {
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
                        CreatedByUserId = (index % 4) switch
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
}
