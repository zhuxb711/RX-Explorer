using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
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
                return LinkData?.LinkTargetPath ?? Globalization.GetString("UnknownText");
            }
        }

        public string[] Arguments
        {
            get
            {
                return LinkData?.Argument ?? Array.Empty<string>();
            }
        }

        public bool NeedRunAsAdmin
        {
            get
            {
                return (LinkData?.NeedRunAsAdmin).GetValueOrDefault();
            }
        }

        public override string DisplayType
        {
            get
            {
                return Globalization.GetString("Link_Admin_DisplayType");
            }
        }

        protected LinkDataPackage LinkData { get; set; }

        public async Task LaunchAsync()
        {
            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    if (LinkType == ShellLinkType.Normal)
                    {
                        await Exclusive.Controller.RunAsync(LinkTargetPath, NeedRunAsAdmin, false, false, Arguments).ConfigureAwait(true);
                    }
                    else
                    {
                        if (!await Exclusive.Controller.LaunchUWPLnkAsync(LinkTargetPath).ConfigureAwait(true))
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog.ShowAsync().ConfigureAwait(true);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync().ConfigureAwait(true);
                });
            }
        }

        public async Task<LinkDataPackage> GetLinkDataAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.GetLnkDataAsync(Path).ConfigureAwait(true);
            }
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override async Task LoadMorePropertyCore(bool ForceUpdate)
        {
            if (ForceUpdate || LinkData == null)
            {
                LinkData = await GetLinkDataAsync().ConfigureAwait(true);

                if (!string.IsNullOrEmpty(LinkData.LinkTargetPath))
                {
                    if (LinkData.IconData.Length != 0)
                    {
                        using (MemoryStream IconStream = new MemoryStream(LinkData.IconData))
                        {
                            Thumbnail = new BitmapImage();
                            await Thumbnail.SetSourceAsync(IconStream.AsRandomAccessStream());
                        }
                    }

                    if (await CheckExistAsync(LinkData.LinkTargetPath).ConfigureAwait(true))
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

        public LinkStorageFile(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {
            LinkType = ShellLinkType.Normal;
        }
    }
}
