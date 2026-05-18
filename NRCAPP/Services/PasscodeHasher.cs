using System.Security.Cryptography;
using System.Text;

namespace NRCAPP.Services;

public static class PasscodeHasher
{
    public static string Hash(string passcode)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(passcode));
        return Convert.ToHexString(bytes);
    }

    public static bool Verify(string passcode, string hash)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(passcode)),
            Encoding.UTF8.GetBytes(hash));
    }
}
