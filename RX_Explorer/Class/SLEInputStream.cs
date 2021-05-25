using NetworkAccess;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel;

namespace RX_Explorer.Class
{
    public sealed class SLEInputStream : Stream
    {
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public int KeySize { get; private set; }

        public string FileName { get; private set; }

        private Stream BaseFileStream;
        private readonly string Key;
        private byte[] KeyArray;
        private bool IsResourceInit;
        private CryptoStream TransformStream;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!IsResourceInit)
            {
                ReadHeader();
                CreateAesDecryptor();
                VerifyPassword();

                IsResourceInit = true;
            }

            return TransformStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private void CreateAesDecryptor()
        {
            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                KeySize = KeySize,
                Key = KeyArray,
                IV = Encoding.UTF8.GetBytes(SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
            })
            {
                TransformStream = new CryptoStream(BaseFileStream, AES.CreateDecryptor(), CryptoStreamMode.Read);
            }
        }

        private void VerifyPassword()
        {
            byte[] PasswordConfirm = new byte[16];

            TransformStream.Read(PasswordConfirm, 0, PasswordConfirm.Length);

            if (Encoding.UTF8.GetString(PasswordConfirm) != "PASSWORD_CORRECT")
            {
                throw new PasswordErrorException("Password is not correct");
            }
        }

        private void ReadHeader()
        {
            StringBuilder Builder = new StringBuilder();

            using (StreamReader Reader = new StreamReader(BaseFileStream, Encoding.UTF8, true, 256, true))
            {
                for (int Count = 0; Reader.Peek() >= 0; Count++)
                {
                    if (Count > 256)
                    {
                        throw new FileDamagedException("File damaged, could not be decrypted");
                    }

                    char NextChar = (char)Reader.Read();

                    if (Builder.Length > 0 && NextChar == '$')
                    {
                        Builder.Append(NextChar);
                        break;
                    }
                    else
                    {
                        Builder.Append(NextChar);
                    }
                }
            }

            string RawInfoData = Builder.ToString();

            if (string.IsNullOrWhiteSpace(RawInfoData))
            {
                throw new FileDamagedException("File damaged, could not be decrypted");
            }
            else
            {
                BaseFileStream.Seek(Encoding.UTF8.GetBytes(RawInfoData).Length, SeekOrigin.Begin);

                if (RawInfoData.Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string InfoData)
                {
                    string[] InfoGroup = InfoData.Split('|');

                    if (InfoGroup.Length == 2)
                    {
                        KeySize = Convert.ToInt32(InfoGroup[0]);
                        FileName = InfoGroup[1];

                        if ((KeySize != 128 && KeySize != 256) || string.IsNullOrWhiteSpace(FileName))
                        {
                            throw new FileDamagedException("File damaged, could not be decrypted");
                        }
                        else
                        {
                            int KeyLengthNeed = KeySize / 8;

                            KeyArray = Key.Length > KeyLengthNeed ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed)) : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));
                        }
                    }
                    else
                    {
                        throw new FileDamagedException("File damaged, could not be decrypted");
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            TransformStream?.Dispose();
            BaseFileStream?.Dispose();
            TransformStream = null;
            BaseFileStream = null;
        }

        public SLEInputStream(Stream BaseFileStream, string Key)
        {
            if (BaseFileStream == null)
            {
                throw new ArgumentNullException(nameof(BaseFileStream), "Argument could not be null");
            }

            if (!BaseFileStream.CanRead)
            {
                throw new ArgumentException("BaseStream must be writable", nameof(BaseFileStream));
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            this.Key = Key;
            this.BaseFileStream = BaseFileStream;
        }
    }
}
