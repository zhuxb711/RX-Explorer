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
    public class LinkStorageFile : FileSystemStorageFile, ILinkStorageFile
    {
        public ShellLinkType LinkType { get; private set; }

        public string LinkTargetPath => (RawData?.LinkTargetPath) ?? Globalization.GetString("UnknownText");

        public string[] Arguments => (RawData?.Arguments) ?? Array.Empty<string>();

        public bool NeedRunAsAdmin => (RawData?.NeedRunAsAdmin).GetValueOrDefault();

        public override string DisplayType => Globalization.GetString("Link_Admin_DisplayType");

        protected LinkDataPackage RawData { get; set; }

        public string WorkDirectory => (RawData?.WorkDirectory) ?? string.Empty;

        public string Comment => (RawData?.Comment) ?? string.Empty;

        public WindowState WindowState => (RawData?.WindowState).GetValueOrDefault();

        public int HotKey => (RawData?.HotKey).GetValueOrDefault();

        public async Task<bool> LaunchAsync()
        {
            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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

        public async Task<LinkDataPackage> GetRawDataAsync()
        {
            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
            {
                if (ControllerRef != null)
                {
                    return await ControllerRef.Value.Controller.GetLinkDataAsync(Path);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.GetLinkDataAsync(Path);
                    }
                }
            }
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override Task<FileStream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            return null;
        }

        public override Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            return null;
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            RawData = await GetRawDataAsync();

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

        public override Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode)
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

        public LinkStorageFile(Win32_File_Data Data) : base(Data)
        {
            LinkType = ShellLinkType.Normal;
        }
    }
}
