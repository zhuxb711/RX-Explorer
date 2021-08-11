using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace RX_Explorer.Class
{
    public sealed class SLEOutputStream : Stream
    {
        private const int BlockSize = 16;

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
                return Math.Max(BaseFileStream.Length - Header.HeaderLength - BlockSize, 0);
            }
        }

        public override long Position
        {
            get
            {
                if (Header.Version == SLEVersion.Version_1_5_0)
                {
                    return Math.Max(BaseFileStream.Position - Header.HeaderLength - BlockSize, 0);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public SLEHeader Header { get; }

        private readonly ICryptoTransform Transform;
        private readonly Stream BaseFileStream;
        private readonly string Key;

        private readonly CryptoStream TransformStream;
        private readonly byte[] Counter;
        private bool IsDisposed;

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
            try
            {
                switch (Header.Version)
                {
                    case SLEVersion.Version_1_5_0:
                        {
                            long StartPosition = Position + offset;
                            long CurrentBlockIndex = StartPosition / BlockSize;

                            byte[] XorBuffer = new byte[BlockSize];

                            for (int Index = 0; Index < count; Index += BlockSize)
                            {
                                Array.ConstrainedCopy(BitConverter.GetBytes(CurrentBlockIndex++), 0, Counter, BlockSize / 2, 8);

                                Transform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

                                int ValidDataLength = Math.Min(BlockSize, count - Index);

                                for (int Index2 = 0; Index2 < ValidDataLength; Index2++)
                                {
                                    XorBuffer[Index2] = (byte)(XorBuffer[Index2] ^ buffer[Index + Index2]);
                                }

                                BaseFileStream.Write(XorBuffer, offset, ValidDataLength);
                            }

                            break;
                        }
                    case SLEVersion.Version_1_1_0:
                    case SLEVersion.Version_1_0_0:
                        {
                            TransformStream.Write(buffer, offset, count);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SLEOutputStream.Write)} threw an exception");
            }
        }

        private ICryptoTransform CreateAesEncryptor()
        {
            int KeyLengthNeed = Header.KeySize / 8;

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

            switch (Header.Version)
            {
                case SLEVersion.Version_1_5_0:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = Header.KeySize,
                            Mode = CipherMode.ECB,
                            Padding = PaddingMode.None,
                            Key = KeyArray
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
                case SLEVersion.Version_1_1_0:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = Header.KeySize,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            Key = KeyArray,
                            IV = Encoding.UTF8.GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
                default:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = Header.KeySize,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            Key = KeyArray,
                            IV = Encoding.UTF8.GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
            }
        }

        private void WriteHeader()
        {
            byte[] ExtraInfo = Encoding.UTF8.GetBytes($"${Header.KeySize}|{Header.FileName.Replace('$', '_')}|{(int)Header.Version}$");
            BaseFileStream.Write(ExtraInfo, 0, ExtraInfo.Length);
            Header.HeaderLength = ExtraInfo.Length;

            byte[] PasswordConfirm = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
            Write(PasswordConfirm, 0, PasswordConfirm.Length);
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                TransformStream?.Dispose();
                Transform?.Dispose();
                BaseFileStream?.Dispose();
            }
        }

        public SLEOutputStream(Stream BaseFileStream, SLEHeader Header, string Key)
        {
            if (BaseFileStream == null)
            {
                throw new ArgumentNullException(nameof(BaseFileStream), "Argument could not be null");
            }

            if (!BaseFileStream.CanWrite)
            {
                throw new ArgumentException("BaseStream must be writable", nameof(BaseFileStream));
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            this.BaseFileStream = BaseFileStream;
            this.Header = Header;
            this.Key = Key;

            Transform = CreateAesEncryptor();

            switch (Header.Version)
            {
                case SLEVersion.Version_1_5_0:
                    {
                        byte[] Nonce = new EasClientDeviceInformation().Id.ToByteArray().Take(8).ToArray();
                        Array.Resize(ref Nonce, 16);
                        Counter = Nonce;
                        break;
                    }
                case SLEVersion.Version_1_1_0:
                case SLEVersion.Version_1_0_0:
                    {
                        TransformStream = new CryptoStream(BaseFileStream, Transform, CryptoStreamMode.Write);
                        break;
                    }
            }

            WriteHeader();
        }
    }
}
