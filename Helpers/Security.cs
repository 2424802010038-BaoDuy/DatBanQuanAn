using System;
using System.Security.Cryptography;

namespace DatBanQuanAn.Helpers
{
    public static class Security
    {
        public static string NewSalt()
        {
            var b = new byte[16];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(b);
            return Convert.ToBase64String(b);
        }
        public static string Hash(string password, string salt)
        {
            using (var k = new Rfc2898DeriveBytes(password, Convert.FromBase64String(salt), 10000))
                return Convert.ToBase64String(k.GetBytes(32));
        }
        public static bool Verify(string password, string salt, string hash) { return Hash(password, salt) == hash; }

        public static string RandomCode(int len)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var b = new byte[len];
            using (var r = RandomNumberGenerator.Create()) r.GetBytes(b);
            var s = new char[len];
            for (int i = 0; i < len; i++) s[i] = chars[b[i] % chars.Length];
            return new string(s);
        }
    }
}
