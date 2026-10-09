using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace IfcHopper.Core.Model
{
    /// <summary>IFC GlobalId helpers: the 22-character IFC encoding of a GUID, and stable ids derived from a seed.</summary>
    public static class GlobalIds
    {
        private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        // Namespace for name-based (UUID v5) ids, so seeds never collide with other tools' derived ids.
        private static readonly Guid Namespace = new Guid("8a4f2c6e-1b3d-4e5f-9a7c-0d2e4f6a8b1c");

        public static string New() => FromGuid(Guid.NewGuid());

        /// <summary>A GlobalId that is always the same for the same seed (UUID v5 of the seed).</summary>
        public static string FromSeed(string seed)
        {
            var name = Encoding.UTF8.GetBytes(seed ?? string.Empty);
            var bytes = ToBigEndian(Namespace).Concat(name).ToArray();
            byte[] hash;
            using (var sha1 = SHA1.Create()) hash = sha1.ComputeHash(bytes);

            var uuid = new byte[16];
            Array.Copy(hash, uuid, 16);
            uuid[6] = (byte)((uuid[6] & 0x0F) | 0x50);
            uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);
            return Encode(uuid);
        }

        public static string FromGuid(Guid guid) => Encode(ToBigEndian(guid));

        /// <summary>Accepts a 22-character IFC GlobalId or a standard GUID string and returns the IFC form.</summary>
        public static bool TryParse(string text, out string globalId)
        {
            globalId = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();

            if (IsValid(text))
            {
                globalId = text;
                return true;
            }
            if (Guid.TryParse(text, out var guid))
            {
                globalId = FromGuid(guid);
                return true;
            }
            return false;
        }

        /// <summary>22 characters of the IFC alphabet; the first character carries only 2 bits (0-3).</summary>
        public static bool IsValid(string globalId) =>
            globalId != null && globalId.Length == 22 && globalId[0] >= '0' && globalId[0] <= '3' && globalId.All(c => Alphabet.IndexOf(c) >= 0);

        /// <summary>IFC encoding: the first byte as 2 characters, then 5 groups of 3 bytes as 4 characters each.</summary>
        private static string Encode(byte[] bytes)
        {
            var sb = new StringBuilder(22);
            Append(sb, bytes[0], 2);
            for (int i = 1; i < 16; i += 3)
                Append(sb, (bytes[i] << 16) | (bytes[i + 1] << 8) | bytes[i + 2], 4);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, int value, int digits)
        {
            var chars = new char[digits];
            for (int i = digits - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[value % 64];
                value /= 64;
            }
            sb.Append(chars);
        }

        /// <summary>GUID bytes in textual (big-endian) order; Guid.ToByteArray swaps the first three groups.</summary>
        private static byte[] ToBigEndian(Guid guid)
        {
            var hex = guid.ToString("N");
            var bytes = new byte[16];
            for (int i = 0; i < 16; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
