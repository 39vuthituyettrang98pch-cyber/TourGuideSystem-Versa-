using System.Security.Cryptography;
using System.Text;

namespace AdminWeb.Services;

public sealed class PasswordService
{
    private const int Iterations = 120_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string? storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        if (storedHash.StartsWith("pbkdf2$", StringComparison.Ordinal))
            return VerifyPbkdf2(password, storedHash, out needsUpgrade);

        needsUpgrade = true;
        var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))
            .ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(legacyHash),
            Encoding.ASCII.GetBytes(storedHash.ToLowerInvariant()));
    }

    private static bool VerifyPbkdf2(string password, string storedHash, out bool needsUpgrade)
    {
        needsUpgrade = false;
        var parts = storedHash.Split('$');
        if (parts.Length != 4 ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);
            needsUpgrade = iterations < Iterations;
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
