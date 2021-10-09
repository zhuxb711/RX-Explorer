using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class HiddenStorageFolder : FileSystemStorageFolder, IHiddenStorageItem
    {
        protected HiddenDataPackage RawData { get; set; }

        protected override async Task LoadCoreAsync(FullTrustProcessController Controller, bool ForceUpdate)
        {
            RawData = await GetRawDataAsync(Controller);
        }

        protected override async Task<BitmapImage> GetThumbnailAsync(FullTrustProcessController Controller, ThumbnailMode Mode)
        {
            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    BitmapImage Image = new BitmapImage();
                    await Image.SetSourceAsync(IconStream.AsRandomAccessStream());
                    return Image;
                }
            }
            else
            {
                return null;
            }
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public async Task<HiddenDataPackage> GetRawDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await GetRawDataAsync(Exclusive.Controller);
            }
        }

        public async Task<HiddenDataPackage> GetRawDataAsync(FullTrustProcessController Controller)
        {
            return await Controller.GetHiddenItemDataAsync(Path);
        }

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            ThumbnailOpacity = 0.5;
            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        public HiddenStorageFolder(Win32_File_Data Data) : base(Data)
        {
            base.SetThumbnailOpacity(ThumbnailStatus.ReducedOpacity);
        }
    }
}
