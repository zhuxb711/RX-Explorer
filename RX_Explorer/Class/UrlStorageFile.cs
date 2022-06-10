using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class UrlStorageFile : FileSystemStorageFile, IUrlStorageFile
    {
        public string UrlTargetPath => (RawData?.UrlTargetPath) ?? Globalization.GetString("UnknownText");

        protected UrlFileData RawData { get; set; }

        public override string DisplayType => Globalization.GetString("Url_Admin_DisplayType");

        public async Task<UrlFileData> GetRawDataAsync()
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return await ControllerRef.Value.Controller.GetUrlDataAsync(Path);
                }
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await Exclusive.Controller.GetUrlDataAsync(Path);
                }
            }
        }

        public async Task<bool> LaunchAsync()
        {
            if (!string.IsNullOrWhiteSpace(UrlTargetPath))
            {
                if (Uri.TryCreate(UrlTargetPath, UriKind.RelativeOrAbsolute, out Uri Url))
                {
                    return await Launcher.LaunchUriAsync(Url);
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        protected override Task<IStorageItem> GetStorageItemCoreAsync(bool ForceUpdate)
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (RawData == null || ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }
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

        public UrlStorageFile(NativeFileData Data) : base(Data)
        {

        }
    }
}
