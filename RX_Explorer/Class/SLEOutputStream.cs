using System;
using System.Collections.Generic;
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
                return BaseFileStream.Length - Header.HeaderLength;
            }
        }

        public override long Position
        {
            get
            {
                if (Header.Version == SLEVersion.Version_1_2_0)
                {
                    return BaseFileStream.Position - Header.HeaderLength;
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
            switch (Header.Version)
            {
                case SLEVersion.Version_1_2_0:
                    {
                        Queue<byte> XorMask = new Queue<byte>();

                        for (int Index = 0; Index < count; Index++)
                        {
                            if (XorMask.Count == 0)
                            {
                                Array.ConstrainedCopy(BitConverter.GetBytes(Position / BlockSize), 0, Counter, BlockSize / 2, 8);

                                byte[] XorBuffer = new byte[BlockSize];
                                Transform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

                                foreach (byte Xor in XorBuffer)
                                {
                                    XorMask.Enqueue(Xor);
                                }
                            }

                            byte Mask = XorMask.Dequeue();

                            BaseFileStream.WriteByte(Convert.ToByte(buffer[Index] ^ Mask));
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
                case SLEVersion.Version_1_2_0:
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
                Transform?.Dispose();
                TransformStream?.Dispose();
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
                case SLEVersion.Version_1_2_0:
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
