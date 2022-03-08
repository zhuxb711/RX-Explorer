using ShareClassLibrary;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class MTPFileReadWriteStream : Stream
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

        private readonly string Path;
        private readonly Stream BaseStream;
        private int IsFlushed;

        public override void Flush()
        {
            FlushCoreAsync(default).Wait();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return FlushCoreAsync(cancellationToken);
        }

        private async Task FlushCoreAsync(CancellationToken CancelToken)
        {
            //Do not remove .ConfigureAwait(false) from this methor because that might be lead to dead lock in sync execution
            if (Interlocked.CompareExchange(ref IsFlushed, 1, 0) == 0)
            {
                BaseStream.Flush();

                if (await FileSystemStorageItemBase.CreateNewAsync(System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Guid.NewGuid().ToString("N")), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(false) is FileSystemStorageFile TempFile)
                {
                    try
                    {
                        BaseStream.Seek(0, SeekOrigin.Begin);

                        using (Stream TempStream = await TempFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                        {
                            await BaseStream.CopyToAsync(TempStream, 1024, CancelToken).ConfigureAwait(false);
                            await TempStream.FlushAsync().ConfigureAwait(false);
                        }

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync().ConfigureAwait(false))
                        {
                            await Exclusive.Controller.MTPReplaceWithNewFileAsync(Path, TempFile.Path).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        await TempFile.DeleteAsync(true).ConfigureAwait(false);
                    }
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
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
            BaseStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            Flush();
            BaseStream.Dispose();
        }

        public MTPFileReadWriteStream(string Path, Stream BaseStream)
        {
            this.Path = string.IsNullOrWhiteSpace(Path) ? throw new ArgumentNullException(nameof(Path), "Argument could not be null") : Path;
            this.BaseStream = BaseStream ?? throw new ArgumentNullException(nameof(BaseStream), "Argument could not be null");
        }
    }
}
