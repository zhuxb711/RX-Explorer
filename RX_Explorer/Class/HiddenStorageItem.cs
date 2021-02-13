using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class HiddenStorageItem : FileSystemStorageItemBase
    {
        private HiddenItemPackage DataPackage;

        public override Task<IStorageItem> GetStorageItem()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {

        }

        public override string DisplayType
        {
            get
            {
                return StorageType == StorageItemTypes.File ? (DataPackage?.DisplayType ?? string.Empty) : Globalization.GetString("Folder_Admin_DisplayType");
            }
        }

        protected override async Task LoadMorePropertyCore()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                DataPackage = await Exclusive.Controller.GetHiddenItemDataAsync(Path).ConfigureAwait(true);

                if ((DataPackage?.IconData.Length).GetValueOrDefault() > 0)
                {
                    BitmapImage Icon = new BitmapImage();

                    using (MemoryStream Stream = new MemoryStream(DataPackage.IconData))
                    {
                        await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                    }

                    Thumbnail = Icon;
                }
            }
        }

        public HiddenStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime) : base(Data, StorageType, Path, CreationTime, ModifiedTime)
        {
            base.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity);
        }
    }
}
