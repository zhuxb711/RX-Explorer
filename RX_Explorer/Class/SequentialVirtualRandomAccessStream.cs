using SharedLibrary;
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

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => SequentialStream.Length;

        public override long Position { get; set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            MakeSurePositionReachable(Position + count);

            int TempBytesRead = TempStream.Read(buffer, offset, count);

            if (TempBytesRead > 0)
            {
                Position += TempBytesRead;
            }

            return TempBytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await MakeSurePositionReachable(Position + count, cancellationToken);

            int TempBytesRead = await TempStream.ReadAsync(buffer, offset, count, cancellationToken);

            if (TempBytesRead > 0)
            {
                Position += TempBytesRead;
            }

            return TempBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
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

        public override void SetLength(long value)
        {
            TempStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MakeSurePositionReachable(Position + count);
            TempStream.Seek(Position, SeekOrigin.Begin);
            TempStream.Write(buffer, offset, count);
            Position += count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await MakeSurePositionReachable(Position + count, cancellationToken);
            await TempStream.WriteAsync(buffer, offset, count, cancellationToken);
            Position += count;
        }

        public override void Flush()
        {
            TempStream.Flush();
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

        private void MakeSurePositionReachable(long Position)
        {
            long RequestPosition = Math.Min(Position, Length);

            if (RequestPosition > TempStream.Length)
            {
                TempStream.Seek(0, SeekOrigin.End);

                do
                {
                    byte[] TempBuffer = new byte[4096];

                    int SequentialBytesRead = SequentialStream.Read(TempBuffer, 0, TempBuffer.Length);

                    if (SequentialBytesRead == 0)
                    {
                        break;
                    }

                    TempStream.Write(TempBuffer, 0, SequentialBytesRead);
                }
                while (RequestPosition > TempStream.Length);
            }

            TempStream.Seek(this.Position, SeekOrigin.Begin);
        }

        private async Task MakeSurePositionReachable(long Position, CancellationToken CancelToken = default)
        {
            long RequestPosition = Math.Min(Position, Length);

            if (RequestPosition > TempStream.Length)
            {
                TempStream.Seek(0, SeekOrigin.End);

                do
                {
                    byte[] TempBuffer = new byte[4096];

                    int SequentialBytesRead = await SequentialStream.ReadAsync(TempBuffer, 0, TempBuffer.Length, CancelToken);

                    if (SequentialBytesRead == 0)
                    {
                        break;
                    }

                    await TempStream.WriteAsync(TempBuffer, 0, SequentialBytesRead, CancelToken);
                }
                while (RequestPosition > TempStream.Length);
            }

            TempStream.Seek(this.Position, SeekOrigin.Begin);
        }

        ~SequentialVirtualRandomAccessStream()
        {
            Dispose(true);
        }

        public static async Task<SequentialVirtualRandomAccessStream> CreateAsync(Stream SequentialStream)
        {
            return new SequentialVirtualRandomAccessStream(SequentialStream, await FileSystemStorageItemBase.CreateTemporaryFileStreamAsync(Preference: SequentialStream.Length >= 1073741824 ? IOPreference.NoPreference : IOPreference.PreferUseMoreMemory));
        }

        private SequentialVirtualRandomAccessStream(Stream SequentialStream, Stream TempStream)
        {
            this.TempStream = TempStream;
            this.SequentialStream = SequentialStream;
        }
    }
}
