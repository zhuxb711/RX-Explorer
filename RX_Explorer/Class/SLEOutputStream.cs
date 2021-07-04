using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel;

namespace RX_Explorer.Class
{
    public sealed class SLEOutputStream : Stream
    {
        public override bool CanRead
        {
            get
            {
                return false;
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
                return true;
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

        public int KeySize { get; }

        public string Key { get; }

        public SLEVersion Version
        {
            get
            {
                string VersionString = string.Format("{0}{1}{2}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build);

                if (ushort.TryParse(VersionString, out ushort Version))
                {
                    if (Version > 655)
                    {
                        return SLEVersion.Version_1_1_0;
                    }
                    else
                    {
                        return SLEVersion.Version_1_0_0;
                    }
                }
                else
                {
                    return SLEVersion.Version_1_0_0;
                }
            }
        }

        private Stream BaseFileStream;
        private CryptoStream TransformStream;
        private readonly string FileName;
        private bool IsResourceInit;

        public override void Flush()
        {
            BaseFileStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            if (!IsResourceInit)
            {
                CreateAesEncryptor();
                WriteHeader();

                IsResourceInit = true;
            }

            TransformStream.Write(buffer, offset, count);
        }

        private void CreateAesEncryptor()
        {
            int KeyLengthNeed = KeySize / 8;

            byte[] KeyArray;

            if (Key.Length > KeyLengthNeed)
            {
                KeyArray = Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed));
            }
            else if (Key.Length < KeyLengthNeed)
            {
                KeyArray = Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));
            }
            else
            {
                KeyArray = Encoding.UTF8.GetBytes(Key);
            }

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                KeySize = KeySize,
                Mode = CipherMode.CBC,
                Padding = Version > SLEVersion.Version_1_0_0 ? PaddingMode.PKCS7 : PaddingMode.Zeros,
                Key = KeyArray,
                IV = Encoding.UTF8.GetBytes("HqVQ2YgUnUlRNp5Z")
            })
            {
                TransformStream = new CryptoStream(BaseFileStream, AES.CreateEncryptor(), CryptoStreamMode.Write);
            }
        }

        private void WriteHeader()
        {
            byte[] ExtraInfo = Encoding.UTF8.GetBytes($"${KeySize}|{FileName.Replace('$', '_')}|{(int)Version}$");
            BaseFileStream.Write(ExtraInfo, 0, ExtraInfo.Length);

            byte[] PasswordConfirm = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
            TransformStream.Write(PasswordConfirm, 0, PasswordConfirm.Length);
        }

        protected override void Dispose(bool disposing)
        {
            TransformStream?.Dispose();
            BaseFileStream?.Dispose();
            TransformStream = null;
            BaseFileStream = null;
        }

        public SLEOutputStream(Stream BaseFileStream, string FileName, string Key, int KeySize)
        {
            if (BaseFileStream == null)
            {
                throw new ArgumentNullException(nameof(BaseFileStream), "Argument could not be null");
            }

            if (!BaseFileStream.CanWrite)
            {
                throw new ArgumentException("BaseStream must be writable", nameof(BaseFileStream));
            }

            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException("FilePath could not be empty", nameof(FileName));
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidDataException("KeySize could only be set with 128 or 256");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            this.BaseFileStream = BaseFileStream;
            this.FileName = FileName;
            this.Key = Key;
            this.KeySize = KeySize;
        }
    }
}
