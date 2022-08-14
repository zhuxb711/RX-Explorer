using FluentFTP;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FtpSafeReadStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly FtpClient Client;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => BaseStream.Length;

        public override long Position { get => BaseStream.Position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

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
            throw new NotSupportedException();
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

        public FtpSafeReadStream(FtpClient Client, Stream BaseStream)
        {
            if (BaseStream == null)
            {
                throw new ArgumentNullException(nameof(BaseStream), "Argument could not be null");
            }

            if (!BaseStream.CanRead)
            {
                throw new ArgumentException("Stream must be readable", nameof(BaseStream));
            }

            this.Client = Client;
            this.BaseStream = BaseStream;
        }
    }
}
