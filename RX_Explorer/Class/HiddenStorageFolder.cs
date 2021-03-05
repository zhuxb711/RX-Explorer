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
        protected HiddenDataPackage HiddenData { get; set; }

        protected override async Task LoadMorePropertyCore(bool ForceUpdate)
        {
            if (HiddenData == null || ForceUpdate)
            {
                HiddenData = await GetHiddenDataAsync().ConfigureAwait(true);

                if ((HiddenData?.IconData.Length).GetValueOrDefault() > 0)
                {
                    BitmapImage Icon = new BitmapImage();

                    using (MemoryStream Stream = new MemoryStream(HiddenData.IconData))
                    {
                        await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                    }

                    Thumbnail = Icon;
                }
            }
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public async Task<HiddenDataPackage> GetHiddenDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.GetHiddenItemDataAsync(Path).ConfigureAwait(true);
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
