using System.Security.Cryptography;
using System.Text;

namespace HelpAsk.Utils;

public static class PasswordHelper
{
    public static byte[] HashPassword(string password)
    {
        using var sha512 = SHA512.Create();
        return sha512.ComputeHash(Encoding.Unicode.GetBytes(password));
    }

    public static bool VerifyPassword(string password, byte[] storedHash)
    {
        var computedHash = HashPassword(password);
        return computedHash.SequenceEqual(storedHash);
    }
}
