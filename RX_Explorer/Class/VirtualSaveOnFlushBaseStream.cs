using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public abstract class VirtualSaveOnFlushBaseStream : Stream
    {
        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => BaseStream.CanWrite;

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        private int HasWroteAnyData;
        private readonly SemaphoreSlim FlushLocker = new SemaphoreSlim(1, 1);

        protected Stream BaseStream { get; }

        public override void Flush()
        {
            FlushAsync(default).Wait();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {            
            //Do not remove .ConfigureAwait(false) from this methor because that might be lead to dead lock in sync execution
            if (Interlocked.CompareExchange(ref HasWroteAnyData, 0, 1) == 1)
            {
                await FlushLocker.WaitAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    await BaseStream.FlushAsync().ConfigureAwait(false);
                    await FlushCoreAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    FlushLocker.Release();
                }
            }
        }

        protected abstract Task FlushCoreAsync(CancellationToken CancelToken);

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
