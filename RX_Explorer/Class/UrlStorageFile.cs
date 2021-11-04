using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class UrlStorageFile : FileSystemStorageFile, IUrlStorageFile
    {
        public string UrlTargetPath
        {
            get
            {
                return (RawData?.UrlTargetPath) ?? Globalization.GetString("UnknownText");
            }
        }

        protected UrlDataPackage RawData { get; set; }

        public override string DisplayType
        {
            get
            {
                return Globalization.GetString("Url_Admin_DisplayType");
            }
        }

        public async Task<UrlDataPackage> GetRawDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await GetRawDataAsync(Exclusive.Controller);
            }
        }

        public async Task<UrlDataPackage> GetRawDataAsync(FullTrustProcessController Controller)
        {
            return await Controller.GetUrlDataAsync(Path);
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

        public UrlStorageFile(Win32_File_Data Data) : base(Data)
        {

        }
    }
}
