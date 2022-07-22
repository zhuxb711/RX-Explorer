using System;
using System.ComponentModel;
using System.IO;
using System.Threading;

namespace AuxiliaryTrustProcess
{
    public sealed class ReadonlyProgressReportStream : Stream
    {
        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => BaseStream.CanSeek;

        public override bool CanWrite => false;

        public override long Length => BaseStream.Length;

        public override long Position
        {
            get => BaseStream.Position;
            set => BaseStream.Position = value;
        }

        private readonly Stream BaseStream;
        private readonly CancellationToken CancelToken;
        private readonly ProgressChangedEventHandler ProgressHandler;

        private int ProgressValue = 0;

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int BytesRead = BaseStream.Read(buffer, offset, count);

            if (BytesRead > 0)
            {
                int LatestValue = Math.Max(0, Math.Min(100, Convert.ToInt32(Math.Ceiling(Position * 100d / Length))));

                if (LatestValue > ProgressValue)
                {
                    ProgressValue = LatestValue;
                    ProgressHandler?.Invoke(this, new ProgressChangedEventArgs(LatestValue, null));
                }
            }

            CancelToken.ThrowIfCancellationRequested();

            return BytesRead;
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
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            BaseStream.Dispose();
        }

        public ReadonlyProgressReportStream(Stream BaseStream, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            this.BaseStream = BaseStream ?? throw new ArgumentNullException(nameof(BaseStream), "Argument could not be null");
            this.CancelToken = CancelToken;
            this.ProgressHandler = ProgressHandler;
        }
    }
}
