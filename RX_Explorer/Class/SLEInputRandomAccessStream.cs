using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage.Streams;

namespace RX_Explorer.Class
{
    public sealed class SLEInputRandomAccessStream : IRandomAccessStream
    {
        public IInputStream GetInputStreamAt(ulong position)
        {
            Seek(position);
            return this;
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public void Seek(ulong position)
        {
            Position = position;
        }

        public IRandomAccessStream CloneStream()
        {
            return new SLEInputRandomAccessStream(BaseFileStream, Key);
        }

        public bool CanRead => true;

        public bool CanWrite => false;

        public ulong Position
        {
            get
            {
                return Convert.ToUInt64(BaseFileStream.Position - HeaderLength);
            }
            set
            {
                BaseFileStream.Position = Convert.ToInt64(value) + HeaderLength;
            }
        }

        public ulong Size
        {
            get
            {
                return Convert.ToUInt64(BaseFileStream.Length - HeaderLength);
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public SLEHeader Header { get; }

        private readonly Stream BaseFileStream;
        private readonly ICryptoTransform EncryptTransform;
        private readonly string Key;
        private readonly byte[] Counter;
        private long HeaderLength;

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            return AsyncInfo.Run<IBuffer, uint>((Token, Progress) =>
            {
                return Task.Run(() =>
                {
                    const int BlockSize = 16;

                    long DataStartPosition = Convert.ToInt64(Position);
                    long StartBlockIndex = DataStartPosition / BlockSize;
                    long BlockStartPosition = StartBlockIndex * BlockSize;

                    BaseFileStream.Seek(BlockStartPosition, SeekOrigin.Begin);

                    int BlockNumNeedToRead = Convert.ToInt32(Math.Ceiling(Convert.ToSingle(count) / BlockSize));

                    byte[] FileDataBuffer = new byte[BlockNumNeedToRead * BlockSize];

                    int ByteRead = BaseFileStream.Read(FileDataBuffer, 0, FileDataBuffer.Length);

                    if (ByteRead > 0)
                    {
                        using (Stream BufferStream = buffer.AsStream())
                        {
                            Queue<byte> XorMask = new Queue<byte>();

                            for (int Index = 0, WriteCount = 0; Index < ByteRead; Index++)
                            {
                                if (XorMask.Count == 0)
                                {
                                    byte[] XorBuffer = new byte[BlockSize];
                                    EncryptTransform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

                                    foreach (byte Xor in XorBuffer)
                                    {
                                        XorMask.Enqueue(Xor);
                                    }

                                    long CurrentBlockIndex = StartBlockIndex + Index / BlockSize;
                                    Array.ConstrainedCopy(BitConverter.GetBytes(CurrentBlockIndex), 0, Counter, BlockSize - 8, 8);
                                }

                                byte Mask = XorMask.Dequeue();

                                if (Index >= DataStartPosition - BlockStartPosition)
                                {
                                    BufferStream.WriteByte(Convert.ToByte(FileDataBuffer[Index] ^ Mask));
                                    WriteCount++;

                                    if (WriteCount >= count)
                                    {
                                        break;
                                    }
                                }
                            }

                            BufferStream.Flush();
                        }

                        BaseFileStream.Seek(DataStartPosition + count, SeekOrigin.Begin);
                    }
                    else
                    {
                        Progress.Report(100);
                    }

                    return buffer;
                }, Token);
            });
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            throw new NotSupportedException();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            throw new NotSupportedException();
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

            using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
            {
                Mode = CipherMode.ECB,
                Padding = PaddingMode.None,
                KeySize = Header.KeySize,
                Key = KeyArray,
            })
            {
                return AES.CreateEncryptor();
            }
        }

        private bool VerifyPassword()
        {
            const int BlockSize = 16;

            byte[] FileData = new byte[BlockSize];

            BaseFileStream.Seek(HeaderLength, SeekOrigin.Begin);
            BaseFileStream.Read(FileData, 0, FileData.Length);

            byte[] XorBuffer = new byte[BlockSize];
            EncryptTransform.TransformBlock(Counter, 0, Counter.Length, XorBuffer, 0);

            byte[] PasswordConfirm = new byte[BlockSize];

            for (int Index = 0; Index < PasswordConfirm.Length; Index++)
            {
                PasswordConfirm[Index] = Convert.ToByte(XorBuffer[Index] ^ FileData[Index]);
            }

            if (Encoding.UTF8.GetString(PasswordConfirm) == "PASSWORD_CORRECT")
            {
                HeaderLength += BlockSize;
                return true;
            }
            else
            {
                return false;
            }
        }

        public SLEInputRandomAccessStream(Stream BaseFileStream, string Key)
        {
            this.Key = Key;
            this.BaseFileStream = BaseFileStream;
            this.BaseFileStream.Seek(0, SeekOrigin.Begin);

            Header = SLEHeader.GetHeader(this.BaseFileStream);
            HeaderLength = Header.HeaderLength;

            EncryptTransform = CreateAesEncryptor();

            byte[] Nonce = new EasClientDeviceInformation().Id.ToByteArray().Take(8).ToArray();
            Array.Resize(ref Nonce, 16);
            Counter = Nonce;

            if (!VerifyPassword())
            {
                throw new PasswordErrorException("Password is not correct");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            EncryptTransform?.Dispose();
            BaseFileStream?.Dispose();
        }

        ~SLEInputRandomAccessStream()
        {
            Dispose();
        }
    }
}
