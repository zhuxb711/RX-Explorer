﻿using FluentFTP;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FtpSafeWriteStream : Stream
    {
        private readonly Stream BaseStream;
        private readonly AsyncFtpClient Client;

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

            Task.Run(() =>
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource(3000))
                {
                    try
                    {
                        while (true)
                        {
                            Task<FtpReply> ReplyTask = Client.GetReply(Cancellation.Token);

                            if (Task.WaitAny(Task.Delay(3000), ReplyTask) > 0)
                            {
                                FtpReply Reply = ReplyTask.Result;

                                if (Reply.Success && (ReplyTask.Result.Message?.Contains("NOOP")).GetValueOrDefault())
                                {
                                    continue;
                                }
                            }

                            break;
                        }
                    }
                    catch (Exception)
                    {
                        //No need to handle this exception
                    }
                }
            });

            base.Dispose(disposing);
        }

        public FtpSafeWriteStream(AsyncFtpClient Client, Stream BaseStream)
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
