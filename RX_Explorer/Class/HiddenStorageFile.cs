using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class HiddenStorageFile : FileSystemStorageFile, IHiddenStorageItem
    {
        protected HiddenFileData RawData { get; set; }

        public override string DisplayType => (RawData?.DisplayType) ?? Type;

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (RawData == null || ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }
        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                BitmapImage Thumbnail = new BitmapImage();

                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    await Thumbnail.SetSourceAsync(IconStream.AsRandomAccessStream());
                }

                return Thumbnail;
            }

            return null;
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    return Task.FromResult(IconStream.AsRandomAccessStream());
                }
            }

            return null;
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            ThumbnailOpacity = 0.5;
            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        public override Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            return null;
        }

        public override Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            return null;
        }

        public async Task<HiddenFileData> GetRawDataAsync()
        {
            try
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetHiddenItemDataAsync(Path);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetHiddenItemDataAsync(Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(GetRawDataAsync)}");
            }

            return null;
        }

        public HiddenStorageFile(NativeFileData Data) : base(Data)
        {
            if (Data == null)
            {
                throw new ArgumentNullException(nameof(Data));
            }

            base.SetThumbnailOpacity(ThumbnailStatus.ReducedOpacity);
        }
    }
}
