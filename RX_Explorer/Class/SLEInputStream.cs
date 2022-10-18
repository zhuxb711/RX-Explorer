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

        public override long Length => Math.Max(BaseFileStream.Length - FileContentOffset, 0);

        public override long Position
        {
            get
            {
                if (Header.Core.Version >= SLEVersion.SLE150)
                {
                    return Math.Max(BaseFileStream.Position - FileContentOffset, 0);
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
                    BaseFileStream.Position = Convert.ToInt64(value) + FileContentOffset;
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
        private readonly string Key;
        private readonly byte[] Counter;
        private readonly int FileContentOffset;
        private bool IsDisposed;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int Count)
        {
            try
            {
                if (Header.Core.Version >= SLEVersion.SLE150)
                {
                    if (Position + offset > Length)
                    {
                        return 0;
                    }
                    else
                    {
                        long StartPosition = Position + offset;
                        long CurrentBlockIndex = StartPosition / BlockSize;

                        byte[] FileDataBuffer = new byte[Count];
                        byte[] XorBuffer = new byte[BlockSize];

                        int ByteRead = BaseFileStream.Read(FileDataBuffer, offset, FileDataBuffer.Length);

                        long StartBlockOffset = StartPosition % BlockSize;
                        long EndBlockOffset = (StartPosition + ByteRead) % BlockSize;

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
                                    buffer[Index2] = (byte)(XorBuffer[Index2 + StartBlockOffset] ^ FileDataBuffer[Index2]);
                                }

                                Index += LoopCount;
                            }
                            else if (Index + BlockSize > ByteRead)
                            {
                                long LoopCount = Math.Min(EndBlockOffset, Count - Index);

                                for (int Index2 = 0; Index2 < LoopCount; Index2++)
                                {
                                    buffer[Index + Index2] = (byte)(XorBuffer[Index2] ^ FileDataBuffer[Index + Index2]);
                                }

                                break;
                            }
                            else
                            {
                                long LoopCount = Math.Min(BlockSize, Count - Index);

                                for (int Index2 = 0; Index2 < LoopCount; Index2++)
                                {
                                    buffer[Index + Index2] = (byte)(XorBuffer[Index2] ^ FileDataBuffer[Index + Index2]);
                                }

                                Index += LoopCount;
                            }
                        }

                        return ByteRead;
                    }
                }
                else
                {
                    return TransformStream.Read(buffer, offset, Count);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SLEInputStream.Read)} threw an exception");
                return 0;
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

        private ICryptoTransform CreateAesDecryptor()
        {
            if (Key.Any((Char) => Char > '\u007F'))
            {
                throw new NotSupportedException($"Only ASCII char is allowed in {nameof(Key)}");
            }

            int KeyLengthNeed = Header.Core.KeySize / 8;

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

            switch (Header.Core.Version)
            {
                case >= SLEVersion.SLE150:
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.ECB,
                            Padding = PaddingMode.None,
                            KeySize = Header.Core.KeySize,
                            Key = KeyArray,
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
                            KeySize = Header.Core.KeySize,
                            Key = KeyArray,
                            IV = Encoding.UTF8.GetBytes("HqVQ2YgUnUlRNp5Z")
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
                            KeySize = Header.Core.KeySize,
                            Key = KeyArray,
                            IV = Encoding.UTF8.GetBytes("HqVQ2YgUnUlRNp5Z")
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
                BaseFileStream.Seek(FileContentOffset, SeekOrigin.Begin);
            }
        }

        public SLEInputStream(Stream BaseFileStream, Encoding HeaderEncoding, string Key)
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
            this.BaseFileStream.Seek(0, SeekOrigin.Begin);

            Header = SLEHeader.GetHeader(this.BaseFileStream, HeaderEncoding);
            FileContentOffset = Header.HeaderSize + Header.HeaderEncoding.GetByteCount("PASSWORD_CORRECT");
            Transform = CreateAesDecryptor();

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
