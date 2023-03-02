using ComputerVision;
using FluentFTP;
using Microsoft.Toolkit.Deferred;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.View;
using SharedLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.SeparateWindow.PropertyWindow
{
    public sealed partial class PropertiesWindowBase : Page
    {
        private bool IsClosed;

        private readonly AppWindow Window;
        private readonly FileSystemStorageItemBase[] StorageItems;
        private readonly DriveDataBase RootDrive;

        private readonly PointerEventHandler PointerPressedHandler;
        private readonly PointerEventHandler PointerReleasedHandler;
        private readonly PointerEventHandler PointerCanceledHandler;
        private readonly PointerEventHandler PointerMovedHandler;
        private readonly CancellationTokenSource SavingCancellation = new CancellationTokenSource();
        private readonly CancellationTokenSource OperationCancellation = new CancellationTokenSource();
        private readonly InterlockedNoReentryExecution ActionButtonExecution = new InterlockedNoReentryExecution();
        private readonly ObservableCollection<PropertiesGroupItem> PropertiesCollection = new ObservableCollection<PropertiesGroupItem>();

        public event EventHandler WindowClosed;
        public event EventHandler<FileRenamedDeferredEventArgs> RenameRequested;

        private static readonly Size DefaultWindowSize = new Size(420, 650);
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

        private static Size RequestWindowSize
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["PropertyWindowSizeConfiguration"] is string SizeConfigText)
                {
                    WindowSizeConfiguration SizeConfig = JsonSerializer.Deserialize<WindowSizeConfiguration>(SizeConfigText);
                    return new Size(SizeConfig.Width, SizeConfig.Height);
                }
                else
                {
                    return DefaultWindowSize;
                }
            }
        }

        /// <summary>
        /// If want to handle rename operation mannually. Please set this property to false and subscribe RenameRequested event
        /// </summary>
        public bool HandleRenameAutomatically { get; set; } = true;

        public static async Task<PropertiesWindowBase> CreateAsync(params FileSystemStorageItemBase[] StorageItems)
        {
            if (StorageItems.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(StorageItems));
            }

            await Task.WhenAll(StorageItems.Select((Item) => Item.LoadAsync().ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the storage item, StorageType: {Item.GetType().FullName}, Path: {Item.Path}");
                }
            })));

            AppWindow CoreWindow = await InitializeWindowsAsync();
            PropertiesWindowBase PropertiesWindow = new PropertiesWindowBase(CoreWindow, StorageItems);
            ElementCompositionPreview.SetAppWindowContent(CoreWindow, PropertiesWindow);

            return PropertiesWindow;
        }

        public static async Task<PropertiesWindowBase> CreateAsync(DriveDataBase Drive)
        {
            AppWindow CoreWindow = await InitializeWindowsAsync();
            PropertiesWindowBase PropertiesWindow = new PropertiesWindowBase(CoreWindow, Drive);
            ElementCompositionPreview.SetAppWindowContent(CoreWindow, PropertiesWindow);

            return PropertiesWindow;
        }

        private static async Task<AppWindow> InitializeWindowsAsync()
        {
            AppWindow NewWindow = await AppWindow.TryCreateAsync();
            NewWindow.PersistedStateId = "RX_Property_Window";
            NewWindow.Title = Globalization.GetString("Properties_Window_Title");
            NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
            NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            NewWindow.RequestSize(RequestWindowSize);

            WindowManagementPreview.SetPreferredMinSize(NewWindow, DefaultWindowSize);

            return NewWindow;
        }

        public async Task ShowAsync(Point ShowAt)
        {
            Window.RequestMoveRelativeToCurrentViewContent(ShowAt);

            if (await Window.TryShowAsync())
            {
                Window.RequestSize(RequestWindowSize);
            }
        }

        private PropertiesWindowBase(AppWindow Window)
        {
            InitializeComponent();

            this.Window = Window;

            GeneralTab.Text = Globalization.GetString("Properties_General_Tab");
            SecurityTab.Text = Globalization.GetString("Properties_Security_Tab");
            ShortcutTab.Text = Globalization.GetString("Properties_Shortcut_Tab");
            DetailsTab.Text = Globalization.GetString("Properties_Details_Tab");
            ToolsTab.Text = Globalization.GetString("Properties_Tools_Tab");

            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText1"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText2"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText3"));

            ShortcutWindowsStateContent.SelectedIndex = 0;

            Window.Closed += Window_Closed;
            Window.CloseRequested += Window_CloseRequested;

            Loading += PropertiesWindow_Loading;
        }

        private void Window_CloseRequested(AppWindow sender, AppWindowCloseRequestedEventArgs args)
        {
            AppWindowPlacement Placement = Window.GetPlacement();
            ApplicationData.Current.LocalSettings.Values["PropertyWindowSizeConfiguration"] = JsonSerializer.Serialize(new WindowSizeConfiguration(Placement.Size.Height, Placement.Size.Width));
        }

        private PropertiesWindowBase(AppWindow Window, DriveDataBase RootDrive) : this(Window)
        {
            this.RootDrive = RootDrive;

            PropertiesTitleLeft.Text = RootDrive.DisplayName;

            GeneralPanelSwitcher.Value = "RootDrive";
            ToolsPanelSwitcher.Value = "RootDrive";

            PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Shortcut_Tab")));
            PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Details_Tab")));

            if (RootDrive is MTPDriveData)
            {
                PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Security_Tab")));
                PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Tools_Tab")));
            }

            SecurityObjectNameContentScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(ScrollableTextBlock_PointerPressed), true);
            SecurityObjectNameContentScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(ScrollableTextBlock_PointerReleased), true);
            SecurityObjectNameContentScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(ScrollableTextBlock_PointerCanceled), true);
            SecurityObjectNameContentScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(ScrollableTextBlock_PointerMoved), true);
        }

        private PropertiesWindowBase(AppWindow Window, params FileSystemStorageItemBase[] StorageItems) : this(Window)
        {
            this.StorageItems = StorageItems;

            PropertiesTitleLeft.Text = string.Join(", ", StorageItems.Select((Item) => Item is FileSystemStorageFolder
                                                                                       ? (string.IsNullOrEmpty(Item.DisplayName)
                                                                                           ? Item.Name
                                                                                           : Item.DisplayName)
                                                                                       : Item.Name));

            switch ((StorageItems?.Length).GetValueOrDefault())
            {
                case > 1:
                    {
                        GeneralPanelSwitcher.Value = "MultiItems";

                        MultiLocationScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(ScrollableTextBlock_PointerPressed), true);
                        MultiLocationScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(ScrollableTextBlock_PointerReleased), true);
                        MultiLocationScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(ScrollableTextBlock_PointerCanceled), true);
                        MultiLocationScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(ScrollableTextBlock_PointerMoved), true);

                        while (PivotControl.Items.Count > (StorageItems.Any((Item) => Item is INotWin32StorageItem) ? 1 : 2))
                        {
                            PivotControl.Items.RemoveAt(PivotControl.Items.Count - 1);
                        }

                        break;
                    }
                case 1:
                    {
                        SecurityObjectNameContentScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(ScrollableTextBlock_PointerPressed), true);
                        SecurityObjectNameContentScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(ScrollableTextBlock_PointerReleased), true);
                        SecurityObjectNameContentScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(ScrollableTextBlock_PointerCanceled), true);
                        SecurityObjectNameContentScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(ScrollableTextBlock_PointerMoved), true);

                        switch (StorageItems.First())
                        {
                            case FileSystemStorageFolder Folder:
                                {
                                    GeneralPanelSwitcher.Value = "Folder";

                                    FolderLocationScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(ScrollableTextBlock_PointerPressed), true);
                                    FolderLocationScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(ScrollableTextBlock_PointerReleased), true);
                                    FolderLocationScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(ScrollableTextBlock_PointerCanceled), true);
                                    FolderLocationScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(ScrollableTextBlock_PointerMoved), true);

                                    while (PivotControl.Items.Count > (Folder is INotWin32StorageItem ? 1 : 2))
                                    {
                                        PivotControl.Items.RemoveAt(PivotControl.Items.Count - 1);
                                    }

                                    break;
                                }
                            case FileSystemStorageFile File:
                                {
                                    GeneralPanelSwitcher.Value = "File";
                                    ToolsPanelSwitcher.Value = "NormalTools";

                                    FileLocationScrollViewer.AddHandler(PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(ScrollableTextBlock_PointerPressed), true);
                                    FileLocationScrollViewer.AddHandler(PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(ScrollableTextBlock_PointerReleased), true);
                                    FileLocationScrollViewer.AddHandler(PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(ScrollableTextBlock_PointerCanceled), true);
                                    FileLocationScrollViewer.AddHandler(PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(ScrollableTextBlock_PointerMoved), true);

                                    switch (File)
                                    {
                                        case MTPStorageFile:
                                            {
                                                PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock)?.Text == Globalization.GetString("Properties_Tools_Tab")));
                                                PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock)?.Text == Globalization.GetString("Properties_Security_Tab")));
                                                break;
                                            }

                                        case FtpStorageFile:
                                            {
                                                UnlockArea.Visibility = Visibility.Collapsed;
                                                PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock)?.Text == Globalization.GetString("Properties_Security_Tab")));
                                                break;
                                            }
                                    }

                                    if (File is not (LinkStorageFile or UrlStorageFile))
                                    {
                                        PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock)?.Text == Globalization.GetString("Properties_Shortcut_Tab")));
                                    }

                                    break;
                                }
                            default:
                                {
                                    throw new NotSupportedException();
                                }
                        }

                        break;
                    }
            }
        }

        private async Task CloseWindowAsync(bool SaveConfig)
        {
            AppWindowPlacement Placement = Window.GetPlacement();
            ApplicationData.Current.LocalSettings.Values["PropertyWindowSizeConfiguration"] = JsonSerializer.Serialize(new WindowSizeConfiguration(Placement.Size.Height, Placement.Size.Width));

            if (SaveConfig)
            {
                await SaveConfigurationAsync(SavingCancellation.Token);
            }

            if (!IsClosed)
            {
                await Window.CloseAsync();
            }
        }

        private void Window_Closed(AppWindow sender, AppWindowClosedEventArgs args)
        {
            Window.Closed -= Window_Closed;

            OperationCancellation?.Cancel();
            OperationCancellation?.Dispose();

            SavingCancellation.Cancel();
            SavingCancellation.Dispose();

            switch ((StorageItems?.Length).GetValueOrDefault())
            {
                case > 1:
                    {
                        MultiLocationScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
                        MultiLocationScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
                        MultiLocationScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
                        MultiLocationScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);
                        break;
                    }
                case 1:
                    {
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);

                        switch (StorageItems.First())
                        {
                            case FileSystemStorageFolder:
                                {
                                    FolderLocationScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
                                    FolderLocationScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
                                    FolderLocationScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
                                    FolderLocationScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);
                                    break;
                                }
                            case FileSystemStorageFile:
                                {
                                    FileLocationScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
                                    FileLocationScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
                                    FileLocationScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
                                    FileLocationScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);
                                    break;
                                }
                        }

                        break;
                    }
                default:
                    {
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerPressedEvent, PointerPressedHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerReleasedEvent, PointerReleasedHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerCanceledEvent, PointerCanceledHandler);
                        SecurityObjectNameContentScrollViewer.RemoveHandler(PointerMovedEvent, PointerMovedHandler);

                        break;
                    }
            }

            IsClosed = true;

            WindowClosed?.Invoke(this, new EventArgs());
        }

        private async Task SaveConfigurationAsync(CancellationToken CancelToken)
        {
            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource(2000))
                using (CancellationTokenRegistration Registeration = Cancellation.Token.Register(async () =>
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        LoadingControl.IsLoading = true;
                    });
                }))
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        if (!CancelToken.IsCancellationRequested)
                        {
                            switch ((StorageItems?.Length).GetValueOrDefault())
                            {
                                case > 1:
                                    {
                                        List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>> AttributeDic = new List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>>();

                                        if (MultiReadonlyAttribute.IsChecked != null)
                                        {
                                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(MultiReadonlyAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.ReadOnly));
                                        }

                                        if (MultiHiddenAttribute.IsChecked != null)
                                        {
                                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(MultiHiddenAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.Hidden));
                                        }

                                        try
                                        {
                                            await Task.WhenAll(StorageItems.Select((Item) => Exclusive.Controller.SetFileAttributeAsync(Item.Path, AttributeDic.ToArray())));
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, $"Could not set the file attribute, details: {string.Join(" & ", AttributeDic.Select((Item) => $"{Enum.GetName(typeof(ModifyAttributeAction), Item.Key)}|{Enum.GetName(typeof(System.IO.FileAttributes), Item.Value)}"))}");
                                        }

                                        break;
                                    }
                                case 1:
                                    {
                                        FileSystemStorageItemBase StorageItem = StorageItems.Single();
                                        List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>> AttributeDic = new List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>>();

                                        switch (StorageItem)
                                        {
                                            case FileSystemStorageFolder:
                                                {
                                                    if (FolderReadonlyAttribute.IsChecked != null)
                                                    {
                                                        AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(FolderReadonlyAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.ReadOnly));
                                                    }

                                                    if (FolderHiddenAttribute.IsChecked! != StorageItem.IsHiddenItem)
                                                    {
                                                        AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(StorageItem.IsHiddenItem ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.Hidden));
                                                    }

                                                    if (FolderStorageItemName.Text != StorageItem.Name)
                                                    {
                                                        if (HandleRenameAutomatically)
                                                        {
                                                            await StorageItem.RenameAsync(FolderStorageItemName.Text);
                                                        }
                                                        else if (RenameRequested != null)
                                                        {
                                                            await RenameRequested.InvokeAsync(this, new FileRenamedDeferredEventArgs(StorageItem.Path, FolderStorageItemName.Text));
                                                        }
                                                    }

                                                    break;
                                                }
                                            case FileSystemStorageFile File:
                                                {
                                                    if (FileReadonlyAttribute.IsChecked! != File.IsReadOnly)
                                                    {
                                                        AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(File.IsReadOnly ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.ReadOnly));
                                                    }

                                                    if (FileHiddenAttribute.IsChecked! != StorageItem.IsHiddenItem)
                                                    {
                                                        AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(StorageItem.IsHiddenItem ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.Hidden));
                                                    }

                                                    if (FileStorageItemName.Text != StorageItem.Name)
                                                    {
                                                        if (HandleRenameAutomatically)
                                                        {
                                                            await StorageItem.RenameAsync(FileStorageItemName.Text);
                                                        }
                                                        else if (RenameRequested != null)
                                                        {
                                                            await RenameRequested.InvokeAsync(this, new FileRenamedDeferredEventArgs(StorageItem.Path, FileStorageItemName.Text));
                                                        }
                                                    }

                                                    break;
                                                }
                                        }

                                        try
                                        {
                                            switch (StorageItem)
                                            {
                                                case LinkStorageFile:
                                                    {
                                                        Match LinkTargetMatch = Regex.Match(ShortcutTargetContent.Text, "(?<=\").+(?=\")");
                                                        Match LinkWorkDirectoryMatch = Regex.Match(ShortcutStartInContent.Text, "(?<=\").+(?=\")");

                                                        string LinkTargetPath = LinkTargetMatch.Success ? LinkTargetMatch.Value : string.Empty;
                                                        string LinkWorkDirectory = LinkWorkDirectoryMatch.Success ? LinkWorkDirectoryMatch.Value : string.Empty;

                                                        await Exclusive.Controller.UpdateLinkAsync(new LinkFileData
                                                        {
                                                            LinkPath = StorageItem.Path,
                                                            LinkTargetPath = LinkTargetPath,
                                                            Arguments = Regex.Replace(ShortcutTargetContent.Text, "^\".*\"\\s+", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries),
                                                            WorkDirectory = LinkWorkDirectory,
                                                            WindowState = (WindowState)ShortcutWindowsStateContent.SelectedIndex,
                                                            HotKey = ShortcutKeyContent.Text == Globalization.GetString("ShortcutHotKey_None") ? (byte)VirtualKey.None : (byte)Enum.Parse<VirtualKey>(ShortcutKeyContent.Text.Replace("Ctrl + Alt + ", string.Empty)),
                                                            Comment = ShortcutCommentContent.Text,
                                                            NeedRunAsAdmin = RunAsAdmin.IsChecked.GetValueOrDefault()
                                                        });

                                                        break;
                                                    }
                                                case UrlStorageFile UrlFile:
                                                    {
                                                        await Exclusive.Controller.UpdateUrlAsync(new UrlFileData(StorageItem.Path, ShortcutUrlContent.Text, Array.Empty<byte>()));

                                                        break;
                                                    }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, $"Could not update file data of {StorageItem.GetType().FullName}");
                                        }

                                        try
                                        {
                                            await Exclusive.Controller.SetFileAttributeAsync(StorageItem.Path, AttributeDic.ToArray());
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, $"Could not set the file attribute, details: {string.Join(" & ", AttributeDic.Select((Item) => $"{Enum.GetName(typeof(ModifyAttributeAction), Item.Key)}|{Enum.GetName(typeof(System.IO.FileAttributes), Item.Value)}"))}");
                                        }

                                        break;
                                    }
                                case 0 when RootDrive is not MTPDriveData:
                                    {
                                        if (RootDriveName.Text != Regex.Replace(RootDrive.DisplayName, $@"\({Regex.Escape(RootDrive.Path.TrimEnd('\\'))}\)$", string.Empty).Trim())
                                        {
                                            if (HandleRenameAutomatically)
                                            {
                                                await Exclusive.Controller.SetDriveLabelAsync(RootDrive.Path, RootDriveName.Text, CancelToken);
                                            }
                                            else if (RenameRequested != null)
                                            {
                                                await RenameRequested.InvokeAsync(this, new FileRenamedDeferredEventArgs(RootDrive.Path, RootDriveName.Text));
                                            }

                                            if (await DriveDataBase.CreateAsync(RootDrive) is DriveDataBase RefreshedDrive)
                                            {
                                                int Index = CommonAccessCollection.DriveList.IndexOf(RootDrive);

                                                if (CommonAccessCollection.DriveList.Remove(RootDrive))
                                                {
                                                    CommonAccessCollection.DriveList.Insert(Index, RefreshedDrive);
                                                }
                                            }
                                        }

                                        if (CompressDrive.Tag is bool CompressOriginStatus && CompressDrive.IsChecked != CompressOriginStatus)
                                        {
                                            await Exclusive.Controller.SetDriveCompressionStatusAsync(RootDrive.Path,
                                                                                                      CompressDrive.IsChecked.GetValueOrDefault(),
                                                                                                      CompressDriveOptionApplySubItems.IsChecked.GetValueOrDefault() && !CompressDriveOptionApplyToRoot.IsChecked.GetValueOrDefault(),
                                                                                                      CancelToken);
                                        }

                                        if (AllowIndex.Tag is bool AllowIndexOriginStatus && AllowIndex.IsChecked != AllowIndexOriginStatus)
                                        {
                                            await Exclusive.Controller.SetDriveIndexStatusAsync(RootDrive.Path,
                                                                                                AllowIndex.IsChecked.GetValueOrDefault(),
                                                                                                AllowIndexOptionApplySubItems.IsChecked.GetValueOrDefault() && !AllowIndexOptionApplyToRoot.IsChecked.GetValueOrDefault(),
                                                                                                CancelToken);
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //No need to handle this exception
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not save the changes in property window");
            }
        }

        private async void PropertiesWindow_Loading(FrameworkElement sender, object args)
        {
            List<Task> ParallelLoadingList = new List<Task>(3)
            {
                LoadDataForGeneralPage()
            };

            if (RootDrive is not MTPDriveData && (StorageItems?.All((Item) => Item is not INotWin32StorageItem)).GetValueOrDefault(true))
            {
                ParallelLoadingList.Add(LoadDataForSecurityPage());
            }

            if ((StorageItems?.Length).GetValueOrDefault() == 1 && StorageItems?.First() is FileSystemStorageFile StorageItem)
            {
                ParallelLoadingList.Add(LoadDataForDetailPage());

                if (StorageItem is LinkStorageFile or UrlStorageFile)
                {
                    ParallelLoadingList.Add(LoadDataForShortCutPage());
                }
            }

            await Task.WhenAll(ParallelLoadingList);
        }

        private async Task LoadDataForSecurityPage()
        {
            try
            {
                string SecurityObjectPath = string.Empty;

                switch ((StorageItems?.Length).GetValueOrDefault())
                {
                    case > 1:
                        {
                            break;
                        }
                    case 1:
                        {
                            SecurityObjectPath = StorageItems.First().Path;
                            break;
                        }
                    default:
                        {
                            SecurityObjectPath = RootDrive.Path;
                            break;
                        }
                }

                if (string.IsNullOrEmpty(SecurityObjectPath))
                {
                    PivotControl.Items.Remove(PivotControl.Items.Cast<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Security_Tab")));
                }
                else
                {
                    SecurityObjectNameContent.Text = SecurityObjectPath;

                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                    {
                        foreach (PermissionDataPackage Data in await Exclusive.Controller.GetPermissionsAsync(SecurityObjectPath))
                        {
                            SecurityAccountList.Items.Add(new SecurityAccount(Data.AccountName, Data.AccountType, Data.AccountPermissions));
                        }
                    }

                    SecurityAccountList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the security page in property window");
            }
        }

        private async Task LoadDataForShortCutPage()
        {
            try
            {
                FileSystemStorageItemBase StorageItem = StorageItems.First();

                ShortcutThumbnail.Source = StorageItem.Thumbnail;
                ShortcutItemName.Text = Path.GetFileNameWithoutExtension(StorageItem.Name);

                switch (StorageItem)
                {
                    case LinkStorageFile LinkFile:
                        {
                            ShortcutPanelSwitcher.Value = "Link";
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
                                    ShortcutKeyContent.Text = "Ctrl + Alt + " + Enum.GetName(typeof(VirtualKey), (VirtualKey)LinkFile.HotKey) ?? Globalization.GetString("ShortcutHotKey_None");
                                }
                            }
                            else
                            {
                                ShortcutKeyContent.Text = Globalization.GetString("ShortcutHotKey_None");
                            }

                            if (LinkFile.LinkType == ShellLinkType.Normal)
                            {
                                string LinkTargetPath = LinkFile.LinkTargetPath.Trim('"');
                                string LinkWorkDirectory = LinkFile.WorkDirectory.Trim('"');

                                ShortcutTargetLocationContent.Text = Path.GetFileName(Path.GetDirectoryName(LinkFile.LinkTargetPath));
                                ShortcutTargetContent.Text = string.IsNullOrEmpty(LinkTargetPath) ? string.Empty : string.Join(" ", $"\"{LinkTargetPath}\"", string.Join(" ", LinkFile.Arguments));
                                ShortcutStartInContent.Text = string.IsNullOrEmpty(LinkWorkDirectory) ? string.Empty : $"\"{LinkWorkDirectory}\"";
                                RunAsAdmin.IsChecked = LinkFile.NeedRunAsAdmin;

                                if (Path.HasExtension(LinkFile.LinkTargetPath))
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                    {
                                        ShortcutTargetTypeContent.Text = await Exclusive.Controller.GetFriendlyTypeNameAsync(Path.GetExtension(LinkFile.LinkTargetPath));
                                    }
                                }
                                else
                                {
                                    ShortcutTargetTypeContent.Text = Globalization.GetString("UnknownText");
                                }
                            }
                            else
                            {
                                ShortcutTargetTypeContent.Text = LinkFile.LinkTargetPath;
                                ShortcutTargetLocationContent.Text = Globalization.GetString("ShortcutTargetApplicationType");
                                ShortcutTargetContent.Text = $"\"{LinkFile.LinkTargetPath}\"";
                                ShortcutTargetContent.IsEnabled = false;
                                ShortcutStartInContent.IsEnabled = false;
                                OpenLocation.IsEnabled = false;
                                RunAsAdmin.IsEnabled = false;
                            }

                            break;
                        }

                    case UrlStorageFile UrlFile:
                        {
                            ShortcutPanelSwitcher.Value = "Url";
                            ShortcutUrlContent.Text = UrlFile.UrlTargetPath;
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the shortcut page in property window");
            }
        }

        private async Task LoadDataForDetailPage()
        {
            FileSystemStorageItemBase StorageItem = StorageItems.First();

            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                using (IDisposable Disposable = FileSystemStorageItemBase.SetBulkAccessSharedController(StorageItem, Exclusive))
                {
                    Dictionary<string, object> BasicPropertiesDictionary = new Dictionary<string, object>(10)
                    {
                        { Globalization.GetString("Properties_Details_Name"), StorageItem.Name },
                        { Globalization.GetString("Properties_Details_ItemType"), StorageItem.DisplayType },
                        { Globalization.GetString("Properties_Details_FolderPath"), Path.GetDirectoryName(StorageItem.Path) },
                        { Globalization.GetString("Properties_Details_Size"), StorageItem.Size.GetFileSizeDescription() },
                        { Globalization.GetString("Properties_Details_DateCreated"), StorageItem.CreationTime.GetDateTimeDescription() },
                        { Globalization.GetString("Properties_Details_DateModified"), StorageItem.ModifiedTime.GetDateTimeDescription() },
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

                    if (StorageItem is not INotWin32StorageItem)
                    {
                        ContentType = await Exclusive.Controller.GetMIMEContentTypeAsync(StorageItem.Path);
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
                            VideoPropertiesDictionary[Globalization.GetString("Properties_Details_FrameRate")] = $"{FrameRate / 1000:N2} {Globalization.GetString("Properties_Details_FrameRatePerSecond")}";
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the details in property window");
            }
        }

        private async Task LoadDataForGeneralPage()
        {
            switch ((StorageItems?.Length).GetValueOrDefault())
            {
                case > 1:
                    {
                        MultiThumbnail.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/MultiItems_White.png" : "ms-appx:///Assets/MultiItems_Black.png"));
                        MultiTypeContent.Text = StorageItems.Skip(1).All((Item) => Item.DisplayType == StorageItems.First().DisplayType) ? $"{Globalization.GetString("MultiProperty_Type_Text")} {StorageItems.First().DisplayType}" : Globalization.GetString("MultiProperty_TypeDescription_Text");
                        MultiStorageItemName.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                        MultiSizeContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                        MultiSizeOnDiskContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                        MultiLocationContent.Text = StorageItems.Skip(1).All((Item) => Path.GetDirectoryName(Item.Path).Equals(Path.GetDirectoryName(StorageItems.First().Path), StringComparison.OrdinalIgnoreCase)) ? $"{Globalization.GetString("MultiProperty_Location_Text")} {Path.GetDirectoryName(StorageItems.First().Path) ?? StorageItems.First().Path}" : Globalization.GetString("MultiProperty_DiffLocation_Text");
                        MultiHiddenAttribute.IsChecked = StorageItems.All((Item) => Item.IsHiddenItem)
                                                                      ? true
                                                                      : (StorageItems.Any((Item) => Item.IsHiddenItem)
                                                                            ? null
                                                                            : false);
                        MultiReadonlyAttribute.IsChecked = StorageItems.Any((Item) => Item is FileSystemStorageFolder)
                                                                       ? null
                                                                       : (Array.TrueForAll(StorageItems, (Item) => Item.IsReadOnly)
                                                                               ? true
                                                                               : (Array.TrueForAll(StorageItems, (Item) => !Item.IsReadOnly)
                                                                                       ? false
                                                                                       : null));
                        MultiHiddenAttribute.IsEnabled = StorageItems.All((Item) => Item is not INotWin32StorageItem);
                        MultiReadonlyAttribute.IsEnabled = StorageItems.All((Item) => Item is not INotWin32StorageItem);

                        try
                        {
                            long FileCount = 0;
                            long FolderCount = 0;
                            long TotalSize = 0;

                            ConcurrentBag<Task<ulong>> SizeOnDiskTaskList = new ConcurrentBag<Task<ulong>>();

                            await Task.Factory.StartNew(() => Parallel.ForEach(StorageItems, (StorageItem) =>
                            {
                                try
                                {
                                    if (StorageItem is FileSystemStorageFolder Folder)
                                    {
                                        Interlocked.Increment(ref FolderCount);

                                        using (CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation.Token))
                                        {
                                            IReadOnlyList<FileSystemStorageItemBase> Result = Folder.GetChildItemsAsync(true, true, true, CancelToken: Cancellation.Token).ToEnumerable().ToArray();

                                            Interlocked.Add(ref TotalSize, Result.OfType<FileSystemStorageFile>().Sum((SubFile) => Convert.ToInt64(SubFile.Size)));
                                            Interlocked.Add(ref FileCount, Result.OfType<FileSystemStorageFile>().LongCount());
                                            Interlocked.Add(ref FolderCount, Result.OfType<FileSystemStorageFolder>().LongCount());

                                            foreach (FileSystemStorageFile SubFile in Result.OfType<FileSystemStorageFile>())
                                            {
                                                SizeOnDiskTaskList.Add(SubFile.GetSizeOnDiskAsync());
                                            }
                                        }
                                    }
                                    else if (StorageItem is FileSystemStorageFile File)
                                    {
                                        Interlocked.Add(ref FileCount, 1);
                                        Interlocked.Add(ref TotalSize, Convert.ToInt64(File.Size));
                                        SizeOnDiskTaskList.Add(File.GetSizeOnDiskAsync());
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    //No need to handle this exception
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} threw an exception");
                                }
                            }), TaskCreationOptions.LongRunning);

                            MultiStorageItemName.Text = $"{FileCount} {Globalization.GetString("FolderInfo_File_Count")} , {FolderCount} {Globalization.GetString("FolderInfo_Folder_Count")}";
                            MultiSizeContent.Text = $"{Convert.ToUInt64(TotalSize).GetFileSizeDescription()} ({TotalSize:N0} {Globalization.GetString("Drive_Capacity_Unit")})";

                            ulong[] SizeOnDiskResultArray = await Task.WhenAll(SizeOnDiskTaskList);
                            ulong SizeOnDisk = Convert.ToUInt64(SizeOnDiskResultArray.Sum((Result) => Convert.ToInt64(Result)));
                            MultiSizeOnDiskContent.Text = SizeOnDisk > 0 ? $"{SizeOnDisk.GetFileSizeDescription()} ({SizeOnDisk:N0} {Globalization.GetString("Drive_Capacity_Unit")})" : Globalization.GetString("UnknownText");
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} threw an exception");
                        }

                        break;
                    }
                case 1:
                    {
                        switch (StorageItems.Single())
                        {
                            case FileSystemStorageFolder Folder:
                                {
                                    FolderStorageItemName.Text = Folder.DisplayName;
                                    FolderThumbnail.Source = Folder.Thumbnail;
                                    FolderTypeContent.Text = Folder.DisplayType;
                                    FolderSizeContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                                    FolderContainsContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                                    FolderLocationContent.Text = Path.GetDirectoryName(Folder.Path) ?? Folder.Path;
                                    FolderCreatedContent.Text = Folder.CreationTime == DateTimeOffset.MaxValue.ToLocalTime() || Folder.CreationTime == DateTimeOffset.MinValue.ToLocalTime() ? Globalization.GetString("UnknownText") : Folder.CreationTime.ToString("F");
                                    FolderHiddenAttribute.IsChecked = Folder.IsHiddenItem;
                                    FolderReadonlyAttribute.IsChecked = null;
                                    FolderReadonlyAttribute.IsEnabled = Folder is not INotWin32StorageItem;
                                    FolderHiddenAttribute.IsEnabled = Folder is not INotWin32StorageItem;

                                    try
                                    {
                                        using (CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation.Token))
                                        {
                                            Task CountTask = CalculateFolderAndFileCount(Folder, Cancellation.Token).ContinueWith((PreviousTask) =>
                                            {
                                                FolderContainsContent.Text = $"{PreviousTask.Result.Item1} {Globalization.GetString("FolderInfo_File_Count")} , {PreviousTask.Result.Item2} {Globalization.GetString("FolderInfo_Folder_Count")}";
                                            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                            Task SizeTask = CalculateFolderSize(Folder, Cancellation.Token).ContinueWith((PreviousTask) =>
                                            {
                                                FolderSizeContent.Text = $"{PreviousTask.Result.GetFileSizeDescription()} ({PreviousTask.Result:N0} {Globalization.GetString("Drive_Capacity_Unit")})";
                                            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                            await Task.WhenAll(CountTask, SizeTask);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        //No need to handle this exception
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} threw an exception");
                                    }

                                    break;
                                }
                            case FileSystemStorageFile File:
                                {
                                    FileStorageItemName.Text = File.Name;
                                    FileThumbnail.Source = File.Thumbnail;
                                    FileReadonlyAttribute.IsChecked = File.IsReadOnly;
                                    FileSizeContent.Text = $"{File.Size.GetFileSizeDescription()} ({File.Size:N0} {Globalization.GetString("Drive_Capacity_Unit")})";
                                    FileLocationContent.Text = Path.GetDirectoryName(File.Path) ?? File.Path;
                                    FileCreatedContent.Text = File.CreationTime == DateTimeOffset.MaxValue.ToLocalTime() || File.CreationTime == DateTimeOffset.MinValue.ToLocalTime() ? Globalization.GetString("UnknownText") : File.CreationTime.ToString("F");
                                    FileModifiedContent.Text = File.ModifiedTime == DateTimeOffset.MaxValue.ToLocalTime() || File.ModifiedTime == DateTimeOffset.MinValue.ToLocalTime() ? Globalization.GetString("UnknownText") : File.ModifiedTime.ToString("F");
                                    FileAccessedContent.Text = File.LastAccessTime == DateTimeOffset.MaxValue.ToLocalTime() || File.LastAccessTime == DateTimeOffset.MinValue.ToLocalTime() ? Globalization.GetString("UnknownText") : File.LastAccessTime.ToString("F");
                                    FileHiddenAttribute.IsChecked = File.IsHiddenItem;
                                    FileHiddenAttribute.IsEnabled = File is not INotWin32StorageItem;
                                    FileReadonlyAttribute.IsChecked = File.IsReadOnly;
                                    FileReadonlyAttribute.IsEnabled = File is not INotWin32StorageItem;
                                    FileChangeOpenWithButton.Visibility = File is INotWin32StorageItem ? Visibility.Collapsed : Visibility.Visible;

                                    if (Regex.IsMatch(File.Name, @"\.(exe|bat|lnk|url)$"))
                                    {
                                        if (File is LinkStorageFile Link)
                                        {
                                            switch (await FileSystemStorageItemBase.OpenAsync(Link.LinkTargetPath))
                                            {
                                                case FileSystemStorageFile TargetFile:
                                                    {
                                                        FileDescriptionContent.Text = await Helper.GetExecuteableFileDisplayNameAsync(TargetFile);
                                                        break;
                                                    }
                                                case FileSystemStorageFolder TargetFolder:
                                                    {
                                                        FileDescriptionContent.Text = TargetFolder.DisplayName;
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        FileDescriptionContent.Text = string.Empty;
                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            FileDescriptionContent.Text = await Helper.GetExecuteableFileDisplayNameAsync(File);
                                        }
                                    }

                                    ulong SizeOnDisk = await File.GetSizeOnDiskAsync();

                                    if (SizeOnDisk > 0)
                                    {
                                        FileSizeOnDiskContent.Text = $"{SizeOnDisk.GetFileSizeDescription()} ({SizeOnDisk:N0} {Globalization.GetString("Drive_Capacity_Unit")})";
                                    }
                                    else
                                    {
                                        FileSizeOnDiskContent.Text = Globalization.GetString("UnknownText");
                                    }

                                    bool IsTypeEmpty = string.IsNullOrEmpty(File.Type);
                                    bool IsDisplayTypeEmpty = string.IsNullOrEmpty(File.DisplayType);

                                    if (IsDisplayTypeEmpty && IsTypeEmpty)
                                    {
                                        FileTypeContent.Text = Globalization.GetString("UnknownText");
                                    }
                                    else if (IsDisplayTypeEmpty && !IsTypeEmpty)
                                    {
                                        FileTypeContent.Text = File.Type.ToUpper();
                                    }
                                    else if (!IsDisplayTypeEmpty && IsTypeEmpty)
                                    {
                                        FileTypeContent.Text = File.DisplayType;
                                    }
                                    else
                                    {
                                        FileTypeContent.Text = $"{File.DisplayType} ({File.Type.ToLower()})";
                                    }

                                    string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                                    if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath == Package.Current.Id.FamilyName)
                                    {
                                        switch (File.Type.ToLower())
                                        {
                                            case ".jpg":
                                            case ".jpeg":
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
                                                    FileOpenWithContent.Text = Globalization.GetString("AppDisplayName");

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

                                                                FileOpenWithImage.Source = await Helper.CreateBitmapImageAsync(Stream);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
                                                    }

                                                    break;
                                                }
                                            default:
                                                {
                                                    try
                                                    {
                                                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                                        {
                                                            AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(File.Path);
                                                        }

                                                        if (string.IsNullOrEmpty(AdminExecutablePath))
                                                        {
                                                            FileOpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                                            FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
                                                        }
                                                        else
                                                        {
                                                            if (await FileSystemStorageItemBase.OpenAsync(AdminExecutablePath) is FileSystemStorageFile OpenWithFile)
                                                            {
                                                                FileOpenWithImage.Source = await OpenWithFile.GetThumbnailAsync(ThumbnailMode.SingleItem);
                                                                FileOpenWithContent.Text = await Helper.GetExecuteableFileDisplayNameAsync(OpenWithFile);
                                                            }
                                                            else
                                                            {
                                                                throw new FileNotFoundException(AdminExecutablePath);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        FileOpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                                        FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
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
                                                FileOpenWithImage.Source = await OpenWithFile.GetThumbnailAsync(ThumbnailMode.SingleItem);
                                                FileOpenWithContent.Text = await Helper.GetExecuteableFileDisplayNameAsync(OpenWithFile);
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException(AdminExecutablePath);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            FileOpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                            FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            if (!string.IsNullOrEmpty(File.Type) && (await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                            {
                                                FileOpenWithContent.Text = Info.Package.DisplayName;

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

                                                        FileOpenWithImage.Source = await Helper.CreateBitmapImageAsync(Stream);
                                                    }
                                                }

                                            }
                                            else
                                            {
                                                FileOpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                                FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            FileOpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                                            FileOpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/SingleItem_White.png" : "ms-appx:///Assets/SingleItem_Black.png"));
                                        }
                                    }

                                    break;
                                }
                        }

                        break;
                    }
                default:
                    {
                        RootDriveThumbnail.Source = RootDrive.Thumbnail;
                        RootDriveName.Text = RootDrive.Name;
                        RootDriveCapacity.Text = RootDrive.UsedSpace;
                        RootDriveFreeSpace.Text = RootDrive.FreeSpace;
                        RootDriveTotalSpace.Text = RootDrive.Capacity;
                        RootDriveUsedByte.Text = $"{RootDrive.UsedByte:N0} {Globalization.GetString("Drive_Capacity_Unit")}";
                        RootDriveFreeByte.Text = $"{RootDrive.FreeByte:N0} {Globalization.GetString("Drive_Capacity_Unit")}";
                        RootDriveTotalByte.Text = $"{RootDrive.TotalByte:N0} {Globalization.GetString("Drive_Capacity_Unit")}";
                        RootDriveFileSystemContent.Text = RootDrive.FileSystem;
                        RootDriveDescriptionLabel.Text = $"{Globalization.GetString("Drive_Description_Name")} {RootDrive.Path.TrimEnd('\\')}";
                        RootDriveTypeContent.Text = RootDrive.DriveType switch
                        {
                            DriveType.Fixed => Globalization.GetString("Drive_Type_1"),
                            DriveType.CDRom => Globalization.GetString("Drive_Type_2"),
                            DriveType.Removable => Globalization.GetString("Drive_Type_3"),
                            DriveType.Ram => Globalization.GetString("Drive_Type_4"),
                            DriveType.Network => Globalization.GetString("Drive_Type_5"),
                            _ => Globalization.GetString("UnknownText")
                        };

                        CapacityRingStoryboard.Begin();

                        if (RootDrive is MTPDriveData || RootDrive.DriveType != DriveType.Fixed)
                        {
                            AllowIndex.IsEnabled = false;
                            CompressDrive.IsEnabled = false;
                            DriveCleanup.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                            {
                                bool IsCompressed = await Exclusive.Controller.GetDriveCompressionStatusAsync(RootDrive.Path);
                                bool IsAllowIndex = await Exclusive.Controller.GetDriveIndexStatusAsync(RootDrive.Path);

                                CompressDrive.IsChecked = IsCompressed;
                                CompressDrive.Tag = IsCompressed;
                                AllowIndex.IsChecked = IsAllowIndex;
                                AllowIndex.Tag = IsAllowIndex;
                            }
                        }

                        break;
                    }
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            await ActionButtonExecution.ExecuteAsync(async () =>
            {
                await CloseWindowAsync(true);
            });
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await ActionButtonExecution.ExecuteAsync(async () =>
            {
                await CloseWindowAsync(false);
            });
        }

        private async Task<ulong> CalculateFolderSize(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            return await Folder.GetFolderSizeAsync(CancelToken);
        }

        private async Task<(ulong, ulong)> CalculateFolderAndFileCount(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            IReadOnlyList<FileSystemStorageItemBase> Result = await Folder.GetChildItemsAsync(true, true, true, CancelToken: CancelToken).ToListAsync();

            if (Result.Count > 0)
            {
                return (Convert.ToUInt64(Result.OfType<FileSystemStorageFile>().LongCount()), Convert.ToUInt64(Result.OfType<FileSystemStorageFolder>().LongCount()));
            }

            return (0, 0);
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
            if (StorageItems.Single() is LinkStorageFile Link)
            {
                await TabViewContainer.Current.CreateNewTabAsync(new string[] { Path.GetDirectoryName(Link.LinkTargetPath) });

                if (TabViewContainer.Current.TabCollection.LastOrDefault()?.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                {
                    for (int Retry = 0; Retry < 5; Retry++)
                    {
                        await Task.Delay(500);

                        if (Renderer.CurrentPresenter?.FileCollection.FirstOrDefault((SItem) => SItem.Path == Link.LinkTargetPath) is FileSystemStorageItemBase Target)
                        {
                            Renderer.CurrentPresenter.SelectedItem = Target;
                            Renderer.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                        }
                    }
                }
            }
        }

        private async void CalculateMd5_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItems.Single() is FileSystemStorageFile File)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                {
                    DateTimeOffset StartTime = DateTimeOffset.Now;

                    try
                    {
                        MD5Progress.Value = 0;
                        MD5Progress.IsIndeterminate = false;

                        MD5TextBox.IsEnabled = false;
                        MD5TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                        CalculateMd5.IsEnabled = false;

                        using (CancellationTokenSource Md5Cancellation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation.Token))
                        {
                            if (File is FtpStorageFile)
                            {
                                MD5Progress.IsIndeterminate = true;

                                try
                                {
                                    FtpPathAnalysis Analysis = new FtpPathAnalysis(File.Path);

                                    if (await FtpClientManager.GetClientControllerAsync(Analysis) is FtpClientController Controller)
                                    {
                                        FtpHash Hash = await Controller.RunCommandAsync((Client) => Client.GetChecksum(Analysis.RelatedPath, FtpHashAlgorithm.MD5, Md5Cancellation.Token));

                                        if (Hash.IsValid)
                                        {
                                            MD5TextBox.Text = Hash.Value;
                                        }
                                        else
                                        {
                                            MD5TextBox.Text = Globalization.GetString("HashError");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not get the MD5 hash from ftp server directly");

                                    MD5Progress.IsIndeterminate = false;

                                    using (MD5 MD5Alg = MD5.Create())
                                    using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                    {
                                        MD5TextBox.Text = await MD5Alg.GetHashAsync(Stream, Md5Cancellation.Token, async (s, args) =>
                                        {
                                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                            {
                                                MD5Progress.Value = args.ProgressPercentage;
                                            });
                                        });
                                    }
                                }
                            }
                            else
                            {
                                using (MD5 MD5Alg = MD5.Create())
                                using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                {
                                    MD5TextBox.Text = await MD5Alg.GetHashAsync(Stream, Md5Cancellation.Token, async (s, args) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            MD5Progress.Value = args.ProgressPercentage;
                                        });
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        MD5TextBox.Text = Globalization.GetString("HashError");
                    }
                    finally
                    {
                        if ((DateTimeOffset.Now - StartTime).TotalMilliseconds < 500)
                        {
                            await Task.Delay(500);
                        }

                        MD5TextBox.IsEnabled = true;
                        CalculateMd5.IsEnabled = true;
                        CheckHashButton.IsEnabled = true;
                        CheckHashBox.IsEnabled = true;
                        CheckHashTitle.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
                    }
                }
            }
        }

        private async void CalculateSHA1_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.OpenAsync(StorageItems.First().Path) is FileSystemStorageFile File)
            {
                DateTimeOffset StartTime = DateTimeOffset.Now;

                try
                {
                    SHA1Progress.Value = 0;
                    SHA1Progress.IsIndeterminate = false;

                    SHA1TextBox.IsEnabled = false;
                    SHA1TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                    CalculateSHA1.IsEnabled = false;

                    using (CancellationTokenSource SHA1Cancellation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation.Token))
                    {
                        if (File is FtpStorageFile)
                        {
                            SHA1Progress.IsIndeterminate = true;

                            try
                            {
                                FtpPathAnalysis Analysis = new FtpPathAnalysis(File.Path);

                                if (await FtpClientManager.GetClientControllerAsync(Analysis) is FtpClientController Controller)
                                {
                                    FtpHash Hash = await Controller.RunCommandAsync((Client) => Client.GetChecksum(Analysis.RelatedPath, FtpHashAlgorithm.SHA1, SHA1Cancellation.Token));

                                    if (Hash.IsValid)
                                    {
                                        SHA1TextBox.Text = Hash.Value;
                                    }
                                    else
                                    {
                                        SHA1TextBox.Text = Globalization.GetString("HashError");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not get the SHA1 hash from ftp server directly");

                                SHA1Progress.IsIndeterminate = false;

                                using (SHA1 SHA1Alg = SHA1.Create())
                                using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                {
                                    SHA1TextBox.Text = await SHA1Alg.GetHashAsync(Stream, SHA1Cancellation.Token, async (s, args) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            SHA1Progress.Value = args.ProgressPercentage;
                                        });
                                    });
                                }
                            }
                        }
                        else
                        {
                            using (SHA1 SHA1Alg = SHA1.Create())
                            using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            {
                                SHA1TextBox.Text = await SHA1Alg.GetHashAsync(Stream, SHA1Cancellation.Token, async (s, args) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        SHA1Progress.Value = args.ProgressPercentage;
                                    });
                                });
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    SHA1TextBox.Text = Globalization.GetString("HashError");
                }
                finally
                {
                    if ((DateTimeOffset.Now - StartTime).TotalMilliseconds < 500)
                    {
                        await Task.Delay(500);
                    }

                    SHA1TextBox.IsEnabled = true;
                    CalculateSHA1.IsEnabled = true;
                    CheckHashButton.IsEnabled = true;
                    CheckHashBox.IsEnabled = true;
                    CheckHashTitle.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
                }
            }
        }

        private async void CalculateSHA256_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.OpenAsync(StorageItems.First().Path) is FileSystemStorageFile File)
            {
                DateTimeOffset StartTime = DateTimeOffset.Now;

                try
                {
                    SHA256Progress.Value = 0;
                    SHA256Progress.IsIndeterminate = false;

                    SHA256TextBox.IsEnabled = false;
                    SHA256TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                    CalculateSHA256.IsEnabled = false;

                    using (CancellationTokenSource SHA256Cancellation = CancellationTokenSource.CreateLinkedTokenSource(OperationCancellation.Token))
                    {
                        if (File is FtpStorageFile)
                        {
                            SHA256Progress.IsIndeterminate = true;

                            try
                            {
                                FtpPathAnalysis Analysis = new FtpPathAnalysis(File.Path);

                                if (await FtpClientManager.GetClientControllerAsync(Analysis) is FtpClientController Controller)
                                {
                                    FtpHash Hash = await Controller.RunCommandAsync((Client) => Client.GetChecksum(Analysis.RelatedPath, FtpHashAlgorithm.SHA256, SHA256Cancellation.Token));

                                    if (Hash.IsValid)
                                    {
                                        SHA256TextBox.Text = Hash.Value;
                                    }
                                    else
                                    {
                                        SHA256TextBox.Text = Globalization.GetString("HashError");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not get the SHA256 hash from ftp server directly");

                                SHA256Progress.IsIndeterminate = false;

                                using (SHA256 SHA256Alg = SHA256.Create())
                                using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                {
                                    SHA256TextBox.Text = await SHA256Alg.GetHashAsync(Stream, SHA256Cancellation.Token, async (s, args) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            SHA256Progress.Value = args.ProgressPercentage;
                                        });
                                    });
                                }
                            }
                        }
                        else
                        {
                            using (SHA256 SHA256Alg = SHA256.Create())
                            using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            {
                                SHA256TextBox.Text = await SHA256Alg.GetHashAsync(Stream, SHA256Cancellation.Token, async (s, args) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        SHA256Progress.Value = args.ProgressPercentage;
                                    });
                                });
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    SHA256TextBox.Text = Globalization.GetString("HashError");
                }
                finally
                {
                    if ((DateTimeOffset.Now - StartTime).TotalMilliseconds < 500)
                    {
                        await Task.Delay(500);
                    }

                    SHA256TextBox.IsEnabled = true;
                    CalculateSHA256.IsEnabled = true;
                    CheckHashButton.IsEnabled = true;
                    CheckHashBox.IsEnabled = true;
                    CheckHashTitle.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
                }
            }
        }

        private async void ChangeOpenWithButton_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItems.First() is FileSystemStorageFile File)
            {
                await CloseWindowAsync(false);

                await CoreApplication.MainView.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                {
                    await new ProgramPickerDialog(File, true).ShowAsync();
                });
            }
        }

        private async void Unlock_Click(object sender, RoutedEventArgs e)
        {
            UnlockFlyout.Hide();

            if (StorageItems.First() is FileSystemStorageFile File)
            {
                DateTimeOffset StartTime = DateTimeOffset.Now;

                try
                {
                    VisualStateManager.GoToState(this, "UnlockRunningStatus", true);

                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        if (await Exclusive.Controller.TryUnlockFileAsync(File.Path, ((Button)sender).Name == "CloseForce"))
                        {
                            UnlockText.Text = Globalization.GetString("Properties_Tools_Unlock_Success");
                        }
                        else
                        {
                            UnlockText.Text = Globalization.GetString("Properties_Tools_Unlock_Failure");
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    UnlockText.Text = Globalization.GetString("Properties_Tools_Unlock_FileNotFound");
                }
                catch (UnlockFileFailedException)
                {
                    UnlockText.Text = Globalization.GetString("Properties_Tools_Unlock_NoLock");
                }
                catch
                {
                    UnlockText.Text = Globalization.GetString("Properties_Tools_Unlock_UnexpectedError");
                }
                finally
                {
                    if ((DateTimeOffset.Now - StartTime).TotalMilliseconds < 1000)
                    {
                        await Task.Delay(1500);
                    }

                    if (UnlockText.Text == Globalization.GetString("Properties_Tools_Unlock_Success"))
                    {
                        VisualStateManager.GoToState(this, "UnlockSuccessStatus", true);
                    }
                    else
                    {
                        VisualStateManager.GoToState(this, "UnlockErrorStatus", true);
                    }
                }
            }
        }

        private void ScrollableTextBlock_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                {
                    PointerPoint Pointer = e.GetCurrentPoint(Viewer);

                    if (Pointer.Properties.IsLeftButtonPressed)
                    {
                        double XOffset = Pointer.Position.X;
                        double HorizontalRightScrollThreshold = Viewer.ActualWidth - 30;
                        double HorizontalLeftScrollThreshold = 30;

                        if (XOffset > HorizontalRightScrollThreshold)
                        {
                            Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null);
                        }
                        else if (XOffset < HorizontalLeftScrollThreshold)
                        {
                            Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null);
                        }
                    }
                }
            }
        }

        private void ScrollableTextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                PointerPoint Pointer = e.GetCurrentPoint(Viewer);

                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && Pointer.Properties.IsLeftButtonPressed)
                {
                    Viewer.CapturePointer(e.Pointer);
                }
            }
        }

        private void ScrollableTextBlock_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                if ((Viewer.PointerCaptures?.Any()).GetValueOrDefault())
                {
                    Viewer.ReleasePointerCaptures();
                }
            }
        }

        private void ScrollableTextBlock_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                if ((Viewer.PointerCaptures?.Any()).GetValueOrDefault())
                {
                    Viewer.ReleasePointerCaptures();
                }
            }
        }

        private async void DriveCleanup_Click(object sender, RoutedEventArgs e)
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                if (!await Exclusive.Controller.RunAsync("cleanmgr.exe", Parameters: new string[] { "/d", RootDrive.Path.TrimEnd('\\') }))
                {
                    LogTracer.Log("Could not launch exe: cleanmgr.exe");
                }
            }
        }

        private void CompressDrive_Checked(object sender, RoutedEventArgs e)
        {
            if (CompressDrive.Tag is bool OriginStatus)
            {
                if (OriginStatus)
                {
                    CompressDriveOptionArea.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CompressDriveOptionArea.Visibility = Visibility.Visible;
                }
            }
        }

        private void AllowIndex_Checked(object sender, RoutedEventArgs e)
        {
            if (AllowIndex.Tag is bool OriginStatus)
            {
                if (OriginStatus)
                {
                    AllowIndexOptionArea.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AllowIndexOptionArea.Visibility = Visibility.Visible;
                }
            }
        }

        private void CompressDrive_Unchecked(object sender, RoutedEventArgs e)
        {
            if (CompressDrive.Tag is bool OriginStatus)
            {
                if (OriginStatus)
                {
                    CompressDriveOptionArea.Visibility = Visibility.Visible;
                }
                else
                {
                    CompressDriveOptionArea.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void AllowIndex_Unchecked(object sender, RoutedEventArgs e)
        {
            if (AllowIndex.Tag is bool OriginStatus)
            {
                if (OriginStatus)
                {
                    AllowIndexOptionArea.Visibility = Visibility.Visible;
                }
                else
                {
                    AllowIndexOptionArea.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CancelSavingButton_Click(object sender, RoutedEventArgs e)
        {
            SavingCancellation?.Cancel();
        }

        private async void DriveOptimize_Click(object sender, RoutedEventArgs e)
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                if (!await Exclusive.Controller.RunAsync("dfrgui.exe"))
                {
                    LogTracer.Log("Could not launch dfrgui.exe");
                }
            }
        }

        private async void DriveErrorCheck_Click(object sender, RoutedEventArgs e)
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                if (!await Exclusive.Controller.RunAsync("powershell.exe", RunAsAdmin: true, Parameters: new string[] { $"-NoExit -Command \"chkdsk {RootDrive.Path.TrimEnd('\\')}\"" }))
                {
                    LogTracer.Log("Could not launch chkdsk.exe");
                }
            }
        }

        private void SecurityAccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecurityAccountList.SelectedItem is SecurityAccount Account)
            {
                SecurityPermissionList.Items.Clear();

                foreach (KeyValuePair<Permissions, bool> PermissionMap in Account.AccountPermissions)
                {
                    SecurityPermissionList.Items.Add(new SecurityAccountPermissions(PermissionMap.Key, PermissionMap.Value));
                }
            }
        }

        private void CheckHashButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CheckHashBox.Text))
            {
                if (MD5TextBox.IsEnabled && CheckHashBox.Text.Equals(MD5TextBox.Text, StringComparison.OrdinalIgnoreCase))
                {
                    HashKindLabel.Content = "MD5";
                }
                else if (SHA1TextBox.IsEnabled && CheckHashBox.Text.Equals(SHA1TextBox.Text, StringComparison.OrdinalIgnoreCase))
                {
                    HashKindLabel.Content = "SHA1";
                }
                else if (SHA256TextBox.IsEnabled && CheckHashBox.Text.Equals(SHA256TextBox.Text, StringComparison.OrdinalIgnoreCase))
                {
                    HashKindLabel.Content = "SHA256";
                }
                else
                {
                    HashKindLabel.Content = Globalization.GetString("Properties_Tools_CheckHashKind_None");
                }
            }
        }

        private void CheckHashBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            HashKindLabel.Content = string.Empty;
            VisualStateManager.GoToState(this, "NormalHashCheckStatus", true);
        }
    }
}
