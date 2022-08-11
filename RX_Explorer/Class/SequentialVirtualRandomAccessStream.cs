using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SequentialVirtualRandomAccessStream : Stream
    {
        private bool IsDisposed;
        private readonly Stream SequentialStream;
        private readonly Stream TempStream;
        private readonly long SequentialStreamLength;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => SequentialStreamLength > 0 ? SequentialStreamLength : SequentialStream.Length;

        public override long Position { get; set; }

        public override void Flush()
        {
            TempStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            MakeSurePosition(Position + count);

            int TempBytesRead = TempStream.Read(buffer, offset, count);
            Position += TempBytesRead;
            return TempBytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await MakeSurePositionAsync(Position + count, cancellationToken);

            int TempBytesRead = await TempStream.ReadAsync(buffer, offset, count, cancellationToken);
            Position += TempBytesRead;
            return TempBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
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

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MakeSurePosition(Position + count);
            TempStream.Write(buffer, offset, count);
            Position += count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await MakeSurePositionAsync(Position + count, cancellationToken);
            await TempStream.WriteAsync(buffer, offset, count);
            Position += count;
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

        private void MakeSurePosition(long Position)
        {
            while (Math.Min(Position, Length) > TempStream.Length)
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
        }

        private async Task MakeSurePositionAsync(long Position, CancellationToken CancelToken)
        {
            while (Math.Min(Position, Length) > TempStream.Length)
            {
                byte[] TempBuffer = new byte[4096];

                int SequentialBytesRead = await SequentialStream.ReadAsync(TempBuffer, 0, TempBuffer.Length, CancelToken);

                if (SequentialBytesRead > 0)
                {
                    TempStream.Seek(0, SeekOrigin.End);
                    await TempStream.WriteAsync(TempBuffer, 0, TempBuffer.Length, CancelToken);
                }
            }

            TempStream.Seek(Position, SeekOrigin.Begin);
        }

        ~SequentialVirtualRandomAccessStream()
        {
            Dispose(true);
        }

        public static Task<SequentialVirtualRandomAccessStream> CreateAsync(Stream SequentialStream)
        {
            return CreateAsync(SequentialStream, 0);
        }

        public static async Task<SequentialVirtualRandomAccessStream> CreateAsync(Stream SequentialStream, long StreamLength)
        {
            return new SequentialVirtualRandomAccessStream(SequentialStream, await FileSystemStorageItemBase.CreateTemporaryFileStreamAsync(), StreamLength);
        }

        private SequentialVirtualRandomAccessStream(Stream SequentialStream, Stream TempStream, long SequentialStreamLength = 0)
        {
            this.TempStream = TempStream;
            this.SequentialStream = SequentialStream;
            this.SequentialStreamLength = SequentialStreamLength;
        }
    }
}
