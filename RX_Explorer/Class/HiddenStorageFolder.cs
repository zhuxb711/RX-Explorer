using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class HiddenStorageFolder : FileSystemStorageFolder, IHiddenStorageItem
    {
        protected HiddenDataPackage RawData { get; set; }

        protected override async Task LoadMorePropertyCore()
        {
            RawData = await GetRawDataAsync();

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                BitmapImage Icon = new BitmapImage();

                using (MemoryStream Stream = new MemoryStream(RawData.IconData))
                {
                    await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                }

                Thumbnail = Icon;
            }
        }

        protected override bool CheckIfPropertiesLoaded()
        {
            return RawData != null;
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public async Task<HiddenDataPackage> GetRawDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.GetHiddenItemDataAsync(Path);
            }
        }

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            ThumbnailOpacity = 0.5;
            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        public HiddenStorageFolder(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {
            base.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity);
        }
    }
}
