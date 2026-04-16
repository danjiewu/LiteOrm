using LiteOrm.WebDemo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LiteOrm.WebDemo.Infrastructure;

public class DemoControllerAuthFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var authService = httpContext.RequestServices.GetRequiredService<IAuthService>();
        var authorization = httpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var token = authorization["Bearer ".Length..].Trim();
        var user = await authService.GetUserByTokenAsync(token, httpContext.RequestAborted);
        if (user is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        httpContext.SetCurrentUser(user);
        await next();
    }
}
