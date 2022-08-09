using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SequentialCrossProcessReadonlyStream : Stream
    {
        private readonly Task WaitConnectionTask;
        private readonly NamedPipeServerStream BaseStream;
        private readonly CancellationTokenSource Cancellation;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => BaseStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!WaitConnectionTask.IsCompleted)
            {
                throw new InvalidOperationException("Stream source data is not ready for read");
            }

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

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            Cancellation.Cancel();
            Cancellation.Dispose();
            BaseStream.Dispose();

            base.Dispose(disposing);
        }

        public SequentialCrossProcessReadonlyStream(string PipeId)
        {
            Cancellation = new CancellationTokenSource();
            BaseStream = new NamedPipeServerStream(@$"LOCAL\{PipeId}", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            WaitConnectionTask = BaseStream.WaitForConnectionAsync(Cancellation.Token).ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception ex && ex is not OperationCanceledException)
                {
                    LogTracer.Log(ex, $"Could not build the connection in {nameof(SequentialCrossProcessReadonlyStream)}");
                }
            });
        }
    }
}
