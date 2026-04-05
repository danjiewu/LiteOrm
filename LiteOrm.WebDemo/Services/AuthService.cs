using LiteOrm.Common;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using System.Security.Cryptography;

namespace LiteOrm.WebDemo.Services;

[AutoRegister(Lifetime.Scoped)]
public class AuthService : IAuthService
{
    private readonly IDemoUserService _userService;
    private readonly IDemoAuthSessionService _sessionService;
    private readonly PasswordService _passwordService;

    public AuthService(IDemoUserService userService, IDemoAuthSessionService sessionService, PasswordService passwordService)
    {
        _userService = userService;
        _sessionService = sessionService;
        _passwordService = passwordService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByUserNameAsync(request.UserName, cancellationToken);

        if (user is null || !_passwordService.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return null;
        }

        var session = new DemoAuthSession
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            UserId = user.Id,
            CreatedTime = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(12)
        };

        await _sessionService.InsertAsync(session, cancellationToken);

        return new LoginResponse(session.Token, session.ExpiresAt, ToUserDto(user));
    }

    public async Task<AuthSessionUser?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var session = await _sessionService.SearchOneAsync(
            (Expr.Prop(nameof(DemoAuthSession.Token)) == token)
            & Expr.Prop(nameof(DemoAuthSession.RevokedTime)).IsNull()
            & (Expr.Prop(nameof(DemoAuthSession.ExpiresAt)) >= now),
            cancellationToken: cancellationToken);

        if (session is null)
        {
            return null;
        }

        var user = await _userService.GetProfileAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new AuthSessionUser(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Role,
            user.DepartmentName,
            session.Token,
            session.ExpiresAt);
    }

    public async Task<bool> LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        var session = await _sessionService.SearchOneAsync(
            (Expr.Prop(nameof(DemoAuthSession.Token)) == token)
            & Expr.Prop(nameof(DemoAuthSession.RevokedTime)).IsNull(),
            cancellationToken: cancellationToken);

        if (session is null)
        {
            return false;
        }

        session.RevokedTime = DateTime.UtcNow;
        return await _sessionService.UpdateAsync(session, cancellationToken);
    }

    private static AuthUserDto ToUserDto(DemoUserView user) =>
        new(user.Id, user.UserName, user.DisplayName, user.Role, user.DepartmentName);
}

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthSessionUser?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string token, CancellationToken cancellationToken = default);
}
