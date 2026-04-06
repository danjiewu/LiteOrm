namespace LiteOrm.WebDemo.Contracts;

public sealed record LoginRequest(string UserName, string Password);

public sealed record AuthUserDto(
    int Id,
    string UserName,
    string DisplayName,
    string Role,
    string? DepartmentName);

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAt,
    AuthUserDto User);

public sealed record AuthSessionUser(
    int Id,
    string UserName,
    string DisplayName,
    string Role,
    string? DepartmentName,
    string Token,
    DateTime ExpiresAt);
