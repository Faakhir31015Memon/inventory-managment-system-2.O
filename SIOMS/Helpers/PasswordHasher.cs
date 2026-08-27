using System.Security.Cryptography;

namespace SIOMS.Helpers
{
    // ✅ ADDED: self-contained PBKDF2 password hashing (no extra NuGet package
    // required). Format stored in DB: "{iterations}.{saltBase64}.{hashBase64}".
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 100_000;

        public static string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string storedHash)
        {
            var parts = storedHash.Split('.');
            if (parts.Length != 3) return false;

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var key = Convert.FromBase64String(parts[2]);

            var testKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, key.Length);
            return CryptographicOperations.FixedTimeEquals(key, testKey);
        }
    }
}
