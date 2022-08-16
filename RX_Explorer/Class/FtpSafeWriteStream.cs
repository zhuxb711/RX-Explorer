using FluentFTP;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FtpSafeWriteStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly FtpClient Client;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => BaseStream.Length;

        public override long Position { get => BaseStream.Position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BaseStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return BaseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            BaseStream.Dispose();

            while (true)
            {
                FtpReply Reply = Client.GetReply();

                if (Reply.Success)
                {
                    if ((Reply.Message?.Contains("NOOP")).GetValueOrDefault())
                    {
                        continue;
                    }
                }

                break;
            }

            base.Dispose(disposing);
        }

        public FtpSafeWriteStream(FtpClient Client, Stream BaseStream)
        {
            if (BaseStream == null)
            {
                throw new ArgumentNullException(nameof(BaseStream), "Argument could not be null");
            }

            if (!BaseStream.CanWrite)
            {
                throw new ArgumentException("Stream must be writeable", nameof(BaseStream));
            }

            this.Client = Client;
            this.BaseStream = BaseStream;
        }
    }
}
