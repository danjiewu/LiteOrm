using LiteOrm;
using LiteOrm.WebDemo.Data;
using LiteOrm.WebDemo.Endpoints;
using LiteOrm.WebDemo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.RegisterLiteOrm();
builder.Services.AddScoped<DemoAuthFilter>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    using var _ = SessionManager.PushCurrentFactory(() => context.RequestServices.GetRequiredService<SessionManager>());
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

app.MapDemoEndpoints();

app.Run();
