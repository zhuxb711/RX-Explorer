using Nito.AsyncEx;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public abstract class VirtualSaveOnFlushBaseStream : Stream
    {
        private int HasWroteAnyData;
        private readonly AsyncLock FlushLocker = new AsyncLock();
        private readonly Stream BaseStream;

        public override bool CanRead => true;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => true;

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        public override void Flush()
        {
            FlushAsync(default).Wait();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            //Do not remove .ConfigureAwait(false) from this methor because that might be lead to dead lock in sync execution
            if (Interlocked.CompareExchange(ref HasWroteAnyData, 0, 1) == 1)
            {
                using (await FlushLocker.LockAsync(cancellationToken).ConfigureAwait(false))
                {
                    await BaseStream.FlushAsync().ConfigureAwait(false);
                    await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected abstract Task FlushCoreAsync(CancellationToken CancelToken = default);

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return BaseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return BaseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            BaseStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Interlocked.CompareExchange(ref HasWroteAnyData, 1, 0);
            BaseStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Interlocked.CompareExchange(ref HasWroteAnyData, 1, 0);
            return BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }


        protected override void Dispose(bool disposing)
        {
            Flush();
            BaseStream.Dispose();
            base.Dispose(disposing);
        }

        protected VirtualSaveOnFlushBaseStream(Stream BaseStream)
        {
            this.BaseStream = BaseStream ?? throw new ArgumentNullException(nameof(BaseStream), "Argument could not be null");
        }
    }
}
