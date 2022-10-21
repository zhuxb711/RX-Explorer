using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary
{
    public static class Extention
    {
        public static bool CheckIfValidPtr(this IntPtr Ptr)
        {
            return Ptr != IntPtr.Zero && Ptr.ToInt64() != -1;
        }

        public static async Task<string> EncryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            Encoding Encoding = new UTF8Encoding(false);

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                KeySize = 128,
                Key = Encoding.GetBytes(Key.Length > 16 ? Key.Substring(0, 16) : Key.PadRight(16, '0')),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = Encoding.GetBytes("KUsaWlEy2XN5b6y8")
            })
            {
                using (MemoryStream EncryptStream = new MemoryStream())
                using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter Writer = new StreamWriter(TransformStream, Encoding, 512, true))
                    {
                        await Writer.WriteAsync(OriginText);
                    }

                    TransformStream.FlushFinalBlock();

                    return Convert.ToBase64String(EncryptStream.ToArray());
                }
            }
        }

        public static async Task<string> DecryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            Encoding Encoding = new UTF8Encoding(false);

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                KeySize = 128,
                Key = Encoding.GetBytes(Key.Length > 16 ? Key.Substring(0, 16) : Key.PadRight(16, '0')),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = Encoding.GetBytes("KUsaWlEy2XN5b6y8")
            })
            {
                using (MemoryStream EncryptStream = new MemoryStream(Convert.FromBase64String(OriginText)))
                using (ICryptoTransform Decryptor = AES.CreateDecryptor())
                using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Decryptor, CryptoStreamMode.Read))
                using (StreamReader Reader = new StreamReader(TransformStream, Encoding, true, 512, true))
                {
                    return await Reader.ReadToEndAsync();
                }
            }
        }
    }
}
