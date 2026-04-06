using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Infrastructure;

public class DemoAuthFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var authService = httpContext.RequestServices.GetRequiredService<IAuthService>();
        var authorization = httpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        var token = authorization["Bearer ".Length..].Trim();
        var user = await authService.GetUserByTokenAsync(token, httpContext.RequestAborted);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        httpContext.SetCurrentUser(user);
        return await next(context);
    }
}
