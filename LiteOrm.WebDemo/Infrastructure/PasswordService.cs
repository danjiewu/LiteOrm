using LiteOrm.Common;
using System.Security.Cryptography;

namespace LiteOrm.WebDemo.Infrastructure;

[AutoRegister(Lifetime.Singleton)]
public class PasswordService
{
    public PasswordHashResult Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);

        return new PasswordHashResult(Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(storedHash) ||
            string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        var saltBytes = Convert.FromBase64String(storedSalt);
        var expectedHashBytes = Convert.FromBase64String(storedHash);
        var actualHashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);

        return CryptographicOperations.FixedTimeEquals(expectedHashBytes, actualHashBytes);
    }
}

public sealed record PasswordHashResult(string Hash, string Salt);
