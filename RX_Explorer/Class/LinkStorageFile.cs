using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class LinkStorageFile : FileSystemStorageFile, ILinkStorageFile
    {
        public ShellLinkType LinkType { get; private set; }

        public string LinkTargetPath => (RawData?.LinkTargetPath) ?? Globalization.GetString("UnknownText");

        public string[] Arguments => (RawData?.Arguments) ?? Array.Empty<string>();

        public bool NeedRunAsAdmin => (RawData?.NeedRunAsAdmin).GetValueOrDefault();

        public override string DisplayType => Globalization.GetString("Link_Admin_DisplayType");

        protected LinkFileData RawData { get; set; }

        public string WorkDirectory => (RawData?.WorkDirectory) ?? string.Empty;

        public string Comment => (RawData?.Comment) ?? string.Empty;

        public WindowState WindowState => (RawData?.WindowState).GetValueOrDefault();

        public byte HotKey => (RawData?.HotKey).GetValueOrDefault();

        public async Task<bool> LaunchAsync()
        {
            try
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    if (LinkType == ShellLinkType.Normal)
                    {
                        return await Exclusive.Controller.RunAsync(LinkTargetPath, WorkDirectory, WindowState, NeedRunAsAdmin, false, false, Arguments);
                    }
                    else
                    {
                        return await Exclusive.Controller.LaunchUWPFromPfnAsync(LinkTargetPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not launch the link file");
                return false;
            }
        }

        public async Task<LinkFileData> GetRawDataAsync()
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return await ControllerRef.Value.Controller.GetLinkDataAsync(Path);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync(PriorityLevel.Low))
                {
                    return await Exclusive.Controller.GetLinkDataAsync(Path);
                }
            }
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

            if (!string.IsNullOrEmpty(RawData?.LinkTargetPath))
            {
                if (System.IO.Path.IsPathRooted(RawData.LinkTargetPath))
                {
                    LinkType = ShellLinkType.Normal;
                }
                else
                {
                    LinkType = ShellLinkType.UWP;
                }
            }
        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                BitmapImage Thumbnail = new BitmapImage();

                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    await Thumbnail.SetSourceAsync(IconStream.AsRandomAccessStream());
                }

                return Thumbnail;
            }

            return new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                        ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                        : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    return IconStream.AsRandomAccessStream();
                }
            }

            StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                                ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                                                                                : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));
            return await ThumbnailFile.OpenReadAsync();
        }

        public LinkStorageFile(NativeFileData Data) : base(Data)
        {
            LinkType = ShellLinkType.Normal;
        }
    }
}
