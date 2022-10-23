using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Windows.Security.ExchangeActiveSyncProvisioning;

namespace RX_Explorer.Class
{
    public sealed class SLEInputStream : Stream
    {
        private const int BlockSize = 16;

        public override bool CanRead => true;

        public override bool CanSeek => Header.Core.Version >= SLEVersion.SLE150;

        public override bool CanWrite => false;

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

        private readonly Stream BaseFileStream;
        private readonly CryptoStream TransformStream;
        private readonly ICryptoTransform Transform;
        private readonly byte[] Counter;
        private bool IsDisposed;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (offset + count > buffer.Length)
            {
                throw new ArgumentException("The sum of offset and count is larger than the buffer length");
            }

            if (count == 0 || Position == Length)
            {
                return 0;
            }

            if (Header.Core.Version >= SLEVersion.SLE150)
            {
                long StartPosition = Position;
                long CurrentBlockIndex = StartPosition / BlockSize;

                int ByteRead = BaseFileStream.Read(buffer, 0, count);

                if (ByteRead > 0)
                {
                    long TransformIndex = 0;
                    long StartBlockOffset = StartPosition % BlockSize;
                    long EndBlockOffset = (StartPosition + ByteRead) % BlockSize;

                    byte[] XorBuffer = new byte[BlockSize];

                    while (true)
                    {
                        Array.ConstrainedCopy(BitConverter.GetBytes(CurrentBlockIndex++), 0, Counter, BlockSize / 2, 8);

                        Transform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

                        if (TransformIndex == 0)
                        {
                            long LoopCount = Math.Min(BlockSize - StartBlockOffset, count);

                            for (int Index = 0; Index < LoopCount; Index++)
                            {
                                buffer[Index + offset] = (byte)(XorBuffer[Index + StartBlockOffset] ^ buffer[Index + offset]);
                            }

                            TransformIndex += LoopCount;
                        }
                        else if (TransformIndex + BlockSize > ByteRead)
                        {
                            long LoopCount = Math.Min(EndBlockOffset, count - TransformIndex);

                            for (int Index = 0; Index < LoopCount; Index++)
                            {
                                buffer[TransformIndex + Index + offset] = (byte)(XorBuffer[Index] ^ buffer[TransformIndex + Index + offset]);
                            }

                            break;
                        }
                        else
                        {
                            long LoopCount = Math.Min(BlockSize, count - TransformIndex);

                            for (int Index = 0; Index < LoopCount; Index++)
                            {
                                buffer[TransformIndex + Index + offset] = (byte)(XorBuffer[Index] ^ buffer[TransformIndex + Index + offset]);
                            }

                            TransformIndex += LoopCount;
                        }
                    }
                }

                return ByteRead;
            }
            else
            {
                return TransformStream.Read(buffer, offset, count);
            }
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
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private ICryptoTransform CreateAesDecryptor(SLEVersion Version, string DecryptionKey, int KeySize)
        {
            if (DecryptionKey.Any((Char) => Char > '\u007F'))
            {
                throw new NotSupportedException($"Only ASCII char is allowed in {nameof(DecryptionKey)}");
            }

            int KeyWordsNeeded = KeySize / 8;

            switch (Version)
            {
                case >= SLEVersion.SLE150:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.ECB,
                            Padding = PaddingMode.None,
                            KeySize = KeySize,
                            Key = Encoding.ASCII.GetBytes(DecryptionKey.Length > KeyWordsNeeded ? DecryptionKey.Substring(0, KeyWordsNeeded) : DecryptionKey.PadRight(KeyWordsNeeded, '0')),
                        })
                        {
                            return AES.CreateEncryptor();
                        }
                    }
                case SLEVersion.SLE110:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            KeySize = KeySize,
                            Key = Encoding.ASCII.GetBytes(DecryptionKey.Length > KeyWordsNeeded ? DecryptionKey.Substring(0, KeyWordsNeeded) : DecryptionKey.PadRight(KeyWordsNeeded, '0')),
                            IV = new UTF8Encoding(false).GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateDecryptor();
                        }
                    }
                default:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            KeySize = KeySize,
                            Key = Encoding.ASCII.GetBytes(DecryptionKey.Length > KeyWordsNeeded ? DecryptionKey.Substring(0, KeyWordsNeeded) : DecryptionKey.PadRight(KeyWordsNeeded, '0')),
                            IV = new UTF8Encoding(false).GetBytes("HqVQ2YgUnUlRNp5Z")
                        })
                        {
                            return AES.CreateDecryptor();
                        }
                    }
            }
        }

        private bool VerifyPasswordCheckPoint()
        {
            BaseFileStream.Seek(Header.HeaderSize, SeekOrigin.Begin);

            try
            {
                using (StreamReader Reader = new StreamReader(this, Header.HeaderEncoding, true, 128, true))
                {
                    char[] BlockBuffer = new char[Header.HeaderEncoding.GetByteCount("PASSWORD_CORRECT")];

                    if (Reader.ReadBlock(BlockBuffer, 0, BlockBuffer.Length) > 0)
                    {
                        return new string(BlockBuffer) == "PASSWORD_CORRECT";
                    }
                }

                return false;
            }
            finally
            {
                BaseFileStream.Seek(Header.ContentOffset, SeekOrigin.Begin);
            }
        }

        public SLEInputStream(Stream BaseFileStream, Encoding HeaderEncoding, string DecryptionKey)
        {
            if (BaseFileStream == null)
            {
                throw new ArgumentNullException(nameof(BaseFileStream), "Argument could not be null");
            }

            if (!BaseFileStream.CanRead)
            {
                throw new ArgumentException("BaseStream must be writable", nameof(BaseFileStream));
            }

            if (string.IsNullOrEmpty(DecryptionKey))
            {
                throw new ArgumentNullException(nameof(DecryptionKey), "Parameter could not be null or empty");
            }

            this.BaseFileStream = BaseFileStream;
            this.BaseFileStream.Seek(0, SeekOrigin.Begin);

            Header = SLEHeader.GetHeader(BaseFileStream, HeaderEncoding);
            Transform = CreateAesDecryptor(Header.Core.Version, DecryptionKey, (int)Header.Core.KeySize);

            if (Header.Core.Version >= SLEVersion.SLE150)
            {
                Counter = new EasClientDeviceInformation().Id.ToByteArray().Take(8).Concat(Enumerable.Repeat<byte>(0, 8)).ToArray();
            }
            else
            {
                TransformStream = new CryptoStream(BaseFileStream, Transform, CryptoStreamMode.Read);
            }

            if (!VerifyPasswordCheckPoint())
            {
                throw new PasswordErrorException("Password is not correct");
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
    }
}
