using ComputerVision;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Input;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FilePresenter : Page
    {
        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; private set; }

        private FileControl FileControlInstance;

        private int DropLock;

        private int ViewDropLock;

        private CancellationTokenSource HashCancellation;

        public StorageAreaWatcher AreaWatcher { get; private set; }

        private ListViewBase itemPresenter;
        public ListViewBase ItemPresenter
        {
            get
            {
                return itemPresenter;
            }
            set
            {
                if (value != itemPresenter)
                {
                    itemPresenter = value;

                    if (value is GridView)
                    {
                        GridViewControl.Visibility = Visibility.Visible;
                        ListViewControl.Visibility = Visibility.Collapsed;
                        GridViewControl.ItemsSource = FileCollection;
                        ListViewControl.ItemsSource = null;
                    }
                    else
                    {
                        ListViewControl.Visibility = Visibility.Visible;
                        GridViewControl.Visibility = Visibility.Collapsed;
                        ListViewControl.ItemsSource = FileCollection;
                        GridViewControl.ItemsSource = null;
                    }
                }
            }
        }

        private WiFiShareProvider WiFiProvider;
        private FileSystemStorageItemBase TabTarget;
        private FileSystemStorageItemBase CurrentNameEditItem;
        private DateTimeOffset LastClickTime;
        private DateTimeOffset LastPressTime;
        private string LastPressChar;

        public FileSystemStorageItemBase SelectedItem
        {
            get
            {
                return ItemPresenter.SelectedItem as FileSystemStorageItemBase;
            }
            set
            {
                ItemPresenter.SelectedItem = value;
            }
        }

        public List<FileSystemStorageItemBase> SelectedItems
        {
            get
            {
                return ItemPresenter.SelectedItems.Select((Item) => Item as FileSystemStorageItemBase).ToList();
            }
        }

        public FilePresenter()
        {
            InitializeComponent();

            FileCollection = new ObservableCollection<FileSystemStorageItemBase>();
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
            Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
            Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.KeyStatus.IsMenuKeyDown)
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Left when FileControlInstance.GoBackRecord.IsEnabled:
                        {
                            FileControlInstance.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.Right when FileControlInstance.GoForwardRecord.IsEnabled:
                        {
                            FileControlInstance.GoForwardRecord_Click(null, null);
                            break;
                        }
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileControl Instance)
            {
                CommonAccessCollection.Register(Instance, this);

                FileControlInstance = Instance;

                AreaWatcher = new StorageAreaWatcher(FileCollection, FileControlInstance.FolderTree);
            }
        }

        private async void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);
            CoreVirtualKeyStates ShiftState = sender.GetKeyState(VirtualKey.Shift);
            
            bool HasHiddenItem = SelectedItems.Any((Item) => Item is HiddenStorageItem);

            if (!FileControlInstance.IsSearchOrPathBoxFocused && !QueueContentDialog.IsRunningOrWaiting && !MainPage.ThisPage.IsAnyTaskRunning)
            {
                args.Handled = true;

                if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                {
                    NavigateToStorageItem(args.VirtualKey);
                }

                switch (args.VirtualKey)
                {
                    case VirtualKey.Space when SelectedItem != null && SettingControl.IsQuicklookAvailable && SettingControl.IsQuicklookEnable:
                        {
                            await FullTrustProcessController.Current.ViewWithQuicklookAsync(SelectedItem.Path).ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.Delete:
                        {
                            Delete_Click(null, null);
                            break;
                        }
                    case VirtualKey.F2 when !HasHiddenItem:
                        {
                            Rename_Click(null, null);
                            break;
                        }
                    case VirtualKey.F5:
                        {
                            Refresh_Click(null, null);
                            break;
                        }
                    case VirtualKey.Enter when SelectedItems.Count == 1 && SelectedItem is FileSystemStorageItemBase Item && !HasHiddenItem:
                        {
                            await EnterSelectedItem(Item).ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.Back when FileControlInstance.GoBackRecord.IsEnabled:
                        {
                            FileControlInstance.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            FileControlInstance.AddressBox.Focus(FocusState.Programmatic);
                            break;
                        }
                    case VirtualKey.V when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Paste_Click(null, null);
                            break;
                        }
                    case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItem == null:
                        {
                            ItemPresenter.SelectAll();
                            break;
                        }
                    case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Copy_Click(null, null);
                            break;
                        }
                    case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Cut_Click(null, null);
                            break;
                        }
                    case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Delete_Click(null, null);
                            break;
                        }
                    case VirtualKey.F when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            FileControlInstance.GlobeSearch.Focus(FocusState.Programmatic);
                            break;
                        }
                    case VirtualKey.N when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            CreateFolder_Click(null, null);
                            break;
                        }
                    case VirtualKey.Z when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && OperationRecorder.Current.Value.Count > 0:
                        {
                            await Ctrl_Z_Click().ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.E when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && FileControlInstance.CurrentFolder != null:
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                            break;
                        }
                    case VirtualKey.T when ShiftState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            OpenInTerminal_Click(null, null);
                            break;
                        }
                    case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            OpenFolderInNewTab_Click(null, null);
                            break;
                        }
                    case VirtualKey.W when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            OpenFolderInNewWindow_Click(null, null);
                            break;
                        }
                    case VirtualKey.G when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            ItemOpen_Click(null, null);
                            break;
                        }
                }
            }
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            if (Key >= VirtualKey.A && Key <= VirtualKey.Z)
            {
                try
                {
                    string TargetChar = Convert.ToChar((int)Key).ToString();

                    if (LastPressChar != TargetChar && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1000)
                    {
                        TargetChar = LastPressChar + TargetChar;
                    }

                    List<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(TargetChar, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (Group.Count > 0)
                    {
                        if (SelectedItem != null)
                        {
                            if (Group.Any((Item) => Item == SelectedItem))
                            {
                                int NextIndex = Group.IndexOf(SelectedItem);

                                if (NextIndex < Group.Count - 1)
                                {
                                    SelectedItem = Group[NextIndex + 1];
                                    ItemPresenter.ScrollIntoViewSmoothly(SelectedItem);
                                }
                                else
                                {
                                    SelectedItem = Group[0];
                                    ItemPresenter.ScrollIntoViewSmoothly(SelectedItem);
                                }
                            }
                            else
                            {
                                SelectedItem = Group[0];
                                ItemPresenter.ScrollIntoViewSmoothly(SelectedItem);
                            }
                        }
                        else
                        {
                            SelectedItem = Group[0];
                            ItemPresenter.ScrollIntoViewSmoothly(SelectedItem);
                        }
                    }

                    LastPressChar = TargetChar;
                    LastPressTime = DateTimeOffset.Now;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error happened in NavigateToStorageItem: {ex.Message}");
                }
            }
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 关闭右键菜单
        /// </summary>
        private void Restore()
        {
            FileFlyout.Hide();
            FolderFlyout.Hide();
            EmptyFlyout.Hide();
            MixedFlyout.Hide();
            HiddenItemFlyout.Hide();
        }

        private async Task Ctrl_Z_Click()
        {
            if (OperationRecorder.Current.Value.Count > 0)
            {
                await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Undoing")).ConfigureAwait(true);

                try
                {
                    foreach (string Action in OperationRecorder.Current.Value.Pop())
                    {
                        string[] SplitGroup = Action.Split("||", StringSplitOptions.RemoveEmptyEntries);

                        switch (SplitGroup[1])
                        {
                            case "Move":
                                {
                                    if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[3]))
                                    {
                                        StorageFolder OriginFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[0]));

                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], OriginFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(File, OriginFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }

                                                    break;
                                                }
                                            case "Folder":
                                                {
                                                    if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFolder Folder)
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(Folder, OriginFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }

                                                    break;
                                                }
                                        }
                                    }
                                    else if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[0]))
                                    {
                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], FileControlInstance.CurrentFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[3]));

                                                        if ((await TargetFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(File, FileControlInstance.CurrentFolder, (s, arg) =>
                                                            {
                                                                FileControlInstance.ProBar.IsIndeterminate = false;
                                                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            throw new FileNotFoundException();
                                                        }
                                                    }

                                                    break;
                                                }
                                            case "Folder":
                                                {
                                                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);

                                                    await FullTrustProcessController.Current.MoveAsync(Folder, FileControlInstance.CurrentFolder, (s, arg) =>
                                                    {
                                                        FileControlInstance.ProBar.IsIndeterminate = false;
                                                        FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                    }, true).ConfigureAwait(true);

                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        StorageFolder OriginFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[0]));

                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], OriginFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        StorageFile File = await StorageFile.GetFileFromPathAsync(SplitGroup[3]);

                                                        await FullTrustProcessController.Current.MoveAsync(File, OriginFolder, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }

                                                    break;
                                                }
                                            case "Folder":
                                                {
                                                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);

                                                    await FullTrustProcessController.Current.MoveAsync(Folder, OriginFolder, (s, arg) =>
                                                    {
                                                        FileControlInstance.ProBar.IsIndeterminate = false;
                                                        FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                    }, true).ConfigureAwait(true);

                                                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                    {
                                                        await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                    }

                                                    break;
                                                }
                                        }
                                    }

                                    break;
                                }
                            case "Copy":
                                {
                                    if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[3]))
                                    {
                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(File, true, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        throw new FileNotFoundException();
                                                    }

                                                    break;
                                                }
                                            case "Folder":
                                                {
                                                    if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFolder Folder)
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(Folder, true, (s, arg) =>
                                                        {
                                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                        }, true).ConfigureAwait(true);
                                                    }

                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        await FullTrustProcessController.Current.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                        {
                                            FileControlInstance.ProBar.IsIndeterminate = false;
                                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                        }, true).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                        }
                                    }
                                    break;
                                }
                            case "Delete":
                                {
                                    if ((await FullTrustProcessController.Current.GetRecycleBinItemsAsync().ConfigureAwait(true)).FirstOrDefault((Item) => Item.OriginPath == SplitGroup[0]) is FileSystemStorageItemBase Item)
                                    {
                                        if (!await FullTrustProcessController.Current.RestoreItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = $"{Globalization.GetString("QueueDialog_RecycleBinRestoreError_Content")} {Environment.NewLine}{Item.Name}",
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                            Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    break;
                                }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    List<IStorageItem> TempItemList = new List<IStorageItem>(SelectedItems.Count);

                    foreach (FileSystemStorageItemBase Item in SelectedItems.Where((Item) => !(Item is HyperlinkStorageItem || Item is HiddenStorageItem)))
                    {
                        if (await Item.GetStorageItem().ConfigureAwait(true) is IStorageItem It)
                        {
                            TempItemList.Add(It);
                        }
                    }

                    if (TempItemList.Count > 0)
                    {
                        Package.SetStorageItems(TempItemList, false);
                    }

                    List<FileSystemStorageItemBase> NotStorageItems = SelectedItems.Where((Item) => Item is HyperlinkStorageItem || Item is HiddenStorageItem).ToList();

                    if (NotStorageItems.Count > 0)
                    {
                        StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TransferNotStorageItem</head>");

                        foreach (FileSystemStorageItemBase Item in NotStorageItems)
                        {
                            Builder.Append($"<p>{Item.Path}</p>");
                        }

                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
                    }

                    Clipboard.SetContent(Package);

                    FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            try
            {
                DataPackageView Package = Clipboard.GetContent();

                if (Package.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> ItemList = await Package.GetStorageItemsAsync();

                    if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (ItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == FileControlInstance.CurrentFolder.Path))
                        {
                            return;
                        }

                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            if (FileControlInstance.IsNetworkDevice)
                            {
                                foreach (IStorageItem NewItem in ItemList)
                                {
                                    if (NewItem is StorageFile File)
                                    {
                                        await File.MoveAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);

                                        FileCollection.Add(new FileSystemStorageItemBase(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                    }
                                    else if (NewItem is StorageFolder Folder)
                                    {
                                        StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);

                                        await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                        FileCollection.Add(new FileSystemStorageItemBase(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            FileControlInstance.CurrentNode.HasUnrealizedChildren = true;

                                            if (FileControlInstance.CurrentNode.IsExpanded)
                                            {
                                                FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                                {
                                                    Content = new TreeViewNodeContent(NewFolder),
                                                    HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                await FullTrustProcessController.Current.MoveAsync(ItemList, FileControlInstance.CurrentFolder, (s, arg) =>
                                {
                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                }).ConfigureAwait(true);
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (FileCaputureException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (InvalidOperationException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                    else if (Package.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                    {
                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            if (FileControlInstance.IsNetworkDevice)
                            {
                                foreach (IStorageItem NewItem in ItemList)
                                {
                                    if (NewItem is StorageFile File)
                                    {
                                        StorageFile NewFile = await File.CopyAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);

                                        FileCollection.Add(new FileSystemStorageItemBase(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                    }
                                    else if (NewItem is StorageFolder Folder)
                                    {
                                        StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);

                                        await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                        FileCollection.Add(new FileSystemStorageItemBase(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            FileControlInstance.CurrentNode.HasUnrealizedChildren = true;

                                            if (FileControlInstance.CurrentNode.IsExpanded)
                                            {
                                                FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                                {
                                                    Content = new TreeViewNodeContent(NewFolder),
                                                    HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                });
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                await FullTrustProcessController.Current.CopyAsync(ItemList, FileControlInstance.CurrentFolder, (s, arg) =>
                                {
                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                }).ConfigureAwait(true);
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (InvalidOperationException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }

                if (Package.Contains(StandardDataFormats.Html))
                {
                    if (FileControlInstance.IsNetworkDevice)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_NotAllowInNetwordDevice_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(false);
                        return;
                    }

                    string Html = await Package.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                    {
                        HtmlNodeCollection BodyNode = Document.DocumentNode.SelectNodes("/p");
                        List<string> LinkItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                        if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                        {
                            if (LinkItemsPath.All((Item) => Path.GetDirectoryName(Item) == FileControlInstance.CurrentFolder.Path))
                            {
                                return;
                            }

                            await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, FileControlInstance.CurrentFolder.Path, (s, arg) =>
                                {
                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                }).ConfigureAwait(true);
                            }
                            catch (FileNotFoundException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (FileCaputureException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);

                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                        else if (Package.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                        {
                            await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, FileControlInstance.CurrentFolder.Path, (s, arg) =>
                                {
                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                }).ConfigureAwait(true);
                            }
                            catch (FileNotFoundException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            finally
            {
                await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);
                FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    List<IStorageItem> TempItemList = new List<IStorageItem>(SelectedItems.Count);
                    foreach (FileSystemStorageItemBase Item in SelectedItems.Where((Item) => !(Item is HyperlinkStorageItem || Item is HiddenStorageItem)))
                    {
                        if (await Item.GetStorageItem().ConfigureAwait(true) is IStorageItem It)
                        {
                            TempItemList.Add(It);
                        }
                    }

                    if (TempItemList.Count > 0)
                    {
                        Package.SetStorageItems(TempItemList, false);
                    }

                    List<FileSystemStorageItemBase> NotStorageItems = SelectedItems.Where((Item) => Item is HyperlinkStorageItem || Item is HiddenStorageItem).ToList();
                    if (NotStorageItems.Count > 0)
                    {
                        StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TransferNotStorageItem</head>");

                        foreach (FileSystemStorageItemBase Item in NotStorageItems)
                        {
                            Builder.Append($"<p>{Item.Path}</p>");
                        }

                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
                    }

                    Clipboard.SetContent(Package);

                    FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                    SelectedItems.ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Count > 0)
            {
                if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                Retry:
                    try
                    {
                        List<string> PathList = SelectedItems.Select((Item) => Item.Path).ToList();

                        await FullTrustProcessController.Current.DeleteAsync(PathList, true, (s, arg) =>
                        {
                            FileControlInstance.ProBar.IsIndeterminate = false;
                            FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                        }).ConfigureAwait(true);

                        if (FileControlInstance.IsNetworkDevice)
                        {
                            foreach (FileSystemStorageItemBase Item in FileCollection.Where((Item) => PathList.Contains(Item.Path)).ToList())
                            {
                                FileCollection.Remove(Item);

                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                {
                                    if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == Item.Path) is TreeViewNode Node)
                                    {
                                        FileControlInstance.CurrentNode.Children.Remove(Node);
                                    }
                                }
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(true);
                    }
                    catch (FileCaputureException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                            {
                                goto Retry;
                            }
                            else
                            {
                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteFailUnexpectError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }

                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
                }
                else
                {
                    DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

                    if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            List<string> PathList = SelectedItems.Select((Item) => Item.Path).ToList();

                            await FullTrustProcessController.Current.DeleteAsync(PathList, QueueContenDialog.IsPermanentDelete, (s, arg) =>
                            {
                                FileControlInstance.ProBar.IsIndeterminate = false;
                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                            }).ConfigureAwait(true);

                            if (FileControlInstance.IsNetworkDevice)
                            {
                                foreach (FileSystemStorageItemBase Item in FileCollection.Where((Item) => PathList.Contains(Item.Path)).ToList())
                                {
                                    FileCollection.Remove(Item);

                                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                                    {
                                        if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == Item.Path) is TreeViewNode Node)
                                        {
                                            FileControlInstance.CurrentNode.Children.Remove(Node);
                                        }
                                    }
                                }
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(true);
                        }
                        catch (FileCaputureException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (InvalidOperationException)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DeleteFailUnexpectError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }

                        await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
                    }
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Count > 0)
            {
                if (SelectedItems.Count > 1)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RenameNumError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    RenameDialog dialog = new RenameDialog(SelectedItem);

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (FileControlInstance.IsNetworkDevice)
                        {
                            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                            {
                                try
                                {
                                    await File.RenameAsync(dialog.DesireName);
                                    await SelectedItem.Replace(File.Path).ConfigureAwait(true);
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                catch (FileLoadException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                    };

                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                catch
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        await File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
                                        await SelectedItem.Replace(File.Path).ConfigureAwait(true);
                                    }
                                }
                            }
                            else if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                            {
                                try
                                {
                                    string OldPath = Folder.Path;

                                    await Folder.RenameAsync(dialog.DesireName);

                                    await SelectedItem.Replace(Folder.Path).ConfigureAwait(true);

                                    if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.Children.Select((Item) => Item.Content as TreeViewNodeContent).FirstOrDefault((Item) => Item.Path == OldPath) is TreeViewNodeContent Content)
                                    {
                                        Content.Update(Folder);
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                catch (FileLoadException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_FolderOccupied_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                    };

                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                catch
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        string OldPath = Folder.Path;

                                        await Folder.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                                        await SelectedItem.Replace(Folder.Path).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.Children.Select((Item) => Item.Content as TreeViewNodeContent).FirstOrDefault((Item) => Item.Path == Folder.Path) is TreeViewNodeContent Content)
                                        {
                                            Content.Update(Folder);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                        }
                        else
                        {
                            if (WIN_Native_API.CheckExist(Path.Combine(Path.GetDirectoryName(SelectedItem.Path), dialog.DesireName)))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) != ContentDialogResult.Primary)
                                {
                                    return;
                                }
                            }

                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.RenameAsync(SelectedItem.Path, dialog.DesireName).ConfigureAwait(true);
                            }
                            catch (FileLoadException)
                            {
                                QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                };

                                _ = await LoadExceptionDialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                            }
                            catch
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile ShareFile = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await ShareFile.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                return;
            }

            IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

            if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
            {
                BluetoothUI Bluetooth = new BluetoothUI();
                if ((await Bluetooth.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer(ShareFile);

                    _ = await FileTransfer.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_OpenBluetooth_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MixZip.IsEnabled = true;

            if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
            {
                if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                {
                    if (SelectedItems.All((Item) => Item.Type == ".zip"))
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Decompression");
                    }
                    else if (SelectedItems.All((Item) => Item.Type != ".zip"))
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Compression");
                    }
                    else
                    {
                        MixZip.IsEnabled = false;
                    }
                }
                else
                {
                    if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type == ".zip"))
                    {
                        MixZip.IsEnabled = false;
                    }
                    else
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Compression");
                    }
                }
            }
            else
            {
                MixZip.Label = Globalization.GetString("Operate_Text_Compression");
            }

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (Item.StorageType == StorageItemTypes.File)
                {
                    FileTool.IsEnabled = true;
                    FileEdit.IsEnabled = false;
                    FileShare.IsEnabled = true;
                    Zip.IsEnabled = true;

                    ChooseOtherApp.IsEnabled = true;
                    RunWithSystemAuthority.IsEnabled = false;

                    Zip.Label = Globalization.GetString("Operate_Text_Compression");

                    switch (Item.Type)
                    {
                        case ".zip":
                            {
                                Zip.Label = Globalization.GetString("Operate_Text_Decompression");
                                break;
                            }
                        case ".mp4":
                        case ".wmv":
                            {
                                FileEdit.IsEnabled = true;
                                break;
                            }
                        case ".mkv":
                        case ".m4a":
                        case ".mov":
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".alac":
                        case ".png":
                        case ".bmp":
                        case ".jpg":
                        case ".heic":
                        case ".gif":
                        case ".tiff":
                            {
                                FileEdit.IsEnabled = true;
                                Transcode.IsEnabled = true;
                                break;
                            }
                        case ".exe":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                RunWithSystemAuthority.IsEnabled = true;
                                break;
                            }
                        case ".bat":
                            {
                                RunWithSystemAuthority.IsEnabled = true;
                                break;
                            }
                        case ".lnk":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                RunWithSystemAuthority.IsEnabled = true;
                                FileTool.IsEnabled = false;
                                FileEdit.IsEnabled = false;
                                FileShare.IsEnabled = false;
                                Zip.IsEnabled = false;
                                break;
                            }
                    }
                }
            }

            string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

            if (SelectedItems.Count > 0)
            {
                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}";
                }
                else
                {
                    StatusTips.Text += $"  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}";
                }
            }
            else
            {
                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = StatusTipsSplit[0];
                }
            }
        }

        private void ViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                SelectedItem = null;
                FileControlInstance.IsSearchOrPathBoxFocused = false;
            }
        }

        private void ViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            if (SelectedItems.Any((Item) => Item is HiddenStorageItem))
                            {
                                MixZip.IsEnabled = false;
                            }
                            else
                            {
                                MixZip.IsEnabled = true;
                            }

                            ItemPresenter.ContextFlyout = MixedFlyout;
                        }
                        else
                        {
                            if (Context is HiddenStorageItem)
                            {
                                ItemPresenter.ContextFlyout = HiddenItemFlyout;
                            }
                            else
                            {
                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                            }

                            SelectedItem = Context;
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            ItemPresenter.ContextFlyout = EmptyFlyout;
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    if (SelectedItems.Any((Item) => Item is HiddenStorageItem))
                                    {
                                        MixZip.IsEnabled = false;
                                    }
                                    else
                                    {
                                        MixZip.IsEnabled = true;
                                    }

                                    ItemPresenter.ContextFlyout = MixedFlyout;
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HiddenStorageItem)
                                        {
                                            ItemPresenter.ContextFlyout = HiddenItemFlyout;
                                        }
                                        else
                                        {
                                            ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HiddenStorageItem)
                                            {
                                                ItemPresenter.ContextFlyout = HiddenItemFlyout;
                                            }
                                            else
                                            {
                                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            ItemPresenter.ContextFlyout = EmptyFlyout;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                ItemPresenter.ContextFlyout = EmptyFlyout;
                            }
                        }
                    }
                }
            }

            e.Handled = true;
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile Item = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await Item.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                return;
            }

            if (Item.FileType == ".zip")
            {
                await UnZipAsync(Item).ConfigureAwait(true);
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                    }
                    else
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                    }

                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task<StorageFolder> UnZipAsync(StorageFile ZFile)
        {
            StorageFolder NewFolder = null;
            using (Stream ZipFileStream = await ZFile.OpenStreamForReadAsync().ConfigureAwait(true))
            using (ZipFile ZipEntries = new ZipFile(ZipFileStream))
            {
                ZipEntries.IsStreamOwner = false;

                if (ZipEntries.Count == 0)
                {
                    return null;
                }

                try
                {
                    if (ZipEntries[0].IsCrypted)
                    {
                        ZipDialog Dialog = new ZipDialog(false);
                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                        {
                            await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);
                            ZipEntries.Password = Dialog.Password;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);
                    }

                    NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Path.GetFileNameWithoutExtension(ZFile.Name), CreationCollisionOption.OpenIfExists);

                    foreach (ZipEntry Entry in ZipEntries)
                    {
                        using (Stream ZipEntryStream = ZipEntries.GetInputStream(Entry))
                        {
                            StorageFile NewFile = null;

                            if (Entry.Name.Contains("/"))
                            {
                                string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                                StorageFolder TempFolder = NewFolder;
                                for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                                {
                                    TempFolder = await TempFolder.CreateFolderAsync(SplitFolderPath[i], CreationCollisionOption.OpenIfExists);
                                }

                                if (Entry.Name.Last() == '/')
                                {
                                    await TempFolder.CreateFolderAsync(SplitFolderPath.Last(), CreationCollisionOption.OpenIfExists);
                                    continue;
                                }
                                else
                                {
                                    NewFile = await TempFolder.CreateFileAsync(SplitFolderPath.Last(), CreationCollisionOption.ReplaceExisting);
                                }
                            }
                            else
                            {
                                NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                            }

                            using (Stream NewFileStream = await NewFile.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await ZipEntryStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                catch (Exception e)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + e.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
                }
            }

            return NewFolder;
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="FileList">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="EnableCryption">是否启用加密</param>
        /// <param name="Size">AES加密密钥长度</param>
        /// <param name="Password">密码</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(IStorageItem ZipTarget, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                StorageFile Newfile = await FileControlInstance.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);

                using (Stream NewFileStream = await Newfile.OpenStreamForWriteAsync().ConfigureAwait(true))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.IsStreamOwner = false;
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;

                    if (EnableCryption)
                    {
                        OutputStream.Password = Password;
                    }

                    try
                    {
                        if (ZipTarget is StorageFile ZipFile)
                        {
                            if (EnableCryption)
                            {
                                using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                {
                                    ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        AESKeySize = (int)Size,
                                        IsCrypted = true,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                }
                            }
                            else
                            {
                                using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                {
                                    ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                }
                            }
                        }
                        else if (ZipTarget is StorageFolder ZipFolder)
                        {
                            await ZipFolderCore(ZipFolder, OutputStream, ZipFolder.Name, EnableCryption, Size, Password).ConfigureAwait(true);
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                        OutputStream.Finish();
                    }
                    catch (Exception e)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="FileList">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="EnableCryption">是否启用加密</param>
        /// <param name="Size">AES加密密钥长度</param>
        /// <param name="Password">密码</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> ZipItemGroup, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                StorageFile Newfile = await FileControlInstance.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);

                using (Stream NewFileStream = await Newfile.OpenStreamForWriteAsync().ConfigureAwait(true))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.IsStreamOwner = false;
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;

                    if (EnableCryption)
                    {
                        OutputStream.Password = Password;
                    }

                    try
                    {
                        foreach (FileSystemStorageItemBase StorageItem in ZipItemGroup)
                        {
                            if (await StorageItem.GetStorageItem().ConfigureAwait(true) is StorageFile ZipFile)
                            {
                                if (EnableCryption)
                                {
                                    using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                    {
                                        ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                        {
                                            DateTime = DateTime.Now,
                                            AESKeySize = (int)Size,
                                            IsCrypted = true,
                                            CompressionMethod = CompressionMethod.Deflated,
                                            Size = FileStream.Length
                                        };

                                        OutputStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                    }
                                }
                                else
                                {
                                    using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                    {
                                        ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                        {
                                            DateTime = DateTime.Now,
                                            CompressionMethod = CompressionMethod.Deflated,
                                            Size = FileStream.Length
                                        };

                                        OutputStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                    }
                                }
                            }
                            else if (await StorageItem.GetStorageItem().ConfigureAwait(true) is StorageFolder ZipFolder)
                            {
                                await ZipFolderCore(ZipFolder, OutputStream, ZipFolder.Name, EnableCryption, Size, Password).ConfigureAwait(true);
                            }
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                        OutputStream.Finish();
                    }
                    catch (Exception e)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        private async Task ZipFolderCore(StorageFolder Folder, ZipOutputStream OutputStream, string BaseFolderName, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            IReadOnlyList<IStorageItem> ItemsCollection = await Folder.GetItemsAsync();

            if (ItemsCollection.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    ZipEntry NewEntry = new ZipEntry(BaseFolderName);
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                foreach (IStorageItem Item in ItemsCollection)
                {
                    if (Item is StorageFolder InnerFolder)
                    {
                        if (EnableCryption)
                        {
                            await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}", true, Size, Password).ConfigureAwait(false);
                        }
                        else
                        {
                            await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}").ConfigureAwait(false);
                        }
                    }
                    else if (Item is StorageFile InnerFile)
                    {
                        if (EnableCryption)
                        {
                            using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(true))
                            {
                                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{InnerFile.Name}")
                                {
                                    DateTime = DateTime.Now,
                                    AESKeySize = (int)Size,
                                    IsCrypted = true,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                                OutputStream.CloseEntry();
                            }
                        }
                        else
                        {
                            using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(true))
                            {
                                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{InnerFile.Name}")
                                {
                                    DateTime = DateTime.Now,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                                OutputStream.CloseEntry();
                            }
                        }
                    }
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (SettingControl.IsInputFromPrimaryButton && (e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Source)
            {
                if (!await Source.CheckExist().ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                    return;
                }

                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                switch (Source.FileType)
                {
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".alac":
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    StorageFile DestinationFile = await FileControlInstance.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.GenerateUniqueName);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp).ConfigureAwait(true);
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                            }

                            break;
                        }
                    case ".png":
                    case ".bmp":
                    case ".jpg":
                    case ".heic":
                    case ".tiff":
                        {
                            TranscodeImageDialog Dialog = null;
                            using (IRandomAccessStream OriginStream = await Source.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                            }

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Transcoding")).ConfigureAwait(true);

                                await GeneralTransformer.TranscodeFromImageAsync(Source, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                                await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);
                            }
                            break;
                        }
                }
            }
        }

        private async void FolderProperty_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder Device = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder;
            if (!await Device.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                return;
            }

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                if (QRTeachTip.IsOpen)
                {
                    QRTeachTip.IsOpen = false;
                }

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => WiFiProvider == null);
                }).ConfigureAwait(true);

                WiFiProvider = new WiFiShareProvider();
                WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;

                string Hash = Item.Path.ComputeMD5Hash();
                QRText.Text = WiFiProvider.CurrentUri + Hash;
                WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, Item.Path);

                QrCodeEncodingOptions options = new QrCodeEncodingOptions()
                {
                    DisableECI = true,
                    CharacterSet = "UTF-8",
                    Width = 250,
                    Height = 250,
                    ErrorCorrection = ErrorCorrectionLevel.Q
                };

                BarcodeWriter Writer = new BarcodeWriter
                {
                    Format = BarcodeFormat.QR_CODE,
                    Options = options
                };

                WriteableBitmap Bitmap = Writer.Write(QRText.Text);
                using (SoftwareBitmap PreTransImage = SoftwareBitmap.CreateCopyFromBuffer(Bitmap.PixelBuffer, BitmapPixelFormat.Bgra8, 250, 250))
                using (SoftwareBitmap TransferImage = ComputerVisionProvider.ExtendImageBorder(PreTransImage, Colors.White, 0, 75, 75, 0))
                {
                    SoftwareBitmapSource Source = new SoftwareBitmapSource();
                    QRImage.Source = Source;
                    await Source.SetBitmapAsync(TransferImage);
                }

                await Task.Delay(500).ConfigureAwait(true);

                QRTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                QRTeachTip.IsOpen = true;

                await WiFiProvider.StartToListenRequest().ConfigureAwait(false);
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
            }
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_WiFiError_Content") + e.Message,
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            });
        }

        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(QRText.Text);
            Clipboard.SetContent(Package);
        }

        private async void UseSystemFileMananger_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
        }

        private async void ParentProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                return;
            }

            if (FileControlInstance.CurrentFolder.Path == Path.GetPathRoot(FileControlInstance.CurrentFolder.Path))
            {
                if (CommonAccessCollection.HardDeviceList.FirstOrDefault((Device) => Device.Name == FileControlInstance.CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    PropertyDialog Dialog = new PropertyDialog(FileControlInstance.CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                PropertyDialog Dialog = new PropertyDialog(FileControlInstance.CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void AddToLibray_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFolder folder)
            {
                if (!await folder.CheckExist().ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                    return;
                }

                if (CommonAccessCollection.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    CommonAccessCollection.LibraryFolderList.Add(new LibraryFolder(folder, await folder.GetThumbnailBitmapAsync().ConfigureAwait(true)));
                    await SQLite.Current.SetLibraryPathAsync(folder.Path, LibraryType.UserCustom).ConfigureAwait(false);
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            try
            {
                StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), CreationCollisionOption.GenerateUniqueName);

                if (FileControlInstance.IsNetworkDevice)
                {
                    FileCollection.Add(new FileSystemStorageItemBase(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                    if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                    {
                        FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                        {
                            Content = new TreeViewNodeContent(NewFolder),
                            HasUnrealizedChildren = false
                        });
                    }
                }
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateFolder_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        private async void EmptyFlyout_Opening(object sender, object e)
        {
            try
            {
                DataPackageView Package = Clipboard.GetContent();

                if (Package.Contains(StandardDataFormats.StorageItems))
                {
                    Paste.IsEnabled = true;
                }
                else if (Package.Contains(StandardDataFormats.Html))
                {
                    string Html = await Package.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                    {
                        Paste.IsEnabled = true;
                    }
                    else
                    {
                        Paste.IsEnabled = false;
                    }
                }
                else
                {
                    Paste.IsEnabled = false;
                }
            }
            catch
            {
                Paste.IsEnabled = false;
            }

            if (OperationRecorder.Current.Value.Count > 0)
            {
                Undo.IsEnabled = true;
            }
            else
            {
                Undo.IsEnabled = false;
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile ShareItem)
            {
                if (!await ShareItem.CheckExist().ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                    return;
                }

                DataTransferManager.GetForCurrentView().DataRequested += (s, args) =>
                {
                    DataPackage Package = new DataPackage();
                    Package.Properties.Title = ShareItem.DisplayName;
                    Package.Properties.Description = ShareItem.DisplayType;
                    Package.SetStorageItems(new StorageFile[] { ShareItem });
                    args.Request.Data = Package;
                };

                DataTransferManager.ShowShareUI();
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                return;
            }

            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
        }

        private async void ViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            FileControlInstance.IsSearchOrPathBoxFocused = false;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItemBase ReFile)
            {
                CoreVirtualKeyStates CtrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                CoreVirtualKeyStates ShiftState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                {
                    await EnterSelectedItem(ReFile).ConfigureAwait(false);
                }
            }
        }

        private async Task EnterSelectedItem(FileSystemStorageItemBase ReFile, bool RunAsAdministrator = false)
        {
            await FileControlInstance.CancelAddItemOperation().ConfigureAwait(true);

            if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
            {
                try
                {
                    if (WIN_Native_API.CheckIfHidden(ReFile.Path))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(false);

                        return;
                    }

                    if ((await TabTarget.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                    {
                        if (!await File.CheckExist().ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                            return;
                        }

                        string AdminExcuteProgram = null;
                        if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                        {
                            string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == File.FileType);
                            if (!string.IsNullOrEmpty(SaveUnit))
                            {
                                AdminExcuteProgram = SaveUnit.Split('|')[1];
                            }
                        }

                        if (!string.IsNullOrEmpty(AdminExcuteProgram) && AdminExcuteProgram != Globalization.GetString("RX_BuildIn_Viewer_Name"))
                        {
                            bool IsExcuted = false;
                            foreach (string Path in await SQLite.Current.GetProgramPickerRecordAsync(File.FileType).ConfigureAwait(true))
                            {
                                try
                                {
                                    StorageFile ExcuteFile = await StorageFile.GetFileFromPathAsync(Path);

                                    string AppName = Convert.ToString((await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"]);

                                    if (AppName == AdminExcuteProgram || ExcuteFile.DisplayName == AdminExcuteProgram)
                                    {
                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.RunAsync(Path, File.Path).ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }

                                        IsExcuted = true;

                                        break;
                                    }
                                }
                                catch (Exception)
                                {
                                    await SQLite.Current.DeleteProgramPickerRecordAsync(File.FileType, Path).ConfigureAwait(true);
                                }
                            }

                            if (!IsExcuted)
                            {
                                if ((await Launcher.FindFileHandlersAsync(File.FileType)).FirstOrDefault((Item) => Item.DisplayInfo.DisplayName == AdminExcuteProgram) is AppInfo Info)
                                {
                                    if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.OpenFailed)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                    Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                    PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (!await Launcher.LaunchFileAsync(File))
                                                    {
                                                        LauncherOptions options = new LauncherOptions
                                                        {
                                                            DisplayApplicationPicker = true
                                                        };
                                                        _ = await Launcher.LaunchFileAsync(File, options);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        if (Dialog.OpenFailed)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (!await Launcher.LaunchFileAsync(File))
                                                {
                                                    LauncherOptions options = new LauncherOptions
                                                    {
                                                        DisplayApplicationPicker = true
                                                    };
                                                    _ = await Launcher.LaunchFileAsync(File, options);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            switch (File.FileType.ToLower())
                            {
                                case ".jpg":
                                case ".png":
                                case ".bmp":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PhotoViewer), new Tuple<FileControl, string>(FileControlInstance, File.Name), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PhotoViewer), new Tuple<FileControl, string>(FileControlInstance, File.Name), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".mkv":
                                case ".mp4":
                                case ".mp3":
                                case ".flac":
                                case ".wma":
                                case ".wmv":
                                case ".m4a":
                                case ".mov":
                                case ".alac":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(MediaPlayer), File, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(MediaPlayer), File, new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".txt":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(TextViewer), new Tuple<FileControl, FileSystemStorageItemBase>(FileControlInstance, TabTarget), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(TextViewer), new Tuple<FileControl, FileSystemStorageItemBase>(FileControlInstance, TabTarget), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".pdf":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PdfReader), new Tuple<Frame, StorageFile>(FileControlInstance.Nav, File), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PdfReader), new Tuple<Frame, StorageFile>(FileControlInstance.Nav, File), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".exe":
                                case ".bat":
                                    {
                                    Retry:
                                        try
                                        {
                                            if (TabTarget is HyperlinkStorageItem Item)
                                            {
                                                if (Item.NeedRunAs || RunAsAdministrator)
                                                {
                                                    await FullTrustProcessController.Current.RunAsAdministratorAsync(Item.TargetPath, Item.Argument).ConfigureAwait(false);
                                                }
                                                else
                                                {
                                                    await FullTrustProcessController.Current.RunAsync(Item.TargetPath, Item.Argument).ConfigureAwait(false);
                                                }
                                            }
                                            else
                                            {
                                                if (RunAsAdministrator)
                                                {
                                                    await FullTrustProcessController.Current.RunAsAdministratorAsync(File.Path).ConfigureAwait(false);
                                                }
                                                else
                                                {
                                                    await FullTrustProcessController.Current.RunAsync(File.Path).ConfigureAwait(false);
                                                }
                                            }
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.OpenFailed)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                    Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                    PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (!await Launcher.LaunchFileAsync(File))
                                                    {
                                                        LauncherOptions options = new LauncherOptions
                                                        {
                                                            DisplayApplicationPicker = true
                                                        };
                                                        _ = await Launcher.LaunchFileAsync(File, options);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                    else if ((await TabTarget.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                    {
                        if (!await Folder.CheckExist().ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                            return;
                        }

                        if (Folder.Path.StartsWith((FileControlInstance.FolderTree.RootNodes[0].Content as TreeViewNodeContent).Path))
                        {
                            if (SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.DisplayItemsInFolder(Folder).ConfigureAwait(true);
                            }
                            else
                            {
                                if (FileControlInstance.CurrentNode == null)
                                {
                                    FileControlInstance.CurrentNode = FileControlInstance.FolderTree.RootNodes[0];
                                }

                                TreeViewNode TargetNode = await FileControlInstance.FolderTree.RootNodes[0].GetChildNodeAsync(new PathAnalysis(Folder.Path, (FileControlInstance.FolderTree.RootNodes[0].Content as TreeViewNodeContent).Path)).ConfigureAwait(true);

                                if (TargetNode != null)
                                {
                                    await FileControlInstance.DisplayItemsInFolder(TargetNode).ConfigureAwait(true);
                                }
                            }
                        }
                        else
                        {
                            await FileControlInstance.OpenTargetFolder(Folder).ConfigureAwait(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EnterSelectedItem error: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref TabTarget, null);
                }
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
            {
                VideoEditDialog Dialog = new VideoEditDialog(File);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{File.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                    await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference).ConfigureAwait(true);
                }
            }
        }

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                    await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding).ConfigureAwait(true);
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                ProgramPickerDialog Dialog = new ProgramPickerDialog(Item);
                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    if (Dialog.OpenFailed)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                            PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            if (!await Launcher.LaunchFileAsync(Item))
                            {
                                LauncherOptions options = new LauncherOptions
                                {
                                    DisplayApplicationPicker = true
                                };
                                _ = await Launcher.LaunchFileAsync(Item, options);
                            }
                        }
                    }
                    else if (Dialog.ContinueUseInnerViewer)
                    {
                        await EnterSelectedItem(SelectedItem).ConfigureAwait(false);
                    }
                }
            }
        }

        private async void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem != null)
            {
                await EnterSelectedItem(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Name, SortDirection.Descending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Name, SortDirection.Ascending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.ModifiedTime, SortDirection.Descending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.ModifiedTime, SortDirection.Ascending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Type, SortDirection.Descending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Type, SortDirection.Ascending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Size, SortDirection.Descending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);
                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }

            }
            else
            {
                SortCollectionGenerator.Current.ModifySortWay(SortTarget.Size, SortDirection.Ascending);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);
                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void QRTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
            WiFiProvider = null;
        }

        private async void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            NewFileDialog Dialog = new NewFileDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                try
                {
                    switch (Path.GetExtension(Dialog.NewFileName))
                    {
                        case ".zip":
                            {
                                _ = await SpecialTypeGenerator.Current.CreateZipAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true) ?? throw new UnauthorizedAccessException();
                                break;
                            }
                        case ".rtf":
                            {
                                _ = await SpecialTypeGenerator.Current.CreateRtfAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true) ?? throw new UnauthorizedAccessException();
                                break;
                            }
                        case ".xlsx":
                            {
                                _ = await SpecialTypeGenerator.Current.CreateExcelAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true) ?? throw new UnauthorizedAccessException();
                                break;
                            }
                        case ".lnk":
                            {
                                LinkOptionsDialog dialog = new LinkOptionsDialog();
                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (!await FullTrustProcessController.Current.CreateLink(Path.Combine(FileControlInstance.CurrentFolder.Path, Dialog.NewFileName), dialog.Path, dialog.Description, dialog.Argument).ConfigureAwait(true))
                                    {
                                        throw new UnauthorizedAccessException();
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                _ = await FileControlInstance.CurrentFolder.CreateFileAsync(Dialog.NewFileName, CreationCollisionOption.GenerateUniqueName) ?? throw new UnauthorizedAccessException();
                                break;
                            }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder Item = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

            if (!await Item.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                return;
            }

            ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                if (dialog.IsCryptionEnable)
                {
                    await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                }
                else
                {
                    await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                }
            }

            await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);
        }

        private void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Html))
            {
                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {FileControlInstance.CurrentFolder.DisplayName}";
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {FileControlInstance.CurrentFolder.DisplayName}";
                }

                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void Item_Drop(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref DropLock, 1) == 0)
            {
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        if (FileControlInstance.IsNetworkDevice)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_NotAllowInNetwordDevice_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(false);
                            return;
                        }

                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            HtmlNodeCollection BodyNode = Document.DocumentNode.SelectNodes("/p");
                            List<string> LinkItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                            if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                            {
                                StorageFolder TargetFolder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                                switch (e.AcceptedOperation)
                                {
                                    case DataPackageOperation.Copy:
                                        {
                                            await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                        Retry:
                                            try
                                            {
                                                await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
                                                {
                                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                }).ConfigureAwait(true);
                                            }
                                            catch (FileNotFoundException)
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                    {
                                                        goto Retry;
                                                    }
                                                    else
                                                    {
                                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }

                                            break;
                                        }
                                    case DataPackageOperation.Move:
                                        {
                                            await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                        Retry:
                                            try
                                            {
                                                await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
                                                {
                                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                }).ConfigureAwait(true);
                                            }
                                            catch (FileNotFoundException)
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (FileCaputureException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                            catch (InvalidOperationException)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                    {
                                                        goto Retry;
                                                    }
                                                    else
                                                    {
                                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                            catch (Exception)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                            }

                                            break;
                                        }
                                }
                            }
                        }
                    }

                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                        if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                        {
                            StorageFolder TargetFolder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                            if (DragItemList.Contains(TargetFolder))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DragIncludeFolderError"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                                return;
                            }

                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            if (FileControlInstance.IsNetworkDevice)
                                            {
                                                foreach (IStorageItem DragItem in DragItemList)
                                                {
                                                    if (DragItem is StorageFile File)
                                                    {
                                                        await File.CopyAsync(TargetFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                                                    }
                                                    else if (DragItem is StorageFolder Folder)
                                                    {
                                                        StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);
                                                        await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                        {
                                                            if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == TargetFolder.Path) is TreeViewNode Node)
                                                            {
                                                                Node.HasUnrealizedChildren = true;

                                                                if (Node.IsExpanded)
                                                                {
                                                                    Node.Children.Add(new TreeViewNode
                                                                    {
                                                                        Content = new TreeViewNodeContent(NewFolder),
                                                                        HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                                    });
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                await FullTrustProcessController.Current.CopyAsync(DragItemList, TargetFolder, (s, arg) =>
                                                {
                                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                }).ConfigureAwait(true);

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                                case DataPackageOperation.Move:
                                    {
                                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            if (FileControlInstance.IsNetworkDevice)
                                            {
                                                foreach (IStorageItem DragItem in DragItemList)
                                                {
                                                    if (DragItem is StorageFile File)
                                                    {
                                                        await File.MoveAsync(TargetFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                                                    }
                                                    else if (DragItem is StorageFolder Folder)
                                                    {
                                                        StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);

                                                        await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                                        if (FileCollection.FirstOrDefault((Item) => Item.Path == Folder.Path) is FileSystemStorageItemBase RemoveItem)
                                                        {
                                                            FileCollection.Remove(RemoveItem);
                                                        }

                                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                        {
                                                            if (FileControlInstance.CurrentNode.IsExpanded)
                                                            {
                                                                if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == Folder.Path) is TreeViewNode RemoveNode)
                                                                {
                                                                    FileControlInstance.CurrentNode.Children.Remove(RemoveNode);
                                                                }
                                                            }

                                                            FileControlInstance.CurrentNode.HasUnrealizedChildren = (await FileControlInstance.CurrentFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0;

                                                            if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == TargetFolder.Path) is TreeViewNode Node)
                                                            {
                                                                Node.HasUnrealizedChildren = true;

                                                                if (Node.IsExpanded)
                                                                {
                                                                    Node.Children.Add(new TreeViewNode
                                                                    {
                                                                        Content = new TreeViewNodeContent(NewFolder),
                                                                        HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                                    });
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                await FullTrustProcessController.Current.MoveAsync(DragItemList, TargetFolder, (s, arg) =>
                                                {
                                                    FileControlInstance.ProBar.IsIndeterminate = false;
                                                    FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                                }).ConfigureAwait(true);
                                            }
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (FileCaputureException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    e.Handled = true;
                    Deferral.Complete();
                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);

                    _ = Interlocked.Exchange(ref DropLock, 0);
                }
            }
        }


        private async void ViewControl_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count != 0)
            {
                List<IStorageItem> TempList = new List<IStorageItem>(e.Items.Count);
                List<FileSystemStorageItemBase> DragList = e.Items.Select((Item) => Item as FileSystemStorageItemBase).ToList();

                foreach (FileSystemStorageItemBase StorageItem in DragList.Where((Item) => !(Item is HyperlinkStorageItem || Item is HiddenStorageItem)))
                {
                    if (ItemPresenter.ContainerFromItem(StorageItem) is SelectorItem SItem && SItem.ContentTemplateRoot.FindChildOfType<TextBox>() is TextBox NameEditBox)
                    {
                        NameEditBox.Visibility = Visibility.Collapsed;
                    }

                    if (await StorageItem.GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                    {
                        TempList.Add(Item);
                    }
                }

                if (TempList.Count > 0)
                {
                    e.Data.SetStorageItems(TempList, false);
                }

                List<FileSystemStorageItemBase> NotStorageItems = DragList.Where((Item) => Item is HyperlinkStorageItem || Item is HiddenStorageItem).ToList();
                if (NotStorageItems.Count > 0)
                {
                    StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TransferNotStorageItem</head>");

                    foreach (FileSystemStorageItemBase Item in NotStorageItems)
                    {
                        if (ItemPresenter.ContainerFromItem(Item) is SelectorItem SItem && SItem.ContentTemplateRoot.FindChildOfType<TextBox>() is TextBox NameEditBox)
                        {
                            NameEditBox.Visibility = Visibility.Collapsed;
                        }

                        Builder.Append($"<p>{Item.Path}</p>");
                    }

                    e.Data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
                }
            }
        }

        private async void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;
                args.ItemContainer.Drop -= Item_Drop;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;

                if (args.ItemContainer.ContentTemplateRoot.FindChildOfType<TextBox>() is TextBox NameEditBox)
                {
                    NameEditBox.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (args.Item is FileSystemStorageItemBase Item)
                {
                    if (Item.StorageType == StorageItemTypes.File)
                    {
                        await Item.LoadMoreProperty().ConfigureAwait(true);
                    }

                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        args.ItemContainer.AllowDrop = true;
                        args.ItemContainer.Drop += Item_Drop;
                        args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                    }

                    if (Item is HiddenStorageItem)
                    {
                        args.ItemContainer.AllowDrop = false;
                        args.ItemContainer.CanDrag = false;
                    }

                    args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                }
            }
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                if (sender is SelectorItem)
                {
                    FileSystemStorageItemBase Item = (sender as SelectorItem).Content as FileSystemStorageItemBase;

                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Item.Name}";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Item.Name}";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                }
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable && e.KeyModifiers != VirtualKeyModifiers.Control && e.KeyModifiers != VirtualKeyModifiers.Shift)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    SelectedItem = Item;
                }
            }
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref ViewDropLock, 1) == 0)
            {
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        if (FileControlInstance.IsNetworkDevice)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_NotAllowInNetwordDevice_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(false);
                            return;
                        }

                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            HtmlNodeCollection BodyNode = Document.DocumentNode.SelectNodes("/p");
                            List<string> LinkItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                            StorageFolder TargetFolder = FileControlInstance.CurrentFolder;

                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
                                            {
                                                FileControlInstance.ProBar.IsIndeterminate = false;
                                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                            }).ConfigureAwait(true);
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                                case DataPackageOperation.Move:
                                    {
                                        if (LinkItemsPath.All((Item) => Path.GetDirectoryName(Item) == TargetFolder.Path))
                                        {
                                            return;
                                        }

                                        await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
                                            {
                                                FileControlInstance.ProBar.IsIndeterminate = false;
                                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                            }).ConfigureAwait(true);
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (FileCaputureException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                            }
                        }
                    }

                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                        StorageFolder TargetFolder = FileControlInstance.CurrentFolder;

                        if (DragItemList.Contains(TargetFolder))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DragIncludeFolderError"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            return;
                        }

                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        if (FileControlInstance.IsNetworkDevice)
                                        {
                                            foreach (IStorageItem DragItem in DragItemList)
                                            {
                                                if (DragItem is StorageFile File)
                                                {
                                                    StorageFile NewFile = await File.CopyAsync(TargetFolder, File.Name, NameCollisionOption.GenerateUniqueName);

                                                    FileCollection.Add(new FileSystemStorageItemBase(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                                }
                                                else if (DragItem is StorageFolder Folder)
                                                {
                                                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);
                                                    await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                                    FileCollection.Add(new FileSystemStorageItemBase(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                                                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                    {
                                                        FileControlInstance.CurrentNode.HasUnrealizedChildren = true;

                                                        if (FileControlInstance.CurrentNode.IsExpanded)
                                                        {
                                                            FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                                            {
                                                                Content = new TreeViewNodeContent(NewFolder),
                                                                HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                            });
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(DragItemList, TargetFolder, (s, arg) =>
                                            {
                                                FileControlInstance.ProBar.IsIndeterminate = false;
                                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                            }).ConfigureAwait(true);
                                        }
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                            {
                                                goto Retry;
                                            }
                                            else
                                            {
                                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }

                                    break;
                                }
                            case DataPackageOperation.Move:
                                {
                                    if (DragItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == TargetFolder.Path))
                                    {
                                        return;
                                    }

                                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        if (FileControlInstance.IsNetworkDevice)
                                        {
                                            foreach (IStorageItem DragItem in DragItemList)
                                            {
                                                if (DragItem is StorageFile File)
                                                {
                                                    await File.MoveAsync(TargetFolder, File.Name, NameCollisionOption.GenerateUniqueName);

                                                    FileCollection.Add(new FileSystemStorageItemBase(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                                }
                                                else if (DragItem is StorageFolder Folder)
                                                {
                                                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.GenerateUniqueName);

                                                    await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                                    FileCollection.Add(new FileSystemStorageItemBase(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                                                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                    {
                                                        if (TabViewContainer.ThisPage.TabViewControl.TabItems.Select((Tab) => ((Tab as Microsoft.UI.Xaml.Controls.TabViewItem)?.Content as Frame)?.Content as FileControl).Where((Control) => Control != null).FirstOrDefault((Control) => Control.CurrentFolder.Path == Path.GetDirectoryName(Folder.Path)) is FileControl Control && Control.IsNetworkDevice)
                                                        {
                                                            if (Control.CurrentNode.IsExpanded)
                                                            {
                                                                if (Control.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent).Path == Folder.Path) is TreeViewNode Node)
                                                                {
                                                                    Control.CurrentNode.Children.Remove(Node);
                                                                }
                                                            }

                                                            Control.CurrentNode.HasUnrealizedChildren = (await Control.CurrentFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0;

                                                            Refresh_Click(null, null);
                                                        }

                                                        FileControlInstance.CurrentNode.HasUnrealizedChildren = true;

                                                        if (FileControlInstance.CurrentNode.IsExpanded)
                                                        {
                                                            FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                                            {
                                                                Content = new TreeViewNodeContent(NewFolder),
                                                                HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                                                            });
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(DragItemList, TargetFolder, (s, arg) =>
                                            {
                                                FileControlInstance.ProBar.IsIndeterminate = false;
                                                FileControlInstance.ProBar.Value = arg.ProgressPercentage;
                                            }).ConfigureAwait(true);
                                        }
                                    }
                                    catch (FileCaputureException)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                            {
                                                goto Retry;
                                            }
                                            else
                                            {
                                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }

                                    break;
                                }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DropFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    e.Handled = true;
                    Deferral.Complete();
                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);
                    _ = Interlocked.Exchange(ref ViewDropLock, 0);
                }
            }
        }

        private void ViewControl_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            if (SelectedItems.Any((Item) => Item is HiddenStorageItem))
                            {
                                MixZip.IsEnabled = false;
                            }
                            else
                            {
                                MixZip.IsEnabled = true;
                            }

                            ItemPresenter.ContextFlyout = MixedFlyout;
                        }
                        else
                        {
                            if (Context is HiddenStorageItem)
                            {
                                ItemPresenter.ContextFlyout = HiddenItemFlyout;
                            }
                            else
                            {
                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                            }

                            SelectedItem = Context;
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            ItemPresenter.ContextFlyout = EmptyFlyout;
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    if (SelectedItems.Any((Item) => Item is HiddenStorageItem))
                                    {
                                        MixZip.IsEnabled = false;
                                    }
                                    else
                                    {
                                        MixZip.IsEnabled = true;
                                    }

                                    ItemPresenter.ContextFlyout = MixedFlyout;
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HiddenStorageItem)
                                        {
                                            ItemPresenter.ContextFlyout = HiddenItemFlyout;
                                        }
                                        else
                                        {
                                            ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HiddenStorageItem)
                                            {
                                                ItemPresenter.ContextFlyout = HiddenItemFlyout;
                                            }
                                            else
                                            {
                                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            ItemPresenter.ContextFlyout = EmptyFlyout;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                ItemPresenter.ContextFlyout = EmptyFlyout;
                            }
                        }
                    }
                }
            }
        }

        private async void MixZip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Any((Item) => Item is HyperlinkStorageItem))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            foreach (FileSystemStorageItemBase Item in SelectedItems)
            {
                if (Item.StorageType == StorageItemTypes.Folder)
                {
                    StorageFolder Folder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                    if (!await Folder.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                    if (!await File.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        return;
                    }
                }
            }

            bool IsCompress = false;
            if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
            {
                if (SelectedItems.All((Item) => Item.Type == ".zip"))
                {
                    IsCompress = false;
                }
                else if (SelectedItems.All((Item) => Item.Type != ".zip"))
                {
                    IsCompress = true;
                }
                else
                {
                    return;
                }
            }
            else if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.Folder))
            {
                IsCompress = true;
            }
            else
            {
                if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).All((Item) => Item.Type != ".zip"))
                {
                    IsCompress = true;
                }
                else
                {
                    return;
                }
            }

            if (IsCompress)
            {
                ZipDialog dialog = new ZipDialog(true, Globalization.GetString("Zip_Admin_Name_Text"));

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(SelectedItems, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                    }
                    else
                    {
                        await CreateZipAsync(SelectedItems, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                    }

                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(true);
                }
            }
            else
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems)
                {
                    StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                    await UnZipAsync(File).ConfigureAwait(true);
                }
            }

        }

        private async void TryUnlock_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.File)
            {
                try
                {
                    await FileControlInstance.LoadingActivation(true, Globalization.GetString("Progress_Tip_Unlock")).ConfigureAwait(true);

                    if (await FullTrustProcessController.Current.TryUnlockFileOccupy(Item.Path).ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Unlock_Success_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Unlock_Failure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                catch (FileNotFoundException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_FileNotFound_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                }
                catch (UnlockException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_NoLock_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_UnexpectedError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    await FileControlInstance.LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        private async void CalculateHash_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            try
            {
                if (HashTeachTip.IsOpen)
                {
                    HashTeachTip.IsOpen = false;
                }

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => HashCancellation == null);
                }).ConfigureAwait(true);

                if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
                {
                    Hash_Crc32.IsEnabled = false;
                    Hash_SHA1.IsEnabled = false;
                    Hash_SHA256.IsEnabled = false;
                    Hash_MD5.IsEnabled = false;

                    Hash_Crc32.Text = string.Empty;
                    Hash_SHA1.Text = string.Empty;
                    Hash_SHA256.Text = string.Empty;
                    Hash_MD5.Text = string.Empty;

                    await Task.Delay(500).ConfigureAwait(true);
                    HashTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                    HashTeachTip.IsOpen = true;

                    using (HashCancellation = new CancellationTokenSource())
                    {
                        var task1 = Item.ComputeSHA256Hash(HashCancellation.Token);
                        Hash_SHA256.IsEnabled = true;

                        var task2 = Item.ComputeCrc32Hash(HashCancellation.Token);
                        Hash_Crc32.IsEnabled = true;

                        var task4 = Item.ComputeMD5Hash(HashCancellation.Token);
                        Hash_MD5.IsEnabled = true;

                        var task3 = Item.ComputeSHA1Hash(HashCancellation.Token);
                        Hash_SHA1.IsEnabled = true;

                        Hash_MD5.Text = await task4.ConfigureAwait(true);
                        Hash_Crc32.Text = await task2.ConfigureAwait(true);
                        Hash_SHA1.Text = await task3.ConfigureAwait(true);
                        Hash_SHA256.Text = await task1.ConfigureAwait(true);
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(true);
                }
            }
            catch
            {
                Debug.WriteLine("Error: CalculateHash failed");
            }
            finally
            {
                HashCancellation = null;
            }
        }

        private void Hash_Crc32_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_Crc32.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_SHA1_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_SHA1.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_SHA256_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_SHA256.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_MD5_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_MD5.Text);
            Clipboard.SetContent(Package);
        }

        private void HashTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            HashCancellation?.Cancel();
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (await SQLite.Current.GetTerminalProfileByName(Convert.ToString(ApplicationData.Current.LocalSettings.Values["DefaultTerminal"])).ConfigureAwait(true) is TerminalProfile Profile)
            {
            Retry:
                try
                {
                    if (Profile.RunAsAdmin)
                    {
                        await FullTrustProcessController.Current.RunAsAdministratorAsync(Profile.Path, Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value == "[CurrentLocation]" ? FileControlInstance.CurrentFolder.Path : Mat.Value).ToArray()).ConfigureAwait(false);
                    }
                    else
                    {
                        await FullTrustProcessController.Current.RunAsync(Profile.Path, Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value == "[CurrentLocation]" ? FileControlInstance.CurrentFolder.Path : Mat.Value).ToArray()).ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException)
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                        {
                            goto Retry;
                        }
                        else
                        {
                            QueueContentDialog ErrorDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                await TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(Item.Path).ConfigureAwait(false);
            }
        }

        private void NameLabel_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            TextBlock NameLabel = (TextBlock)sender;

            if ((e.GetCurrentPoint(NameLabel).Properties.IsLeftButtonPressed || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) && SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (Item is HiddenStorageItem)
                    {
                        return;
                    }

                    if (SelectedItem == Item)
                    {
                        TimeSpan ClickSpan = DateTimeOffset.Now - LastClickTime;

                        if (ClickSpan.TotalMilliseconds > 1200 && ClickSpan.TotalMilliseconds < 2000)
                        {
                            NameLabel.Visibility = Visibility.Collapsed;
                            CurrentNameEditItem = Item;

                            if ((NameLabel.Parent as FrameworkElement).FindName("NameEditBox") is TextBox EditBox)
                            {
                                EditBox.Text = NameLabel.Text;
                                EditBox.Visibility = Visibility.Visible;
                                EditBox.Focus(FocusState.Programmatic);
                            }

                            FileControlInstance.IsSearchOrPathBoxFocused = true;
                        }
                    }

                    LastClickTime = DateTimeOffset.Now;
                }
            }
        }

        private async void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox NameEditBox = (TextBox)sender;

            if ((NameEditBox.Parent as FrameworkElement).FindName("NameLabel") is TextBlock NameLabel && CurrentNameEditItem != null)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(NameEditBox.Text) || !FileSystemItemNameChecker.IsValid(NameEditBox.Text))
                    {
                        InvalidNameTip.Target = NameLabel;
                        InvalidNameTip.IsOpen = true;
                        return;
                    }

                    if (CurrentNameEditItem.Name == NameEditBox.Text)
                    {
                        return;
                    }

                    if (FileControlInstance.IsNetworkDevice)
                    {
                        if ((await CurrentNameEditItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                        {
                            try
                            {
                                await File.RenameAsync(NameEditBox.Text);
                                await CurrentNameEditItem.Replace(File.Path).ConfigureAwait(true);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                            catch (FileLoadException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    await File.RenameAsync(NameEditBox.Text, NameCollisionOption.GenerateUniqueName);
                                    await CurrentNameEditItem.Replace(File.Path).ConfigureAwait(true);
                                }
                            }
                        }
                        else if ((await CurrentNameEditItem.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                        {
                            try
                            {
                                string OldPath = Folder.Path;

                                await Folder.RenameAsync(NameEditBox.Text);

                                await CurrentNameEditItem.Replace(Folder.Path).ConfigureAwait(true);

                                if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.Children.Select((Item) => Item.Content as TreeViewNodeContent).FirstOrDefault((Item) => Item.Path == OldPath) is TreeViewNodeContent Content)
                                {
                                    Content.Update(Folder);
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                            catch (FileLoadException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_FolderOccupied_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    string OldPath = Folder.Path;

                                    await Folder.RenameAsync(NameEditBox.Text, NameCollisionOption.GenerateUniqueName);

                                    await CurrentNameEditItem.Replace(Folder.Path).ConfigureAwait(true);

                                    if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.Children.Select((Item) => Item.Content as TreeViewNodeContent).FirstOrDefault((Item) => Item.Path == Folder.Path) is TreeViewNodeContent Content)
                                    {
                                        Content.Update(Folder);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                            };

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                            }
                        }
                    }
                    else
                    {
                        if (WIN_Native_API.CheckExist(Path.Combine(Path.GetDirectoryName(CurrentNameEditItem.Path), NameEditBox.Text)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await Dialog.ShowAsync().ConfigureAwait(true) != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }

                    Retry:
                        try
                        {
                            await FullTrustProcessController.Current.RenameAsync(CurrentNameEditItem.Path, NameEditBox.Text).ConfigureAwait(true);
                        }
                        catch (FileLoadException)
                        {
                            QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await LoadExceptionDialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (InvalidOperationException)
                        {
                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (await FullTrustProcessController.Current.SwitchToAdminMode().ConfigureAwait(true))
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                finally
                {
                    NameEditBox.Visibility = Visibility.Collapsed;

                    NameLabel.Visibility = Visibility.Visible;

                    LastClickTime = DateTimeOffset.MaxValue;

                    FileControlInstance.IsSearchOrPathBoxFocused = false;
                }
            }
        }

        private void GetFocus_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ItemPresenter.Focus(FocusState.Programmatic);
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(Item.Path)}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            await Ctrl_Z_Click().ConfigureAwait(false);
        }

        private async void RemoveHidden_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (await FullTrustProcessController.Current.RemoveHiddenAttribute(SelectedItem.Path).ConfigureAwait(true))
            {
                if (WIN_Native_API.GetStorageItems(SelectedItem.Path).FirstOrDefault() is FileSystemStorageItemBase Item)
                {
                    int Index = FileCollection.IndexOf(SelectedItem);

                    if (Index != -1)
                    {
                        FileCollection.Remove(SelectedItem);
                        FileCollection.Insert(Index, Item);
                    }
                    else
                    {
                        FileCollection.Add(Item);
                    }

                    ItemPresenter.UpdateLayout();

                    SelectedItem = Item;
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_RemoveHiddenError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private async void OpenHiddenItemExplorer_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await Launcher.LaunchFolderPathAsync(SelectedItem.Path))
            {
                await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(SelectedItem.Path));
            }
        }

        private void NameEditBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Any((Item) => Path.GetInvalidFileNameChars().Contains(Item)))
            {
                args.Cancel = true;

                if ((sender.Parent as FrameworkElement).FindName("NameLabel") is TextBlock NameLabel)
                {
                    InvalidCharTip.Target = NameLabel;
                    InvalidCharTip.IsOpen = true;
                }
            }
        }

        private void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortTarget.Name, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortTarget.ModifiedTime, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortTarget.Type, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortTarget.Size, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void Desc_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortDirection: SortDirection.Descending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void Asc_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            SortCollectionGenerator.Current.ModifySortWay(SortDirection: SortDirection.Ascending);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private void SortMenuFlyout_Opening(object sender, object e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                Desc.IsChecked = false;
                Asc.IsChecked = true;
            }
            else
            {
                Asc.IsChecked = false;
                Desc.IsChecked = true;
            }

            switch (SortCollectionGenerator.Current.SortTarget)
            {
                case SortTarget.Name:
                    {
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = true;
                        break;
                    }
                case SortTarget.Type:
                    {
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = true;
                        break;
                    }
                case SortTarget.ModifiedTime:
                    {
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = true;
                        break;
                    }
                case SortTarget.Size:
                    {
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = true;
                        break;
                    }
            }
        }

        private async void BottomCommandBar_Opening(object sender, object e)
        {
            BottomCommandBar.PrimaryCommands.Clear();
            BottomCommandBar.SecondaryCommands.Clear();

            if (SelectedItems.Count > 1)
            {
                AppBarButton CopyButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Copy),
                    Label = Globalization.GetString("Operate_Text_Copy")
                };
                CopyButton.Click += Copy_Click;
                BottomCommandBar.PrimaryCommands.Add(CopyButton);

                AppBarButton CutButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Cut),
                    Label = Globalization.GetString("Operate_Text_Cut")
                };
                CutButton.Click += Cut_Click;
                BottomCommandBar.PrimaryCommands.Add(CutButton);

                AppBarButton DeleteButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Delete),
                    Label = Globalization.GetString("Operate_Text_Delete")
                };
                DeleteButton.Click += Delete_Click;
                BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                bool EnableMixZipButton = true;
                string MixZipButtonText = Globalization.GetString("Operate_Text_Compression");

                if (SelectedItems.Any((Item) => Item is HiddenStorageItem))
                {
                    EnableMixZipButton = false;
                }
                else
                {
                    if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
                    {
                        if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                        {
                            if (SelectedItems.All((Item) => Item.Type == ".zip"))
                            {
                                MixZipButtonText = Globalization.GetString("Operate_Text_Decompression");
                            }
                            else if (SelectedItems.All((Item) => Item.Type != ".zip"))
                            {
                                MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                            }
                            else
                            {
                                EnableMixZipButton = false;
                            }
                        }
                        else
                        {
                            if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type == ".zip"))
                            {
                                EnableMixZipButton = false;
                            }
                            else
                            {
                                MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                            }
                        }
                    }
                    else
                    {
                        MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                    }
                }

                AppBarButton CompressionButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Bookmarks),
                    Label = MixZipButtonText,
                    IsEnabled = EnableMixZipButton
                };
                CompressionButton.Click += MixZip_Click;
                BottomCommandBar.SecondaryCommands.Add(CompressionButton);
            }
            else
            {
                if (SelectedItem is FileSystemStorageItemBase Item)
                {
                    if (Item is HiddenStorageItem)
                    {
                        AppBarButton CopyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Copy),
                            Label = Globalization.GetString("Operate_Text_Copy")
                        };
                        CopyButton.Click += Copy_Click;
                        BottomCommandBar.PrimaryCommands.Add(CopyButton);

                        AppBarButton CutButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Cut),
                            Label = Globalization.GetString("Operate_Text_Cut")
                        };
                        CutButton.Click += Cut_Click;
                        BottomCommandBar.PrimaryCommands.Add(CutButton);

                        AppBarButton DeleteButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Delete),
                            Label = Globalization.GetString("Operate_Text_Delete")
                        };
                        DeleteButton.Click += Delete_Click;
                        BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                        AppBarButton WinExButton = new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uEC50" },
                            Label = Globalization.GetString("Operate_Text_OpenInWinExplorer")
                        };
                        WinExButton.Click += OpenHiddenItemExplorer_Click;
                        BottomCommandBar.PrimaryCommands.Add(WinExButton);

                        AppBarButton RemoveHiddenButton = new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uF5EF" },
                            Label = Globalization.GetString("Operate_Text_RemoveHidden")
                        };
                        RemoveHiddenButton.Click += RemoveHidden_Click;
                        BottomCommandBar.PrimaryCommands.Add(RemoveHiddenButton);
                    }
                    else
                    {
                        AppBarButton CopyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Copy),
                            Label = Globalization.GetString("Operate_Text_Copy")
                        };
                        CopyButton.Click += Copy_Click;
                        BottomCommandBar.PrimaryCommands.Add(CopyButton);

                        AppBarButton CutButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Cut),
                            Label = Globalization.GetString("Operate_Text_Cut")
                        };
                        CutButton.Click += Cut_Click;
                        BottomCommandBar.PrimaryCommands.Add(CutButton);

                        AppBarButton DeleteButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Delete),
                            Label = Globalization.GetString("Operate_Text_Delete")
                        };
                        DeleteButton.Click += Delete_Click;
                        BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                        AppBarButton RenameButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Rename),
                            Label = Globalization.GetString("Operate_Text_Rename")
                        };
                        RenameButton.Click += Rename_Click;
                        BottomCommandBar.PrimaryCommands.Add(RenameButton);

                        if (Item.StorageType == StorageItemTypes.File)
                        {
                            AppBarButton OpenButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.OpenFile),
                                Label = Globalization.GetString("Operate_Text_Open")
                            };
                            OpenButton.Click += ItemOpen_Click;
                            BottomCommandBar.SecondaryCommands.Add(OpenButton);

                            MenuFlyout OpenFlyout = new MenuFlyout();
                            MenuFlyoutItem AdminItem = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uEA0D" },
                                Text = Globalization.GetString("Operate_Text_OpenAsAdministrator"),
                                IsEnabled = RunWithSystemAuthority.IsEnabled
                            };
                            AdminItem.Click += RunWithSystemAuthority_Click;
                            OpenFlyout.Items.Add(AdminItem);

                            MenuFlyoutItem OtherItem = new MenuFlyoutItem
                            {
                                Icon = new SymbolIcon(Symbol.SwitchApps),
                                Text = Globalization.GetString("Operate_Text_ChooseAnotherApp"),
                                IsEnabled = ChooseOtherApp.IsEnabled
                            };
                            OtherItem.Click += ChooseOtherApp_Click;
                            OpenFlyout.Items.Add(OtherItem);

                            BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.OpenWith),
                                Label = Globalization.GetString("Operate_Text_OpenWith"),
                                Flyout = OpenFlyout
                            });

                            BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                            MenuFlyout ToolFlyout = new MenuFlyout();
                            MenuFlyoutItem UnLock = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE785" },
                                Text = Globalization.GetString("Operate_Text_Unlock")
                            };
                            UnLock.Click += TryUnlock_Click;
                            ToolFlyout.Items.Add(UnLock);

                            MenuFlyoutItem Hash = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE2B2" },
                                Text = Globalization.GetString("Operate_Text_ComputeHash")
                            };
                            Hash.Click += CalculateHash_Click;
                            ToolFlyout.Items.Add(Hash);

                            BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                            {
                                Icon = new FontIcon { Glyph = "\uE90F" },
                                Label = Globalization.GetString("Operate_Text_Tool"),
                                IsEnabled = FileTool.IsEnabled,
                                Flyout = ToolFlyout
                            });

                            MenuFlyout EditFlyout = new MenuFlyout();
                            MenuFlyoutItem MontageItem = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE177" },
                                Text = Globalization.GetString("Operate_Text_Montage"),
                                IsEnabled = VideoEdit.IsEnabled
                            };
                            MontageItem.Click += VideoEdit_Click;
                            EditFlyout.Items.Add(MontageItem);

                            MenuFlyoutItem MergeItem = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE11E" },
                                Text = Globalization.GetString("Operate_Text_Merge"),
                                IsEnabled = VideoMerge.IsEnabled
                            };
                            MergeItem.Click += VideoMerge_Click;
                            EditFlyout.Items.Add(MergeItem);

                            MenuFlyoutItem TranscodeItem = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE1CA" },
                                Text = Globalization.GetString("Operate_Text_Transcode"),
                                IsEnabled = Transcode.IsEnabled
                            };
                            TranscodeItem.Click += Transcode_Click;
                            EditFlyout.Items.Add(TranscodeItem);

                            BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Edit),
                                Label = Globalization.GetString("Operate_Text_Edit"),
                                IsEnabled = FileEdit.IsEnabled,
                                Flyout = EditFlyout
                            });

                            MenuFlyout ShareFlyout = new MenuFlyout();
                            MenuFlyoutItem SystemShareItem = new MenuFlyoutItem
                            {
                                Icon = new SymbolIcon(Symbol.Share),
                                Text = Globalization.GetString("Operate_Text_SystemShare")
                            };
                            SystemShareItem.Click += SystemShare_Click;
                            ShareFlyout.Items.Add(SystemShareItem);

                            MenuFlyoutItem WIFIShareItem = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE701" },
                                Text = Globalization.GetString("Operate_Text_WIFIShare")
                            };
                            WIFIShareItem.Click += WIFIShare_Click;
                            ShareFlyout.Items.Add(WIFIShareItem);

                            MenuFlyoutItem BluetoothShare = new MenuFlyoutItem
                            {
                                Icon = new FontIcon { Glyph = "\uE702" },
                                Text = Globalization.GetString("Operate_Text_BluetoothShare")
                            };
                            BluetoothShare.Click += BluetoothShare_Click;
                            ShareFlyout.Items.Add(BluetoothShare);

                            BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Share),
                                Label = Globalization.GetString("Operate_Text_Share"),
                                IsEnabled = FileShare.IsEnabled,
                                Flyout = ShareFlyout
                            });

                            AppBarButton CompressionButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Bookmarks),
                                Label = Zip.Label,
                                IsEnabled = Zip.IsEnabled
                            };
                            CompressionButton.Click += Zip_Click;
                            BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                            BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                            AppBarButton PropertyButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Tag),
                                Label = Globalization.GetString("Operate_Text_Property")
                            };
                            PropertyButton.Click += Attribute_Click;
                            BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                        }
                        else
                        {
                            AppBarButton OpenButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.BackToWindow),
                                Label = Globalization.GetString("Operate_Text_Open")
                            };
                            OpenButton.Click += ItemOpen_Click;
                            BottomCommandBar.SecondaryCommands.Add(OpenButton);

                            AppBarButton NewWindowButton = new AppBarButton
                            {
                                Icon = new FontIcon { Glyph = "\uE727" },
                                Label = Globalization.GetString("Operate_Text_NewWindow")
                            };
                            NewWindowButton.Click += OpenFolderInNewWindow_Click;
                            BottomCommandBar.SecondaryCommands.Add(NewWindowButton);

                            AppBarButton NewTabButton = new AppBarButton
                            {
                                Icon = new FontIcon { Glyph = "\uF7ED" },
                                Label = Globalization.GetString("Operate_Text_NewTab")
                            };
                            NewTabButton.Click += OpenFolderInNewTab_Click;
                            BottomCommandBar.SecondaryCommands.Add(NewTabButton);

                            AppBarButton CompressionButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Bookmarks),
                                Label = Globalization.GetString("Operate_Text_Compression")
                            };
                            CompressionButton.Click += CompressFolder_Click;
                            BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                            AppBarButton PinButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Add),
                                Label = Globalization.GetString("Operate_Text_PinToHome")
                            };
                            PinButton.Click += AddToLibray_Click;
                            BottomCommandBar.SecondaryCommands.Add(PinButton);

                            AppBarButton PropertyButton = new AppBarButton
                            {
                                Icon = new SymbolIcon(Symbol.Tag),
                                Label = Globalization.GetString("Operate_Text_Property")
                            };
                            PropertyButton.Click += FolderProperty_Click;
                            BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                        }
                    }
                }
                else
                {
                    bool IsEnablePaste, IsEnableUndo;

                    try
                    {
                        DataPackageView Package = Clipboard.GetContent();

                        if (Package.Contains(StandardDataFormats.StorageItems))
                        {
                            IsEnablePaste = true;
                        }
                        else if (Package.Contains(StandardDataFormats.Html))
                        {
                            string Html = await Package.GetHtmlFormatAsync();
                            string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                            HtmlDocument Document = new HtmlDocument();
                            Document.LoadHtml(Fragment);
                            HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                            if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                            {
                                IsEnablePaste = true;
                            }
                            else
                            {
                                IsEnablePaste = false;
                            }
                        }
                        else
                        {
                            IsEnablePaste = false;
                        }
                    }
                    catch
                    {
                        IsEnablePaste = false;
                    }

                    if (OperationRecorder.Current.Value.Count > 0)
                    {
                        IsEnableUndo = true;
                    }
                    else
                    {
                        IsEnableUndo = false;
                    }

                    AppBarButton PasteButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Paste),
                        Label = Globalization.GetString("Operate_Text_Paste"),
                        IsEnabled = IsEnablePaste
                    };
                    PasteButton.Click += Paste_Click;
                    BottomCommandBar.PrimaryCommands.Add(PasteButton);

                    AppBarButton UndoButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Undo),
                        Label = Globalization.GetString("Operate_Text_Undo"),
                        IsEnabled = IsEnableUndo
                    };
                    UndoButton.Click += Undo_Click;
                    BottomCommandBar.PrimaryCommands.Add(UndoButton);

                    AppBarButton RefreshButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Refresh),
                        Label = Globalization.GetString("Operate_Text_Refresh")
                    };
                    RefreshButton.Click += Refresh_Click;
                    BottomCommandBar.PrimaryCommands.Add(RefreshButton);

                    MenuFlyout NewFlyout = new MenuFlyout();
                    MenuFlyoutItem CreateFileItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.Page2),
                        Text = Globalization.GetString("Operate_Text_CreateFile"),
                        MinWidth = 150
                    };
                    CreateFileItem.Click += CreateFile_Click;
                    NewFlyout.Items.Add(CreateFileItem);

                    MenuFlyoutItem CreateFolder = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.NewFolder),
                        Text = Globalization.GetString("Operate_Text_CreateFolder"),
                        MinWidth = 150
                    };
                    CreateFolder.Click += CreateFolder_Click;
                    NewFlyout.Items.Add(CreateFolder);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Add),
                        Label = Globalization.GetString("Operate_Text_Create"),
                        Flyout = NewFlyout
                    });

                    bool DescCheck = false;
                    bool AscCheck = false;
                    bool NameCheck = false;
                    bool TimeCheck = false;
                    bool TypeCheck = false;
                    bool SizeCheck = false;

                    if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
                    {
                        DescCheck = false;
                        AscCheck = true;
                    }
                    else
                    {
                        AscCheck = false;
                        DescCheck = true;
                    }

                    switch (SortCollectionGenerator.Current.SortTarget)
                    {
                        case SortTarget.Name:
                            {
                                TypeCheck = false;
                                TimeCheck = false;
                                SizeCheck = false;
                                NameCheck = true;
                                break;
                            }
                        case SortTarget.Type:
                            {
                                TimeCheck = false;
                                SizeCheck = false;
                                NameCheck = false;
                                TypeCheck = true;
                                break;
                            }
                        case SortTarget.ModifiedTime:
                            {
                                SizeCheck = false;
                                NameCheck = false;
                                TypeCheck = false;
                                TimeCheck = true;
                                break;
                            }
                        case SortTarget.Size:
                            {
                                NameCheck = false;
                                TypeCheck = false;
                                TimeCheck = false;
                                SizeCheck = true;
                                break;
                            }
                    }

                    MenuFlyout SortFlyout = new MenuFlyout();

                    RadioMenuFlyoutItem SortName = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                        IsChecked = NameCheck
                    };
                    SortName.Click += OrderByName_Click;
                    SortFlyout.Items.Add(SortName);

                    RadioMenuFlyoutItem SortTime = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                        IsChecked = TimeCheck
                    };
                    SortTime.Click += OrderByTime_Click;
                    SortFlyout.Items.Add(SortTime);

                    RadioMenuFlyoutItem SortType = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                        IsChecked = TypeCheck
                    };
                    SortType.Click += OrderByType_Click;
                    SortFlyout.Items.Add(SortType);

                    RadioMenuFlyoutItem SortSize = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                        IsChecked = SizeCheck
                    };
                    SortSize.Click += OrderBySize_Click;
                    SortFlyout.Items.Add(SortSize);

                    SortFlyout.Items.Add(new MenuFlyoutSeparator());

                    RadioMenuFlyoutItem Asc = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                        IsChecked = AscCheck
                    };
                    Asc.Click += Asc_Click;
                    SortFlyout.Items.Add(Asc);

                    RadioMenuFlyoutItem Desc = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                        IsChecked = DescCheck
                    };
                    Desc.Click += Desc_Click;
                    SortFlyout.Items.Add(Desc);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Sort),
                        Label = Globalization.GetString("Operate_Text_Sort"),
                        Flyout = SortFlyout
                    });

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.GetString("Operate_Text_Property")
                    };
                    PropertyButton.Click += ParentProperty_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);

                    AppBarButton WinExButton = new AppBarButton
                    {
                        Icon = new FontIcon { Glyph = "\uEC50" },
                        Label = Globalization.GetString("Operate_Text_OpenInWinExplorer")
                    };
                    WinExButton.Click += UseSystemFileMananger_Click;
                    BottomCommandBar.SecondaryCommands.Add(WinExButton);

                    AppBarButton TerminalButton = new AppBarButton
                    {
                        Icon = new FontIcon { Glyph = "\uE756" },
                        Label = Globalization.GetString("Operate_Text_OpenInTerminal")
                    };
                    TerminalButton.Click += OpenInTerminal_Click;
                    BottomCommandBar.SecondaryCommands.Add(TerminalButton);
                }
            }
        }
    }
}

