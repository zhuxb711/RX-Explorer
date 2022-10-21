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

        public override bool CanRead => false;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => Math.Max(BaseFileStream.Length - Header.ContentOffset, 0);

        public override long Position
        {
            get
            {
                if (Header.Core.Version >= SLEVersion.SLE150)
                {
                    return Math.Max(BaseFileStream.Position - Header.ContentOffset, 0);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            set
            {
                if (Header.Core.Version >= SLEVersion.SLE150)
                {
                    BaseFileStream.Position = value + Header.ContentOffset;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        public SLEHeader Header { get; }

        private readonly ICryptoTransform Transform;
        private readonly Stream BaseFileStream;
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
            if (Header.Core.Version >= SLEVersion.SLE150)
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        {
                            Position = offset;
                            break;
                        }
                    case SeekOrigin.Current:
                        {
                            Position += offset;
                            break;
                        }
                    case SeekOrigin.End:
                        {
                            Position = Length + offset;
                            break;
                        }
                }

                return Position;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public override void SetLength(long value)
        {
            BaseFileStream.SetLength(Math.Max(value + Header.ContentOffset, 0));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position + offset > Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            int Count = Math.Max(0, Math.Min(buffer.Length, count));

            if (Count > 0)
            {
                if (Header.Core.Version >= SLEVersion.SLE150)
                {
                    long StartPosition = Position + offset;
                    long CurrentBlockIndex = StartPosition / BlockSize;

                    byte[] XorBuffer = new byte[BlockSize];
                    byte[] OutputBuffer = new byte[Count];

                    long StartBlockOffset = StartPosition % BlockSize;
                    long EndBlockOffset = (StartPosition + Count) % BlockSize;

                    long Index = 0;

                    while (true)
                    {
                        Array.ConstrainedCopy(BitConverter.GetBytes(CurrentBlockIndex++), 0, Counter, BlockSize / 2, 8);

                        Transform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

                        if (Index == 0)
                        {
                            long LoopCount = Math.Min(BlockSize - StartBlockOffset, Count);

                            for (int Index2 = 0; Index2 < LoopCount; Index2++)
                            {
                                OutputBuffer[Index2] = (byte)(XorBuffer[Index2 + StartBlockOffset] ^ buffer[Index2]);
                            }

                            Index += LoopCount;
                        }
                        else if (Index + BlockSize > Count)
                        {
                            long LoopCount = Math.Min(EndBlockOffset, Count - Index);

                            for (int Index2 = 0; Index2 < LoopCount; Index2++)
                            {
                                OutputBuffer[Index + Index2] = (byte)(XorBuffer[Index2] ^ buffer[Index + Index2]);
                            }

                            break;
                        }
                        else
                        {
                            long LoopCount = Math.Min(BlockSize, Count - Index);

                            for (int Index2 = 0; Index2 < LoopCount; Index2++)
                            {
                                OutputBuffer[Index + Index2] = (byte)(XorBuffer[Index2] ^ buffer[Index + Index2]);
                            }

                            Index += LoopCount;
                        }
                    }

                    BaseFileStream.Write(OutputBuffer, offset, Count);
                }
                else
                {
                    TransformStream.Write(buffer, offset, Count);
                }
            }
        }

        private ICryptoTransform CreateAesEncryptor(SLEVersion Version, string EncryptionKey, int KeySize)
        {
            if (EncryptionKey.Any((Char) => Char > '\u007F'))
            {
                throw new NotSupportedException($"Only ASCII char is allowed in {nameof(EncryptionKey)}");
            }

            int KeyWordsNeeded = KeySize / 8;

            switch (Version)
            {
                case >= SLEVersion.SLE150:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Mode = CipherMode.ECB,
                            Padding = PaddingMode.None,
                            Key = Encoding.ASCII.GetBytes(EncryptionKey.Length > KeyWordsNeeded ? EncryptionKey.Substring(0, KeyWordsNeeded) : EncryptionKey.PadRight(KeyWordsNeeded, '0'))
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
                case SLEVersion.SLE110:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            Key = Encoding.ASCII.GetBytes(EncryptionKey.Length > KeyWordsNeeded ? EncryptionKey.Substring(0, KeyWordsNeeded) : EncryptionKey.PadRight(KeyWordsNeeded, '0')),
                            IV = new UTF8Encoding(false).GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
                default:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            Key = Encoding.ASCII.GetBytes(EncryptionKey.Length > KeyWordsNeeded ? EncryptionKey.Substring(0, KeyWordsNeeded) : EncryptionKey.PadRight(KeyWordsNeeded, '0')),
                            IV = new UTF8Encoding(false).GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
            }
        }

        private void WritePasswordCheckPoint()
        {
            BaseFileStream.Seek(Header.HeaderSize, SeekOrigin.Begin);

            try
            {
                using (StreamWriter Writer = new StreamWriter(this, Header.HeaderEncoding, 128, true))
                {
                    Writer.Write("PASSWORD_CORRECT");
                    Writer.Flush();
                }
            }
            finally
            {
                BaseFileStream.Seek(Header.ContentOffset, SeekOrigin.Begin);
            }
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

        public SLEOutputStream(Stream BaseFileStream, SLEVersion Version, SLEOriginType OriginType, SLEKeySize KeySize, Encoding HeaderEncoding, string FileName, string EncryptionKey)
        {
            if (BaseFileStream == null)
            {
                throw new ArgumentNullException(nameof(BaseFileStream), "Argument could not be null");
            }

            if (!BaseFileStream.CanWrite)
            {
                throw new ArgumentException("BaseStream must be writable", nameof(BaseFileStream));
            }

            if (string.IsNullOrEmpty(EncryptionKey))
            {
                throw new ArgumentNullException(nameof(EncryptionKey), "Parameter could not be null or empty");
            }

            this.BaseFileStream = BaseFileStream;
            this.BaseFileStream.Seek(0, SeekOrigin.Begin);

            Header = new SLEHeader(Version, OriginType, KeySize, HeaderEncoding, FileName);
            Header.WriteHeader(BaseFileStream);
            Transform = CreateAesEncryptor(Header.Core.Version, EncryptionKey, (int)Header.Core.KeySize);

            if (Version >= SLEVersion.SLE150)
            {
                Counter = new EasClientDeviceInformation().Id.ToByteArray().Take(8).Concat(Enumerable.Repeat<byte>(0, 8)).ToArray();
            }
            else
            {
                TransformStream = new CryptoStream(BaseFileStream, Transform, CryptoStreamMode.Write);
            }

            WritePasswordCheckPoint();
        }
    }
}
