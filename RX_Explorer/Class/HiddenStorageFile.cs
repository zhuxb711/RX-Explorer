using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class HiddenStorageFile : FileSystemStorageFile, IHiddenStorageItem
    {
        protected HiddenDataPackage RawData { get; set; }

        public override string DisplayType
        {
            get
            {
                return (RawData?.DisplayType) ?? Type;
            }
        }

        protected override async Task LoadMorePropertiesCore(bool ForceUpdate)
        {
            RawData = await GetRawDataAsync();

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    BitmapImage Image = new BitmapImage();
                    await Image.SetSourceAsync(IconStream.AsRandomAccessStream());
                    Thumbnail = Image;
                }
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

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            ThumbnailOpacity = 0.5;
            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        public async Task<HiddenDataPackage> GetRawDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.GetHiddenItemDataAsync(Path);
            }
        }

        public HiddenStorageFile(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {
            base.SetThumbnailOpacity(ThumbnailStatus.ReducedOpacity);
        }
    }
}
