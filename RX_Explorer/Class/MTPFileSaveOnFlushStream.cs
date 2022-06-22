using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class MTPFileSaveOnFlushStream : VirtualSaveOnFlushBaseStream
    {
        private readonly string Path;

        protected override async Task FlushCoreAsync(CancellationToken CancelToken)
        {
            string TempFilePath = System.IO.Path.Combine(ApplicationData.Current.TemporaryFolder.Path, Guid.NewGuid().ToString("N"));

            using (Stream TempStream = await FileSystemStorageItemBase.CreateLocalOneTimeFileStreamAsync(TempFilePath))
            {
                BaseStream.Seek(0, SeekOrigin.Begin);

                await BaseStream.CopyToAsync(TempStream, 1024, CancelToken);
                await TempStream.FlushAsync();

                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync(PriorityLevel.High))
                {
                    await Exclusive.Controller.MTPReplaceWithNewFileAsync(Path, TempFilePath);
                }
            }
        }

        public MTPFileSaveOnFlushStream(string Path, Stream BaseStream) : base(BaseStream)
        {
            this.Path = string.IsNullOrWhiteSpace(Path) ? throw new ArgumentNullException(nameof(Path), "Argument could not be null") : Path;
        }
    }
}
