using LiteOrm.WebDemo.Contracts;

namespace LiteOrm.WebDemo.Infrastructure;

public static class HttpContextUserExtensions
{
    private const string CurrentUserKey = "__DemoCurrentUser";

    public static void SetCurrentUser(this HttpContext context, AuthSessionUser user) =>
        context.Items[CurrentUserKey] = user;

    public static AuthSessionUser GetCurrentDemoUser(this HttpContext context) =>
        context.Items.TryGetValue(CurrentUserKey, out var value) && value is AuthSessionUser user
            ? user
            : throw new InvalidOperationException("Current user is not available.");
}
