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

        protected UrlDataPackage RawData { get; set; }

        public override string DisplayType => Globalization.GetString("Url_Admin_DisplayType");

        public async Task<UrlDataPackage> GetRawDataAsync()
        {
            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
            {
                if (ControllerRef != null)
                {
                    return await ControllerRef.Value.Controller.GetUrlDataAsync(Path);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.GetUrlDataAsync(Path);
                    }
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

        public override Task<IStorageItem> GetStorageItemAsync()
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

        public override Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode)
        {
            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    return Task.FromResult(IconStream.AsRandomAccessStream());
                }
            }
            else
            {
                return null;
            }
        }

        public override async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode)
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

        public UrlStorageFile(Win32_File_Data Data) : base(Data)
        {

        }
    }
}
