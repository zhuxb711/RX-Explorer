using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SharedLibrary
{
    public static class Extention
    {
        public static bool CheckIfValidPtr(this IntPtr Ptr)
        {
            return Ptr != IntPtr.Zero && Ptr.ToInt64() != -1;
        }

        public static string Encrypt(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                {
                    KeySize = 128,
                    Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7,
                    IV = Encoding.UTF8.GetBytes("KUsaWlEy2XN5b6y8")
                })
                {
                    using (MemoryStream EncryptStream = new MemoryStream())
                    using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                    using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Encryptor, CryptoStreamMode.Write))
                    {
                        byte[] OriginBytes = Encoding.UTF8.GetBytes(OriginText);
                        TransformStream.Write(OriginBytes, 0, OriginBytes.Length);
                        TransformStream.FlushFinalBlock();
                        return Convert.ToBase64String(EncryptStream.ToArray());
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string Decrypt(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                {
                    KeySize = 128,
                    Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.PKCS7,
                    IV = Encoding.UTF8.GetBytes("KUsaWlEy2XN5b6y8")
                })
                {
                    using (MemoryStream DecryptStream = new MemoryStream())
                    using (ICryptoTransform Decryptor = AES.CreateDecryptor())
                    using (CryptoStream TransformStream = new CryptoStream(DecryptStream, Decryptor, CryptoStreamMode.Write))
                    {
                        byte[] EncryptedBytes = Convert.FromBase64String(OriginText);
                        TransformStream.Write(EncryptedBytes, 0, EncryptedBytes.Length);
                        TransformStream.FlushFinalBlock();
                        return Encoding.UTF8.GetString(DecryptStream.ToArray());
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
