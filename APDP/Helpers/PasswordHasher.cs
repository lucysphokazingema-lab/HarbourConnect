using System;
using System.Security.Cryptography;

namespace APDP.Helpers
{
    /// <summary>
    /// Salted PBKDF2-SHA256 password hashing.
    /// Stored format: PBKDF2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize   = 16;
        private const int HashSize   = 32;
        private const int Iterations = 210000;
        private const string Prefix  = "PBKDF2";

        // Used when no user matches, so a failed lookup takes as long as a wrong password.
        private static readonly string DummyHash = Hash(Guid.NewGuid().ToString());

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            byte[] hash = Derive(password, salt, Iterations);
            return string.Join("$", Prefix, Iterations,
                Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            {
                Verify("x", DummyHash);
                return false;
            }

            string[] parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return false;

            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations <= 0) return false;

            byte[] salt, expected;
            try
            {
                salt     = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            byte[] actual = Derive(password, salt, iterations, expected.Length);
            return FixedTimeEquals(actual, expected);
        }

        /// <summary>Burns the same CPU time as a real check; call when the user was not found.</summary>
        public static void VerifyDummy(string password)
        {
            Verify(string.IsNullOrEmpty(password) ? "x" : password, DummyHash);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashSize)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
                return pbkdf2.GetBytes(length);
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
