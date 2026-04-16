using LiteOrm;
using LiteOrm.WebDemo.Controllers;
using LiteOrm.WebDemo.Data;
using LiteOrm.WebDemo.Endpoints;
using LiteOrm.WebDemo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.RegisterLiteOrm();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<DemoAuthFilter>();
builder.Services.AddScoped<DemoControllerAuthFilter>();

var dynamicAssembly = DynamicControllerBuilder.BuildDynamicControllers("LiteOrm.WebDemo");
builder.Services
    .AddControllers()
    .AddApplicationPart(dynamicAssembly);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

app.MapDemoEndpoints();
app.MapControllers();

app.Run();
