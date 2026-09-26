using System.Security.Cryptography;

namespace MarketLink.Services
{
    // Creates random passwords, e.g. "Kp7@x2Qm9d"
    public static class PasswordGenerator
    {
        // Letters and digits that are easy to confuse (0/O, 1/l/I) are left out
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Symbols = "@#$%";

        public static string Generate(int length = 10)
        {
            string all = Upper + Lower + Digits + Symbols;

            // At least one of each kind
            var chars = new List<char> { Pick(Upper), Pick(Lower), Pick(Digits), Pick(Symbols) };

            while (chars.Count < length)
            {
                chars.Add(Pick(all));
            }

            // Shuffle so the first 4 characters are not always upper, lower, digit, symbol
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }

        // RandomNumberGenerator is safe for passwords; the normal Random class is not
        private static char Pick(string source)
        {
            return source[RandomNumberGenerator.GetInt32(source.Length)];
        }
    }
}
