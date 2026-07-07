namespace NRCAPP.Services;

public static class PasscodeHasher
{
    public static string Hash(string passcode) =>
        BCrypt.Net.BCrypt.HashPassword(passcode, workFactor: 10);

    public static bool Verify(string passcode, string hash) =>
        BCrypt.Net.BCrypt.Verify(passcode, hash);
}
