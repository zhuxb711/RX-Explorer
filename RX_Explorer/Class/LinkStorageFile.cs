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
    public class LinkStorageFile : FileSystemStorageFile, ILinkStorageFile
    {
        public ShellLinkType LinkType { get; private set; }

        public string LinkTargetPath
        {
            get
            {
                return (RawData?.LinkTargetPath) ?? Globalization.GetString("UnknownText");
            }
        }

        public string[] Arguments
        {
            get
            {
                return (RawData?.Arguments) ?? Array.Empty<string>();
            }
        }

        public bool NeedRunAsAdmin
        {
            get
            {
                return (RawData?.NeedRunAsAdmin).GetValueOrDefault();
            }
        }

        public override string DisplayType
        {
            get
            {
                return Globalization.GetString("Link_Admin_DisplayType");
            }
        }

        protected LinkDataPackage RawData { get; set; }

        public string WorkDirectory
        {
            get
            {
                return (RawData?.WorkDirectory) ?? string.Empty;
            }
        }

        public string Comment
        {
            get
            {
                return (RawData?.Comment) ?? string.Empty;
            }
        }

        public WindowState WindowState
        {
            get
            {
                return (RawData?.WindowState).GetValueOrDefault();
            }
        }

        public int HotKey
        {
            get
            {
                return (RawData?.HotKey).GetValueOrDefault();
            }
        }

        public async Task<bool> LaunchAsync()
        {
            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await GetRawDataAsync(Exclusive.Controller);
            }
        }

        public async Task<LinkDataPackage> GetRawDataAsync(FullTrustProcessController Controller)
        {
            return await Controller.GetLinkDataAsync(Path);
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                RawData = await GetRawDataAsync(Exclusive.Controller);

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

        public LinkStorageFile(Win32_File_Data Data) : base(Data)
        {
            LinkType = ShellLinkType.Normal;
        }
    }
}
