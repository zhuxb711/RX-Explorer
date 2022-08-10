using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SequentialStreamRandomAccessStream : Stream
    {
        private bool IsDisposed;
        private readonly Stream SequentialStream;
        private readonly Stream TempStream;
        private readonly long SequentialStreamLength;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => SequentialStreamLength > 0 ? SequentialStreamLength : SequentialStream.Length;

        public override long Position { get; set; }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                while (Math.Min(Position + count, Length) > TempStream.Length)
                {
                    byte[] TempBuffer = new byte[4096];

                    int SequentialBytesRead = SequentialStream.Read(TempBuffer, 0, TempBuffer.Length);

                    if (SequentialBytesRead > 0)
                    {
                        TempStream.Seek(0, SeekOrigin.End);
                        TempStream.Write(TempBuffer, 0, SequentialBytesRead);
                    }
                }

                TempStream.Seek(Position, SeekOrigin.Begin);

                int TempBytesRead = TempStream.Read(buffer, offset, count);
                Position += TempBytesRead;
                return TempBytesRead;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                while (Math.Min(Position + count, Length) > TempStream.Length)
                {
                    byte[] TempBuffer = new byte[4096];

                    int SequentialBytesRead = await SequentialStream.ReadAsync(TempBuffer, 0, TempBuffer.Length, cancellationToken);

                    if (SequentialBytesRead > 0)
                    {
                        TempStream.Seek(0, SeekOrigin.End);
                        await TempStream.WriteAsync(TempBuffer, 0, TempBuffer.Length, cancellationToken);
                    }
                }

                TempStream.Seek(Position, SeekOrigin.Begin);

                int TempBytesRead = await TempStream.ReadAsync(buffer, offset, count, cancellationToken);
                Position += TempBytesRead;
                return TempBytesRead;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            try
            {
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        {
                            if (offset >= Length)
                            {
                                throw new ArgumentOutOfRangeException();
                            }

                            return Position = offset;
                        }
                    case SeekOrigin.Current:
                        {
                            long ActualPosition = Position + offset;

                            if (ActualPosition >= Length)
                            {
                                throw new ArgumentOutOfRangeException();
                            }

                            return Position = ActualPosition;
                        }
                    case SeekOrigin.End:
                        {
                            if (offset > 0)
                            {
                                throw new ArgumentOutOfRangeException();
                            }

                            return Position = Length - 1 + offset;
                        }
                    default:
                        {
                            throw new ArgumentException();
                        }
                }
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine($"请求定位位置: {Position}, 总长度: {Length}");
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

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                GC.SuppressFinalize(this);

                TempStream.Dispose();
                SequentialStream.Dispose();

                base.Dispose(disposing);
            }
        }

        ~SequentialStreamRandomAccessStream()
        {
            Dispose(true);
        }

        public static Task<SequentialStreamRandomAccessStream> CreateAsync(Stream SequentialStream)
        {
            return CreateAsync(SequentialStream, 0);
        }

        public static async Task<SequentialStreamRandomAccessStream> CreateAsync(Stream SequentialStream, long StreamLength)
        {
            return new SequentialStreamRandomAccessStream(SequentialStream, await FileSystemStorageItemBase.CreateTemporaryFileStreamAsync(), StreamLength);
        }

        private SequentialStreamRandomAccessStream(Stream SequentialStream, Stream TempStream, long SequentialStreamLength = 0)
        {
            this.TempStream = TempStream;
            this.SequentialStream = SequentialStream;
            this.SequentialStreamLength = SequentialStreamLength;
        }
    }
}
