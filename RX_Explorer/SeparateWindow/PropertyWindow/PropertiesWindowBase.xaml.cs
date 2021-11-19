using ComputerVision;
using Microsoft.Toolkit.Deferred;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.SeparateWindow.PropertyWindow
{
    public sealed partial class PropertiesWindowBase : Page
    {
        private readonly AppWindow Window;
        private readonly FileSystemStorageItemBase StorageItem;
        private readonly ObservableCollection<PropertiesGroupItem> PropertiesCollection = new ObservableCollection<PropertiesGroupItem>();
        private static readonly Dictionary<uint, string> OfflineAvailabilityMap = new Dictionary<uint, string>(3)
        {
            { 0, Globalization.GetString("OfflineAvailabilityText1") },
            { 1, Globalization.GetString("OfflineAvailabilityText2") },
            { 2, Globalization.GetString("OfflineAvailabilityText3") }
        };

        /*
        * | System.FilePlaceholderStatus       | Value    | Description                                                                                                        |
        * | ---------------------------------- | -------- | ------------------------------------------------------------------------------------------------------------------ |
        * | PS_NONE                            | 0        | None of the other states apply at this time                                                                        |
        * | PS_MARKED_FOR_OFFLINE_AVAILABILITY | 1        | May already be or eventually will be available offline                                                             |
        * | PS_FULL_PRIMARY_STREAM_AVAILABLE   | 2        | The primary stream has been made fully available                                                                   |
        * | PS_CREATE_FILE_ACCESSIBLE          | 4        | The file is accessible through a call to the CreateFile function, without requesting the opening of reparse points |
        * | PS_CLOUDFILE_PLACEHOLDER           | 8        | The file is a cloud file placeholder                                                                               |
        * | PS_DEFAULT                         | 7        | A bitmask value for default flags                                                                                  | 
        * | PS_ALL                             | 15       | A bitmask value for all valid PLACEHOLDER_STATES flags                                                             |
        */
        private static readonly Dictionary<uint, string> OfflineAvailabilityStatusMap = new Dictionary<uint, string>(10)
        {
            { 0, Globalization.GetString("OfflineAvailabilityStatusText1") },
            { 1, Globalization.GetString("OfflineAvailabilityStatusText2") },
            { 2, Globalization.GetString("OfflineAvailabilityStatusText3") },
            { 3, Globalization.GetString("OfflineAvailabilityStatusText3") },
            { 4, Globalization.GetString("OfflineAvailabilityStatusText5") },
            { 5, Globalization.GetString("OfflineAvailabilityStatusText6") },
            { 6, Globalization.GetString("OfflineAvailabilityStatusText8") },
            { 7, Globalization.GetString("OfflineAvailabilityStatusText3") },
            { 8, Globalization.GetString("OfflineAvailabilityStatusText1") },
            { 9, Globalization.GetString("OfflineAvailabilityStatusText7") },
            { 14, Globalization.GetString("OfflineAvailabilityStatusText3") },
            { 15, Globalization.GetString("OfflineAvailabilityStatusText3") },
        };

        private static readonly Size WindowSize = new Size(450, 620);

        private CancellationTokenSource FolderCancellation;
        private CancellationTokenSource Md5Cancellation;
        private CancellationTokenSource SHA1Cancellation;
        private CancellationTokenSource SHA256Cancellation;
        private int ConfirmButtonLockResource;

        private readonly PointerEventHandler PointerPressedHandler;
        private readonly PointerEventHandler PointerReleasedHandler;
        private readonly PointerEventHandler PointerCanceledHandler;
        private readonly PointerEventHandler PointerMovedHandler;

        public event EventHandler WindowClosed;
        public event EventHandler<FileRenamedDeferredEventArgs> RenameRequested;

        /// <summary>
        /// If want to handle rename operation mannually. Please set this property to false and subscribe RenameRequested event
        /// </summary>
        public bool HandleRenameAutomatically { get; set; } = true;

        public static async Task<PropertiesWindowBase> CreateAsync(FileSystemStorageItemBase StorageItem)
        {
            AppWindow NewWindow = await AppWindow.TryCreateAsync();
            NewWindow.PersistedStateId = "Properties";
            NewWindow.Title = Globalization.GetString("Properties_Window_Title");
            NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
            NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            await StorageItem.LoadAsync();

            PropertiesWindowBase PropertiesWindow = new PropertiesWindowBase(NewWindow, StorageItem);

            ElementCompositionPreview.SetAppWindowContent(NewWindow, PropertiesWindow);
            WindowManagementPreview.SetPreferredMinSize(NewWindow, WindowSize);

            return PropertiesWindow;
        }

        public async Task ShowAsync(Point ShowAt)
        {
            Window.RequestMoveRelativeToCurrentViewContent(ShowAt);
            await Window.TryShowAsync();
        }

        private PropertiesWindowBase(AppWindow Window, FileSystemStorageItemBase StorageItem)
        {
            InitializeComponent();

            LocationScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(LocationScrollViewer_PointerPressed), true);
            LocationScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(LocationScrollViewer_PointerReleased), true);
            LocationScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(LocationScrollViewer_PointerCanceled), true);
            LocationScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(LocationScrollViewer_PointerMoved), true);

            this.Window = Window;
            this.StorageItem = StorageItem;

            PropertiesTitleLeft.Text = StorageItem is FileSystemStorageFolder ? (string.IsNullOrEmpty(StorageItem.DisplayName)
                                                                                  ? StorageItem.Name
                                                                                  : StorageItem.DisplayName)
                                                                              : StorageItem.Name;
            GeneralTab.Text = Globalization.GetString("Properties_General_Tab");
            ShortcutTab.Text = Globalization.GetString("Properties_Shortcut_Tab");
            DetailsTab.Text = Globalization.GetString("Properties_Details_Tab");
            ToolsTab.Text = Globalization.GetString("Properties_Tools_Tab");

            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText1"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText2"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText3"));

            ShortcutWindowsStateContent.SelectedIndex = 0;


            if (StorageItem is FileSystemStorageFolder)
            {
                GeneralSubGrid.RowDefinitions[2].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[3].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[6].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[7].Height = new GridLength(35);
                GeneralSubGrid.RowDefinitions[10].Height = new GridLength(0);

                OpenWithPanel.Visibility = Visibility.Collapsed;

                while (PivotControl.Items.Count > 1)
                {
                    PivotControl.Items.RemoveAt(PivotControl.Items.Count - 1);
                }
            }
            else if (StorageItem is FileSystemStorageFile)
            {
                switch (StorageItem.Type.ToLower())
                {
                    case ".exe":
                    case ".bat":
                    case ".lnk":
                    case ".url":
                        {
                            GeneralSubGrid.RowDefinitions[2].Height = new GridLength(0);
                            break;
                        }
                    default:
                        {
                            GeneralSubGrid.RowDefinitions[2].Height = new GridLength(35);
                            break;
                        }
                }

                GeneralSubGrid.RowDefinitions[3].Height = new GridLength(10);
                GeneralSubGrid.RowDefinitions[6].Height = new GridLength(35);
                GeneralSubGrid.RowDefinitions[7].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[10].Height = new GridLength(35);

                OpenWithPanel.Visibility = Visibility.Visible;

                Unlock.IsEnabled = Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64;

                if (StorageItem is IUnsupportedStorageItem)
                {
                    PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Tools_Tab")));
                }

                if (StorageItem is not (LinkStorageFile or UrlStorageFile))
                {
                    PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Shortcut_Tab")));
                }
            }

            Window.Closed += Window_Closed;
            Loading += PropertiesWindow_Loading;
            Loaded += PropertiesWindow_Loaded;
        }

        private void Window_Closed(AppWindow sender, AppWindowClosedEventArgs args)
        {
            Window.Closed -= Window_Closed;

            FolderCancellation?.Cancel();
            Md5Cancellation?.Cancel();
            SHA1Cancellation?.Cancel();
            SHA256Cancellation?.Cancel();

            LocationScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
            LocationScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
            LocationScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
            LocationScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);

            WindowClosed?.Invoke(this, new EventArgs());
        }

        private async Task SaveConfiguration()
        {
            List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>> AttributeDic = new List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>>(2);

            switch (StorageItem)
            {
                case FileSystemStorageFolder:
                    {
                        if (ReadonlyAttribute.IsChecked != null)
                        {
                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(ReadonlyAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.ReadOnly));
                        }

                        break;
                    }
                case FileSystemStorageFile File:
                    {
                        if (ReadonlyAttribute.IsChecked.GetValueOrDefault() != File.IsReadOnly)
                        {
                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(File.IsReadOnly ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.ReadOnly));
                        }

                        break;
                    }
            }

            if (HiddenAttribute.IsChecked.GetValueOrDefault() != (StorageItem is IHiddenStorageItem))
            {
                AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(StorageItem is IHiddenStorageItem ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.Hidden));
            }

            Task ShowLoadingTask = Task.Delay(2000).ContinueWith((_) =>
            {
                LoadingControl.IsLoading = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                await Exclusive.Controller.SetFileAttributeAsync(StorageItem.Path, AttributeDic.ToArray());

                switch (StorageItem)
                {
                    case LinkStorageFile:
                        {
                            string[] TargetSplit = ShortcutTargetContent.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                            await Exclusive.Controller.UpdateLinkAsync(new LinkDataPackage
                            {
                                LinkPath = StorageItem.Path,
                                LinkTargetPath = TargetSplit.FirstOrDefault(),
                                Arguments = TargetSplit.Skip(1).ToArray(),
                                WorkDirectory = ShortcutStartInContent.Text,
                                WindowState = (WindowState)ShortcutWindowsStateContent.SelectedIndex,
                                HotKey = ShortcutKeyContent.Text == Globalization.GetString("ShortcutHotKey_None") ? (int)VirtualKey.None : (int)Enum.Parse<VirtualKey>(ShortcutKeyContent.Text.Replace("Ctrl + Alt + ", string.Empty)),
                                Comment = ShortcutCommentContent.Text,
                                NeedRunAsAdmin = RunAsAdmin.IsChecked.GetValueOrDefault()
                            });

                            break;
                        }
                    case UrlStorageFile:
                        {
                            await Exclusive.Controller.UpdateUrlAsync(new UrlDataPackage
                            {
                                UrlPath = StorageItem.Path,
                                UrlTargetPath = ShortcutUrlContent.Text
                            });

                            break;
                        }
                }
            }

            if (StorageItemName.Text != StorageItem.Name)
            {
                if (HandleRenameAutomatically)
                {
                    await StorageItem.RenameAsync(StorageItemName.Text);
                }
                else
                {
                    await RenameRequested?.InvokeAsync(this, new FileRenamedDeferredEventArgs(StorageItem.Path, StorageItemName.Text));
                }
            }
        }

        private void PropertiesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Window.RequestSize(WindowSize);
        }

        private async void PropertiesWindow_Loading(FrameworkElement sender, object args)
        {
            switch (StorageItem)
            {
                case FileSystemStorageFolder:
                    {
                        await LoadDataForGeneralPage();
                        break;
                    }
                case FileSystemStorageFile:
                    {
                        await LoadDataForGeneralPage();
                        await LoadDataForDetailPage();

                        if (StorageItem is LinkStorageFile or UrlStorageFile)
                        {
                            await LoadDataForShortCutPage();
                        }

                        break;
                    }
            }
        }

        private async Task LoadDataForShortCutPage()
        {
            switch (StorageItem)
            {
                case LinkStorageFile LinkFile:
                    {
                        UrlArea.Visibility = Visibility.Collapsed;
                        LinkArea.Visibility = Visibility.Visible;

                        ShortcutThumbnail.Source = LinkFile.Thumbnail;
                        ShortcutItemName.Text = Path.GetFileNameWithoutExtension(LinkFile.Name);
                        ShortcutCommentContent.Text = LinkFile.Comment;
                        ShortcutWindowsStateContent.SelectedIndex = (int)LinkFile.WindowState;

                        if (LinkFile.HotKey > 0)
                        {
                            if (LinkFile.HotKey >= 112 && LinkFile.HotKey <= 135)
                            {
                                ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), (VirtualKey)LinkFile.HotKey) ?? Globalization.GetString("ShortcutHotKey_None");
                            }
                            else
                            {
                                ShortcutKeyContent.Text = "Ctrl + Alt + " + Enum.GetName(typeof(VirtualKey), (VirtualKey)(LinkFile.HotKey - 393216)) ?? Globalization.GetString("ShortcutHotKey_None");
                            }
                        }
                        else
                        {
                            ShortcutKeyContent.Text = Globalization.GetString("ShortcutHotKey_None");
                        }

                        if (LinkFile.LinkType == ShellLinkType.Normal)
                        {
                            ShortcutTargetLocationContent.Text = Path.GetFileName(Path.GetDirectoryName(LinkFile.LinkTargetPath));
                            ShortcutTargetContent.Text = $"{LinkFile.LinkTargetPath} {string.Join(" ", LinkFile.Arguments)}";
                            ShortcutStartInContent.Text = LinkFile.WorkDirectory;
                            RunAsAdmin.IsChecked = LinkFile.NeedRunAsAdmin;

                            if (await FileSystemStorageItemBase.OpenAsync(LinkFile.LinkTargetPath) is FileSystemStorageItemBase TargetItem)
                            {
                                switch (await TargetItem.GetStorageItemAsync())
                                {
                                    case StorageFile File:
                                        {
                                            ShortcutTargetTypeContent.Text = File.DisplayType;
                                            break;
                                        }
                                    case StorageFolder Folder:
                                        {
                                            ShortcutTargetTypeContent.Text = Folder.DisplayType;
                                            break;
                                        }
                                    default:
                                        {
                                            ShortcutTargetTypeContent.Text = TargetItem.DisplayType;
                                            break;
                                        }
                                }
                            }
                        }
                        else
                        {
                            ShortcutTargetTypeContent.Text = LinkFile.LinkTargetPath;
                            ShortcutTargetLocationContent.Text = Globalization.GetString("ShortcutTargetApplicationType");
                            ShortcutTargetContent.Text = LinkFile.LinkTargetPath;
                            ShortcutTargetContent.IsEnabled = false;
                            ShortcutStartInContent.IsEnabled = false;
                            OpenLocation.IsEnabled = false;
                            RunAsAdmin.IsEnabled = false;
                        }

                        break;
                    }

                case UrlStorageFile UrlFile:
                    {
                        UrlArea.Visibility = Visibility.Visible;
                        LinkArea.Visibility = Visibility.Collapsed;

                        ShortcutThumbnail.Source = UrlFile.Thumbnail;
                        ShortcutItemName.Text = Path.GetFileNameWithoutExtension(UrlFile.Name);
                        ShortcutUrlContent.Text = UrlFile.UrlTargetPath;
                        break;
                    }
            }
        }

        private async Task LoadDataForDetailPage()
        {
            await StorageItem.StartProcessRefShareRegionAsync();

            try
            {
                Dictionary<string, object> BasicPropertiesDictionary = new Dictionary<string, object>(10)
                {
                    { Globalization.GetString("Properties_Details_Name"), StorageItem.Name },
                    { Globalization.GetString("Properties_Details_ItemType"), StorageItem.DisplayType },
                    { Globalization.GetString("Properties_Details_FolderPath"), Path.GetDirectoryName(StorageItem.Path) },
                    { Globalization.GetString("Properties_Details_Size"), StorageItem.SizeDescription },
                    { Globalization.GetString("Properties_Details_DateCreated"), StorageItem.CreationTimeDescription },
                    { Globalization.GetString("Properties_Details_DateModified"), StorageItem.ModifiedTimeDescription },
                    { Globalization.GetString("Properties_Details_Availability"), string.Empty },
                    { Globalization.GetString("Properties_Details_OfflineAvailabilityStatus"), string.Empty },
                    { Globalization.GetString("Properties_Details_Owner"), string.Empty },
                    { Globalization.GetString("Properties_Details_ComputerName"), string.Empty }
                };

                IReadOnlyDictionary<string, string> BasicPropertiesResult = await StorageItem.GetPropertiesAsync(new string[]
                {
                "System.OfflineAvailability",
                "System.FileOfflineAvailabilityStatus",
                "System.FileOwner",
                "System.ComputerName",
                "System.FilePlaceholderStatus"
                });

                if (!string.IsNullOrEmpty(BasicPropertiesResult["System.OfflineAvailability"]))
                {
                    BasicPropertiesDictionary[Globalization.GetString("Properties_Details_Availability")] = OfflineAvailabilityMap[Convert.ToUInt32(BasicPropertiesResult["System.OfflineAvailability"])];
                }

                if (!string.IsNullOrEmpty(BasicPropertiesResult["System.FileOfflineAvailabilityStatus"]))
                {
                    BasicPropertiesDictionary[Globalization.GetString("Properties_Details_OfflineAvailabilityStatus")] = OfflineAvailabilityStatusMap[Convert.ToUInt32(BasicPropertiesResult["System.FileOfflineAvailabilityStatus"])];
                }
                else if (!string.IsNullOrEmpty(BasicPropertiesResult["System.FilePlaceholderStatus"]))
                {
                    BasicPropertiesDictionary[Globalization.GetString("Properties_Details_OfflineAvailabilityStatus")] = OfflineAvailabilityStatusMap[Convert.ToUInt32(BasicPropertiesResult["System.FilePlaceholderStatus"])];
                }

                if (!string.IsNullOrEmpty(BasicPropertiesResult["System.FileOwner"]))
                {
                    BasicPropertiesDictionary[Globalization.GetString("Properties_Details_Owner")] = BasicPropertiesResult["System.FileOwner"];
                }

                if (!string.IsNullOrEmpty(BasicPropertiesResult["System.ComputerName"]))
                {
                    BasicPropertiesDictionary[Globalization.GetString("Properties_Details_ComputerName")] = BasicPropertiesResult["System.ComputerName"];
                }

                PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Basic_Label"), BasicPropertiesDictionary.ToArray()));

                string ContentType = string.Empty;

                if (string.IsNullOrEmpty(ContentType))
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        ContentType = await Exclusive.Controller.GetMIMEContentTypeAsync(StorageItem.Path);
                    }
                }

                if (ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                {
                    IReadOnlyDictionary<string, string> PropertiesResult = await StorageItem.GetPropertiesAsync(new string[]
                    {
                    "System.Video.FrameWidth",
                    "System.Video.FrameHeight",
                    "System.Media.Duration",
                    "System.Video.FrameRate",
                    "System.Video.TotalBitrate",
                    "System.Audio.EncodingBitrate",
                    "System.Audio.SampleRate",
                    "System.Audio.ChannelCount",
                    "System.Title",
                    "System.Media.SubTitle",
                    "System.Rating",
                    "System.Comment",
                    "System.Media.Year",
                    "System.Video.Director",
                    "System.Media.Producer",
                    "System.Media.Publisher",
                    "System.Keywords",
                    "System.Copyright"
                    });

                    Dictionary<string, object> VideoPropertiesDictionary = new Dictionary<string, object>(5)
                    {
                        { Globalization.GetString("Properties_Details_Duration"), string.Empty },
                        { Globalization.GetString("Properties_Details_FrameWidth"), PropertiesResult["System.Video.FrameWidth"] },
                        { Globalization.GetString("Properties_Details_FrameHeight"), PropertiesResult["System.Video.FrameHeight"] },
                        { Globalization.GetString("Properties_Details_Bitrate"), string.Empty },
                        { Globalization.GetString("Properties_Details_FrameRate"), string.Empty }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Video.FrameRate"]))
                    {
                        uint FrameRate = Convert.ToUInt32(PropertiesResult["System.Video.FrameRate"]);
                        VideoPropertiesDictionary[Globalization.GetString("Properties_Details_FrameRate")] = $"{ FrameRate / 1000:N2} {Globalization.GetString("Properties_Details_FrameRatePerSecond")}";
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Video.TotalBitrate"]))
                    {
                        uint Bitrate = Convert.ToUInt32(PropertiesResult["System.Video.TotalBitrate"]);
                        VideoPropertiesDictionary[Globalization.GetString("Properties_Details_Bitrate")] = Bitrate / 1024f < 1024 ? $"{Math.Round(Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(Bitrate / 1048576f, 2):N2} Mbps";
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Media.Duration"]))
                    {
                        VideoPropertiesDictionary[Globalization.GetString("Properties_Details_Duration")] = TimeSpan.FromMilliseconds(Convert.ToUInt64(PropertiesResult["System.Media.Duration"]) / 10000).ConvertTimeSpanToString();
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Video_Label"), VideoPropertiesDictionary.ToArray()));

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>(3)
                    {
                        { Globalization.GetString("Properties_Details_Bitrate"), string.Empty },
                        { Globalization.GetString("Properties_Details_Channels"), PropertiesResult["System.Audio.ChannelCount"] },
                        { Globalization.GetString("Properties_Details_SampleRate"), string.Empty }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Audio.EncodingBitrate"]))
                    {
                        uint Bitrate = Convert.ToUInt32(PropertiesResult["System.Audio.EncodingBitrate"]);
                        AudioPropertiesDictionary[Globalization.GetString("Properties_Details_Bitrate")] = Bitrate / 1024f < 1024 ? $"{Math.Round(Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(Bitrate / 1048576f, 2):N2} Mbps";
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Audio.SampleRate"]))
                    {
                        uint SampleRate = Convert.ToUInt32(PropertiesResult["System.Audio.SampleRate"]);
                        AudioPropertiesDictionary[Globalization.GetString("Properties_Details_SampleRate")] = $"{SampleRate / 1000:N3} kHz";
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Audio_Label"), AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), PropertiesResult["System.Title"] },
                        { Globalization.GetString("Properties_Details_Subtitle"), PropertiesResult["System.Media.SubTitle"] },
                        { Globalization.GetString("Properties_Details_Rating"), PropertiesResult["System.Rating"] },
                        { Globalization.GetString("Properties_Details_Comment"), PropertiesResult["System.Comment"] }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(6)
                    {
                        { Globalization.GetString("Properties_Details_Year"), PropertiesResult["System.Media.Year"] },
                        { Globalization.GetString("Properties_Details_Directors"), PropertiesResult["System.Video.Director"] },
                        { Globalization.GetString("Properties_Details_Producers"), PropertiesResult["System.Media.Producer"] },
                        { Globalization.GetString("Properties_Details_Publisher"), PropertiesResult["System.Media.Publisher"] },
                        { Globalization.GetString("Properties_Details_Keywords"), PropertiesResult["System.Keywords"] },
                        { Globalization.GetString("Properties_Details_Copyright"), PropertiesResult["System.Copyright"] }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
                {
                    IReadOnlyDictionary<string, string> PropertiesResult = await StorageItem.GetPropertiesAsync(new string[]
                    {
                    "System.Media.Duration",
                    "System.Audio.SampleRate",
                    "System.Audio.ChannelCount",
                    "System.Audio.EncodingBitrate",
                    "System.Title",
                    "System.Media.SubTitle",
                    "System.Rating",
                    "System.Comment",
                    "System.Media.Year",
                    "System.Music.Genre",
                    "System.Music.Artist",
                    "System.Music.AlbumArtist",
                    "System.Media.Producer",
                    "System.Media.Publisher",
                    "System.Music.Conductor",
                    "System.Music.Composer",
                    "System.Music.TrackNumber",
                    "System.Copyright"
                    });

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Bitrate"), string.Empty },
                        { Globalization.GetString("Properties_Details_Duration"), string.Empty },
                        { Globalization.GetString("Properties_Details_Channels"), PropertiesResult["System.Audio.ChannelCount"] },
                        { Globalization.GetString("Properties_Details_SampleRate"), string.Empty }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Audio.EncodingBitrate"]))
                    {
                        uint Bitrate = Convert.ToUInt32(PropertiesResult["System.Audio.EncodingBitrate"]);
                        AudioPropertiesDictionary[Globalization.GetString("Properties_Details_Bitrate")] = Bitrate / 1024f < 1024 ? $"{Math.Round(Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(Bitrate / 1048576f, 2):N2} Mbps";
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Media.Duration"]))
                    {
                        AudioPropertiesDictionary[Globalization.GetString("Properties_Details_Duration")] = TimeSpan.FromMilliseconds(Convert.ToUInt64(PropertiesResult["System.Media.Duration"]) / 10000).ConvertTimeSpanToString();
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Audio.SampleRate"]))
                    {
                        uint SampleRate = Convert.ToUInt32(PropertiesResult["System.Audio.SampleRate"]);
                        AudioPropertiesDictionary[Globalization.GetString("Properties_Details_SampleRate")] = $"{SampleRate / 1000:N3} kHz";
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Audio_Label"), AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), PropertiesResult["System.Title"] },
                        { Globalization.GetString("Properties_Details_Subtitle"), PropertiesResult["System.Media.SubTitle"] },
                        { Globalization.GetString("Properties_Details_Rating"), PropertiesResult["System.Rating"] },
                        { Globalization.GetString("Properties_Details_Comment"), PropertiesResult["System.Comment"] }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(10)
                    {
                        { Globalization.GetString("Properties_Details_Year"), PropertiesResult["System.Media.Year"] },
                        { Globalization.GetString("Properties_Details_Genre"), PropertiesResult["System.Music.Genre"] },
                        { Globalization.GetString("Properties_Details_Artist"), PropertiesResult["System.Music.Artist"] },
                        { Globalization.GetString("Properties_Details_AlbumArtist"), PropertiesResult["System.Music.AlbumArtist"] },
                        { Globalization.GetString("Properties_Details_Producers"), PropertiesResult["System.Media.Producer"] },
                        { Globalization.GetString("Properties_Details_Publisher"), PropertiesResult["System.Media.Publisher"] },
                        { Globalization.GetString("Properties_Details_Conductors"), PropertiesResult["System.Music.Conductor"] },
                        { Globalization.GetString("Properties_Details_Composers"), PropertiesResult["System.Music.Composer"] },
                        { Globalization.GetString("Properties_Details_TrackNum"), PropertiesResult["System.Music.TrackNumber"] },
                        { Globalization.GetString("Properties_Details_Copyright"), PropertiesResult["System.Copyright"] }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    IReadOnlyDictionary<string, string> PropertiesResult = await StorageItem.GetPropertiesAsync(new string[]
                    {
                    "System.Image.Dimensions",
                    "System.Image.HorizontalSize",
                    "System.Image.VerticalSize",
                    "System.Image.BitDepth",
                    "System.Image.ColorSpace",
                    "System.Title",
                    "System.Photo.DateTaken",
                    "System.Rating",
                    "System.Photo.CameraModel",
                    "System.Photo.CameraManufacturer",
                    "System.Keywords",
                    "System.Comment",
                    "System.GPS.LatitudeDecimal",
                    "System.GPS.LongitudeDecimal",
                    "System.Photo.PeopleNames"
                    });

                    Dictionary<string, object> ImagePropertiesDictionary = new Dictionary<string, object>(5)
                    {
                        { Globalization.GetString("Properties_Details_Dimensions"), PropertiesResult["System.Image.Dimensions"] },
                        { Globalization.GetString("Properties_Details_Width"), PropertiesResult["System.Image.HorizontalSize"] },
                        { Globalization.GetString("Properties_Details_Height"), PropertiesResult["System.Image.VerticalSize"] },
                        { Globalization.GetString("Properties_Details_BitDepth"), PropertiesResult["System.Image.BitDepth"]},
                        { Globalization.GetString("Properties_Details_ColorSpace"), string.Empty }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Image.ColorSpace"]))
                    {
                        ushort ColorSpaceEnum = Convert.ToUInt16(PropertiesResult["System.Image.ColorSpace"]);

                        if (ColorSpaceEnum == 1)
                        {
                            ImagePropertiesDictionary[Globalization.GetString("Properties_Details_ColorSpace")] = Globalization.GetString("Properties_Details_ColorSpace_SRGB");
                        }
                        else if (ColorSpaceEnum == ushort.MaxValue)
                        {
                            ImagePropertiesDictionary[Globalization.GetString("Properties_Details_ColorSpace")] = Globalization.GetString("Properties_Details_ColorSpace_Uncalibrated");
                        }
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Image_Label"), ImagePropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), PropertiesResult["System.Title"] },
                        { Globalization.GetString("Properties_Details_DateTaken"), string.Empty },
                        { Globalization.GetString("Properties_Details_Rating"), PropertiesResult["System.Rating"] },
                        { Globalization.GetString("Properties_Details_Comment"), PropertiesResult["System.Comment"] }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Photo.DateTaken"]))
                    {
                        DescriptionPropertiesDictionary[Globalization.GetString("Properties_Details_DateTaken")] = DateTimeOffset.Parse(PropertiesResult["System.Photo.DateTaken"]).ToString("G");
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(6)
                    {
                        { Globalization.GetString("Properties_Details_CameraModel"), PropertiesResult["System.Photo.CameraModel"] },
                        { Globalization.GetString("Properties_Details_CameraManufacturer"), PropertiesResult["System.Photo.CameraManufacturer"] },
                        { Globalization.GetString("Properties_Details_Keywords"), PropertiesResult["System.Keywords"] },
                        { Globalization.GetString("Properties_Details_Latitude"), PropertiesResult["System.GPS.LatitudeDecimal"] },
                        { Globalization.GetString("Properties_Details_Longitude"), PropertiesResult["System.GPS.LongitudeDecimal"] },
                        { Globalization.GetString("Properties_Details_PeopleNames"), PropertiesResult["System.Photo.PeopleNames"] }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("application/msword", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.ms-powerpoint", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.openxmlformats-officedocument", StringComparison.OrdinalIgnoreCase))
                {
                    IReadOnlyDictionary<string, string> PropertiesResult = await StorageItem.GetPropertiesAsync(new string[]
                    {
                    "System.Title",
                    "System.Comment",
                    "System.Keywords",
                    "System.Author",
                    "System.Document.LastAuthor",
                    "System.Document.Version",
                    "System.Document.RevisionNumber",
                    "System.Document.Template",
                    "System.Document.PageCount",
                    "System.Document.WordCount",
                    "System.Document.CharacterCount",
                    "System.Document.LineCount",
                    "System.Document.TotalEditingTime",
                    "System.Document.DateCreated",
                    "System.Document.DateSaved"
                    });

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), PropertiesResult["System.Title"] },
                        { Globalization.GetString("Properties_Details_Comment"), PropertiesResult["System.Comment"] },
                        { Globalization.GetString("Properties_Details_Keywords"), PropertiesResult["System.Keywords"] },
                        { Globalization.GetString("Properties_Details_Authors"), PropertiesResult["System.Author"] },
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, string> ExtraPropertiesDictionary = new Dictionary<string, string>(11)
                    {
                        { Globalization.GetString("Properties_Details_LastAuthor"), PropertiesResult["System.Document.LastAuthor"] },
                        { Globalization.GetString("Properties_Details_Version"), PropertiesResult["System.Document.Version"] },
                        { Globalization.GetString("Properties_Details_RevisionNumber"), PropertiesResult["System.Document.RevisionNumber"] },
                        { Globalization.GetString("Properties_Details_PageCount"), PropertiesResult["System.Document.PageCount"] },
                        { Globalization.GetString("Properties_Details_WordCount"), PropertiesResult["System.Document.WordCount"] },
                        { Globalization.GetString("Properties_Details_CharacterCount"), PropertiesResult["System.Document.CharacterCount"] },
                        { Globalization.GetString("Properties_Details_LineCount"), PropertiesResult["System.Document.LineCount"] },
                        { Globalization.GetString("Properties_Details_Template"), PropertiesResult["System.Document.Template"] },
                        { Globalization.GetString("Properties_Details_TotalEditingTime"), string.Empty },
                        { Globalization.GetString("Properties_Details_ContentCreated"), string.Empty },
                        { Globalization.GetString("Properties_Details_DateLastSaved"), string.Empty }
                    };

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Document.TotalEditingTime"]))
                    {
                        ulong TotalEditing = Convert.ToUInt64(PropertiesResult["System.Document.TotalEditingTime"]);
                        ExtraPropertiesDictionary[Globalization.GetString("Properties_Details_TotalEditingTime")] = TimeSpan.FromMilliseconds(TotalEditing / 10000).ConvertTimeSpanToString();
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Document.DateCreated"]))
                    {
                        ExtraPropertiesDictionary[Globalization.GetString("Properties_Details_ContentCreated")] = DateTimeOffset.Parse(PropertiesResult["System.Document.DateCreated"]).ToString("G");
                    }

                    if (!string.IsNullOrEmpty(PropertiesResult["System.Document.DateSaved"]))
                    {
                        ExtraPropertiesDictionary[Globalization.GetString("Properties_Details_DateLastSaved")] = DateTimeOffset.Parse(PropertiesResult["System.Document.DateSaved"]).ToString("G");
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.Select((Pair) => new KeyValuePair<string, object>(Pair.Key, Pair.Value))));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the details in property window");
            }
            finally
            {
                await StorageItem.EndProcessRefShareRegionAsync();
            }
        }

        private async Task LoadDataForGeneralPage()
        {
            Thumbnail.Source = StorageItem.Thumbnail;
            LocationContent.Text = Path.GetDirectoryName(StorageItem.Path) ?? StorageItem.Path;
            CreatedContent.Text = StorageItem.CreationTime.ToString("F");
            ModifiedContent.Text = StorageItem.ModifiedTime.ToString("F");
            HiddenAttribute.IsChecked = StorageItem is IHiddenStorageItem;

            if (StorageItem is FileSystemStorageFolder Folder)
            {
                StorageItemName.Text = Folder.Name;
                TypeContent.Text = Folder.DisplayType;
                SizeContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                ContainsContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");

                ReadonlyAttribute.IsThreeState = true;
                ReadonlyAttribute.IsChecked = null;

                try
                {
                    FolderCancellation = new CancellationTokenSource();

                    Task CountTask = CalculateFolderAndFileCount(Folder, FolderCancellation.Token).ContinueWith((task) =>
                    {
                        ContainsContent.Text = task.Result;
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    Task SizeTask = CalculateFolderSize(Folder, FolderCancellation.Token).ContinueWith((task) =>
                    {
                        SizeContent.Text = task.Result;
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    await Task.WhenAll(CountTask, SizeTask);
                }
                catch (TaskCanceledException)
                {
                    LogTracer.Log($"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} have been canceled");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} threw an exception");
                }
                finally
                {
                    FolderCancellation.Dispose();
                    FolderCancellation = null;
                }
            }
            else if (StorageItem is FileSystemStorageFile File)
            {
                StorageItemName.Text = File.Name;
                ReadonlyAttribute.IsChecked = File.IsReadOnly;
                SizeContent.Text = $"{StorageItem.SizeDescription} ({StorageItem.Size:N0} {Globalization.GetString("Device_Capacity_Unit")})";

                using (SafeFileHandle Handle = await File.GetNativeHandleAsync(AccessMode.Read))
                {
                    if (Handle.IsInvalid)
                    {
                        SizeOnDiskContent.Text = Globalization.GetString("UnknownText");
                    }
                    else
                    {
                        ulong Size = Win32_Native_API.GetFileSpaceSize(Handle.DangerousGetHandle());
                        SizeOnDiskContent.Text = $"{Size.GetFileSizeDescription()} ({Size:N0} {Globalization.GetString("Device_Capacity_Unit")})";
                    }
                }

                bool IsDisplayTypeEmpty = string.IsNullOrEmpty(File.DisplayType);
                bool IsTypeEmpty = string.IsNullOrEmpty(File.Type);

                if (IsDisplayTypeEmpty && IsTypeEmpty)
                {
                    TypeContent.Text = Globalization.GetString("UnknownText");
                }
                else if (IsDisplayTypeEmpty && !IsTypeEmpty)
                {
                    TypeContent.Text = File.Type.ToUpper();
                }
                else if (!IsDisplayTypeEmpty && IsTypeEmpty)
                {
                    TypeContent.Text = File.DisplayType;
                }
                else
                {
                    TypeContent.Text = $"{File.DisplayType} ({File.Type.ToLower()})";
                }

                string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath == Package.Current.Id.FamilyName)
                {
                    switch (File.Type.ToLower())
                    {
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                        case ".mkv":
                        case ".mp4":
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".wmv":
                        case ".m4a":
                        case ".mov":
                        case ".txt":
                        case ".pdf":
                            {
                                OpenWithContent.Text = Globalization.GetString("AppDisplayName");

                                try
                                {
                                    RandomAccessStreamReference Reference = Package.Current.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                                    using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                                    {
                                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                                        using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                        using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                        using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                                        {
                                            BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                            Encoder.SetSoftwareBitmap(ResizeBitmap);
                                            await Encoder.FlushAsync();

                                            BitmapImage Image = new BitmapImage();
                                            OpenWithImage.Source = Image;
                                            await Image.SetSourceAsync(Stream);
                                        }
                                    }
                                }
                                catch
                                {
                                    OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                                }

                                break;
                            }
                        default:
                            {
                                try
                                {
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                    {
                                        AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(File.Path);
                                    }

                                    if (await FileSystemStorageItemBase.OpenAsync(AdminExecutablePath) is FileSystemStorageFile OpenWithFile)
                                    {
                                        OpenWithImage.Source = await OpenWithFile.GetThumbnailAsync(ThumbnailMode.SingleItem);
                                        IReadOnlyDictionary<string, string> PropertiesDic = await OpenWithFile.GetPropertiesAsync(new string[] { "System.FileDescription" });

                                        string AppName = PropertiesDic["System.FileDescription"];

                                        if (string.IsNullOrEmpty(AppName))
                                        {
                                            OpenWithContent.Text = OpenWithFile.DisplayName;
                                        }
                                        else
                                        {
                                            OpenWithContent.Text = AppName;
                                        }
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException();
                                    }
                                }
                                catch
                                {
                                    OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                    OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                                }

                                break;
                            }
                    }
                }
                else if (Path.IsPathRooted(AdminExecutablePath))
                {
                    try
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(AdminExecutablePath) is FileSystemStorageFile OpenWithFile)
                        {
                            OpenWithImage.Source = await OpenWithFile.GetThumbnailAsync(ThumbnailMode.SingleItem);
                            IReadOnlyDictionary<string, string> PropertiesDic = await OpenWithFile.GetPropertiesAsync(new string[] { "System.FileDescription" });

                            string AppName = PropertiesDic["System.FileDescription"];

                            if (string.IsNullOrEmpty(AppName))
                            {
                                OpenWithContent.Text = OpenWithFile.DisplayName;
                            }
                            else
                            {
                                OpenWithContent.Text = AppName;
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }
                    catch
                    {
                        OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                    }
                }
                else
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(StorageItem.Type) && (await Launcher.FindFileHandlersAsync(StorageItem.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                        {
                            OpenWithContent.Text = Info.Package.DisplayName;

                            RandomAccessStreamReference Reference = Info.Package.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                            using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                                using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                                {
                                    BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                    Encoder.SetSoftwareBitmap(ResizeBitmap);
                                    await Encoder.FlushAsync();

                                    BitmapImage Image = new BitmapImage();
                                    OpenWithImage.Source = Image;
                                    await Image.SetSourceAsync(Stream);
                                }
                            }

                        }
                        else
                        {
                            OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                            OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                        }
                    }
                    catch
                    {
                        OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                    }
                }
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref ConfirmButtonLockResource, 1) == 0)
            {
                try
                {
                    await SaveConfiguration();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not save configuration");
                }
                finally
                {
                    try
                    {
                        await Window.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not close the window");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref ConfirmButtonLockResource, 0);
                    }
                }
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref ConfirmButtonLockResource, 1) == 0)
            {
                try
                {
                    await Window.CloseAsync();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref ConfirmButtonLockResource, 0);
                }
            }
        }

        private async Task<string> CalculateFolderSize(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            ulong TotalSize = await Folder.GetFolderSizeAsync(CancelToken).ConfigureAwait(false);

            if (CancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderSize)} was canceled");
            }
            else
            {
                return $"{TotalSize.GetFileSizeDescription()} ({TotalSize:N0} {Globalization.GetString("Device_Capacity_Unit")})";
            }
        }

        private async Task<string> CalculateFolderAndFileCount(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            IReadOnlyList<FileSystemStorageItemBase> Result = await Folder.GetChildItemsAsync(true, true, true, CancelToken: CancelToken);

            if (CancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderAndFileCount)} was canceled");
            }
            else
            {
                return $"{Result.OfType<FileSystemStorageFile>().Count()} {Globalization.GetString("FolderInfo_File_Count")} , {Result.OfType<FileSystemStorageFolder>().Count()} {Globalization.GetString("FolderInfo_Folder_Count")}";
            }
        }

        private void ShortcutKeyContent_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Back:
                    {
                        ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        break;
                    }
                case VirtualKey.Shift:
                case VirtualKey.Control:
                case VirtualKey.CapitalLock:
                case VirtualKey.Menu:
                case VirtualKey.Space:
                case VirtualKey.Tab:
                    {
                        break;
                    }
                default:
                    {
                        string KeyName = Enum.GetName(typeof(VirtualKey), e.Key);

                        if (string.IsNullOrEmpty(KeyName))
                        {
                            ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        }
                        else
                        {
                            if ((e.Key >= VirtualKey.F1 && e.Key <= VirtualKey.F24) || (e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9))
                            {
                                ShortcutKeyContent.Text = KeyName;
                            }
                            else
                            {
                                ShortcutKeyContent.Text = $"Ctrl + Alt + {KeyName}";
                            }
                        }

                        break;
                    }
            }
        }

        private async void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is LinkStorageFile Link)
            {
                await TabViewContainer.Current.CreateNewTabAsync(new string[] { Path.GetDirectoryName(Link.LinkTargetPath) });

                if (TabViewContainer.Current.TabCollection.LastOrDefault()?.Tag is FileControl Control)
                {
                    do
                    {
                        await Task.Delay(500);
                    }
                    while (Control.CurrentPresenter?.CurrentFolder == null);

                    await Task.Delay(500);

                    if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem.Path == Link.LinkTargetPath) is FileSystemStorageItemBase Target)
                    {
                        Control.CurrentPresenter.SelectedItem = Target;
                        Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                    }
                }
            }
        }

        private async void CalculateMd5_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.CheckExistAsync(StorageItem.Path))
            {
                if (StorageItem is FileSystemStorageFile File)
                {
                    CalculateMd5.IsEnabled = false;
                    MD5TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                    Md5Cancellation = new CancellationTokenSource();

                    try
                    {
                        using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read))
                        using (MD5 MD5Alg = MD5.Create())
                        {
                            await MD5Alg.GetHashAsync(Stream, Md5Cancellation.Token).ContinueWith((beforeTask) =>
                            {
                                MD5TextBox.Text = beforeTask.Result;
                                MD5TextBox.IsEnabled = true;
                            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Calculate MD5 failed");

                        MD5TextBox.Text = Globalization.GetString("HashError");
                        CalculateMd5.IsEnabled = true;
                    }
                    finally
                    {
                        Md5Cancellation?.Dispose();
                        Md5Cancellation = null;
                    }
                }
            }
        }

        private async void CalculateSHA1_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                CalculateSHA1.IsEnabled = false;
                SHA1TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                SHA1Cancellation = new CancellationTokenSource();

                try
                {
                    using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read))
                    using (SHA1 SHA1Alg = SHA1.Create())
                    {
                        await SHA1Alg.GetHashAsync(Stream, SHA1Cancellation.Token).ContinueWith((beforeTask) =>
                        {
                            SHA1TextBox.Text = beforeTask.Result;
                            SHA1TextBox.IsEnabled = true;
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Calculate SHA1 failed");

                    SHA1TextBox.Text = Globalization.GetString("HashError");
                    CalculateSHA1.IsEnabled = true;
                }
                finally
                {
                    SHA1Cancellation?.Dispose();
                    SHA1Cancellation = null;
                }
            }
        }

        private async void CalculateSHA256_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                CalculateSHA256.IsEnabled = false;
                SHA256TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                SHA256Cancellation = new CancellationTokenSource();

                try
                {
                    using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read))
                    using (SHA256 SHA256Alg = SHA256.Create())
                    {
                        await SHA256Alg.GetHashAsync(Stream, SHA256Cancellation.Token).ContinueWith((beforeTask) =>
                        {
                            SHA256TextBox.Text = beforeTask.Result;
                            SHA256TextBox.IsEnabled = true;
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Calculate SHA256 failed");

                    SHA256TextBox.Text = Globalization.GetString("HashError");
                    CalculateSHA256.IsEnabled = true;
                }
                finally
                {
                    SHA256Cancellation?.Dispose();
                    SHA256Cancellation = null;
                }
            }
        }

        private async void ChangeOpenWithButton_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    ProgramPickerDialog Dialog = new ProgramPickerDialog(File, true);
                    await Dialog.ShowAsync();
                });

                await Window.CloseAsync();
            }
        }

        private async void Unlock_Click(object sender, RoutedEventArgs e)
        {
            UnlockFlyout.Hide();

            if (StorageItem is FileSystemStorageFile File)
            {
                try
                {
                    UnlockProgressRing.Visibility = Visibility.Visible;
                    UnlockText.Visibility = Visibility.Collapsed;

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (await Exclusive.Controller.TryUnlockFileOccupy(File.Path, ((Button)sender).Name == "CloseForce"))
                        {
                            UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_Success_Content");
                        }
                        else
                        {
                            UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_Failure_Content");
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_FileNotFound_Content");
                }
                catch (UnlockException)
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_NoLock_Content");
                }
                catch
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_UnexpectedError_Content");
                }
                finally
                {
                    UnlockProgressRing.Visibility = Visibility.Collapsed;
                    UnlockText.Visibility = Visibility.Visible;
                }
            }
        }

        private void LocationScrollViewer_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
            {
                PointerPoint Pointer = e.GetCurrentPoint(LocationScrollViewer);

                if (Pointer.Properties.IsLeftButtonPressed)
                {
                    double XOffset = Pointer.Position.X;
                    double HorizontalRightScrollThreshold = LocationScrollViewer.ActualWidth - 30;
                    double HorizontalLeftScrollThreshold = 30;

                    if (XOffset > HorizontalRightScrollThreshold)
                    {
                        LocationScrollViewer.ChangeView(LocationScrollViewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null);
                    }
                    else if (XOffset < HorizontalLeftScrollThreshold)
                    {
                        LocationScrollViewer.ChangeView(LocationScrollViewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null);
                    }
                }
            }
        }

        private void LocationScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint Pointer = e.GetCurrentPoint(LocationScrollViewer);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && Pointer.Properties.IsLeftButtonPressed)
            {
                LocationScrollViewer.CapturePointer(e.Pointer);
            }
        }

        private void LocationScrollViewer_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if ((LocationScrollViewer.PointerCaptures?.Any()).GetValueOrDefault())
            {
                LocationScrollViewer.ReleasePointerCaptures();
            }
        }

        private void LocationScrollViewer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if ((LocationScrollViewer.PointerCaptures?.Any()).GetValueOrDefault())
            {
                LocationScrollViewer.ReleasePointerCaptures();
            }
        }
    }
}
