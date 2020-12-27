using ComputerVision;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace RX_Explorer
{
    public sealed partial class FilePresenter : Page
    {
        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; private set; }

        private FileControl Container
        {
            get
            {
                if (WeakToFileControl.TryGetTarget(out FileControl Instance))
                {
                    return Instance;
                }
                else
                {
                    return null;
                }
            }
        }

        public WeakReference<FileControl> WeakToFileControl { get; set; }

        private int DropLock;

        private int ViewDropLock;

        private PointerEventHandler PointerPressedEventHandler;

        private ListViewBase itemPresenter;
        public ListViewBase ItemPresenter
        {
            get => itemPresenter;
            set
            {
                if (value != itemPresenter)
                {
                    if (PointerPressedEventHandler != null)
                    {
                        value.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
                    }

                    value.AddHandler(PointerPressedEvent, PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed), true);

                    if (SelectionExtention != null)
                    {
                        SelectionExtention.Dispose();
                    }

                    SelectionExtention = new ListViewBaseSelectionExtention(value, DrawRectangle);

                    if (value is GridView)
                    {
                        if (ListViewRefreshContainer != null)
                        {
                            ListViewRefreshContainer.Visibility = Visibility.Collapsed;
                            ListViewControl.ItemsSource = null;
                        }

                        GridViewControl.ItemsSource = FileCollection;
                        GridViewRefreshContainer.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (GridViewRefreshContainer != null)
                        {
                            GridViewRefreshContainer.Visibility = Visibility.Collapsed;
                            GridViewControl.ItemsSource = null;
                        }

                        ListViewControl.ItemsSource = FileCollection;
                        ListViewRefreshContainer.Visibility = Visibility.Visible;

                        if (value.Header == null)
                        {
                            value.Header = ListViewHeaderController.Create();
                        }

                        if (value.Header is ListViewHeaderController Instance)
                        {
                            Instance.Filter.RefreshListRequested -= Filter_RefreshListRequested;
                            Instance.Filter.RefreshListRequested += Filter_RefreshListRequested;
                        }
                    }

                    itemPresenter = value;
                }
            }
        }

        private WiFiShareProvider WiFiProvider;
        private ListViewBaseSelectionExtention SelectionExtention;
        private FileSystemStorageItemBase TabTarget;
        private DateTimeOffset LastClickTime;
        private DateTimeOffset LastPressTime;
        private string LastPressString;

        public FileSystemStorageItemBase SelectedItem
        {
            get => ItemPresenter.SelectedItem as FileSystemStorageItemBase;
            set
            {
                ItemPresenter.SelectedItem = value;

                if (value != null)
                {
                    (ItemPresenter.ContainerFromItem(value) as SelectorItem)?.Focus(FocusState.Programmatic);
                }
            }
        }

        public List<FileSystemStorageItemBase> SelectedItems => ItemPresenter.SelectedItems.Select((Item) => Item as FileSystemStorageItemBase).ToList();

        public FilePresenter()
        {
            InitializeComponent();

            FileCollection = new ObservableCollection<FileSystemStorageItemBase>();
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;

            TryUnlock.IsEnabled = Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64;
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
                    case VirtualKey.Left:
                        {
                            Container.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.Right:
                        {
                            Container.GoForwardRecord_Click(null, null);
                            break;
                        }
                }
            }
        }

        private async void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);
            CoreVirtualKeyStates ShiftState = sender.GetKeyState(VirtualKey.Shift);

            bool AnyCommandBarFlyoutOpened = LnkItemFlyout.IsOpen || EmptyFlyout.IsOpen || FolderFlyout.IsOpen || FileFlyout.IsOpen || MixedFlyout.IsOpen;

            if (!QueueContentDialog.IsRunningOrWaiting && !MainPage.ThisPage.IsAnyTaskRunning && !AnyCommandBarFlyoutOpened)
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
                    case VirtualKey.F2:
                        {
                            Rename_Click(null, null);
                            break;
                        }
                    case VirtualKey.F5:
                        {
                            Refresh_Click(null, null);
                            break;
                        }
                    case VirtualKey.Enter when SelectedItems.Count == 1 && SelectedItem is FileSystemStorageItemBase Item:
                        {
                            await EnterSelectedItem(Item).ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.Back:
                        {
                            Container.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Container.AddressBox.Focus(FocusState.Programmatic);
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
                    case VirtualKey.Delete:
                    case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Delete_Click(null, null);
                            break;
                        }
                    case VirtualKey.F when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Container.GlobeSearch.Focus(FocusState.Programmatic);
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
                    case VirtualKey.E when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && Container.CurrentFolder != null:
                        {
                            _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                            break;
                        }
                    case VirtualKey.T when ShiftState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            OpenInTerminal_Click(null, null);
                            break;
                        }
                    case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            CloseAllFlyout();

                            if (SelectedItem is FileSystemStorageItemBase Item)
                            {
                                if (Item.StorageType == StorageItemTypes.Folder)
                                {
                                    TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(Item.Path);
                                }
                            }
                            else
                            {
                                TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(string.Empty);
                            }

                            break;
                        }
                    case VirtualKey.Q when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            OpenFolderInNewWindow_Click(null, null);
                            break;
                        }
                    case VirtualKey.Up:
                    case VirtualKey.Down:
                        {
                            if (SelectedItem is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    MixZip.IsEnabled = true;

                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout).ConfigureAwait(true);
                                }
                                else
                                {
                                    if (Context is HyperlinkStorageItem)
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout).ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout).ConfigureAwait(true);
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = FileCollection.FirstOrDefault();
                            }

                            break;
                        }
                    default:
                        {
                            args.Handled = false;
                            break;
                        }
                }
            }
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            char Input = Convert.ToChar(Key);

            if (char.IsLetterOrDigit(Input))
            {
                string SearchString = Input.ToString();

                try
                {
                    if (LastPressString != SearchString && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                    {
                        SearchString = LastPressString + SearchString;

                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any() && (SelectedItem == null || !Group.Contains(SelectedItem)))
                        {
                            SelectedItem = Group.FirstOrDefault();
                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                    else
                    {
                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any())
                        {
                            if (SelectedItem != null)
                            {
                                FileSystemStorageItemBase[] ItemArray = Group.ToArray();

                                int NextIndex = Array.IndexOf(ItemArray, SelectedItem);

                                if (NextIndex != -1)
                                {
                                    if (NextIndex < ItemArray.Length - 1)
                                    {
                                        SelectedItem = ItemArray[NextIndex + 1];
                                    }
                                    else
                                    {
                                        SelectedItem = ItemArray.FirstOrDefault();
                                    }
                                }
                                else
                                {
                                    SelectedItem = ItemArray.FirstOrDefault();
                                }
                            }
                            else
                            {
                                SelectedItem = Group.FirstOrDefault();
                            }

                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(NavigateToStorageItem)} throw an exception");
                }
                finally
                {
                    LastPressString = SearchString;
                    LastPressTime = DateTimeOffset.Now;
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
        private void CloseAllFlyout()
        {
            FileFlyout.Hide();
            FolderFlyout.Hide();
            EmptyFlyout.Hide();
            MixedFlyout.Hide();
            LnkItemFlyout.Hide();
        }

        private async Task Ctrl_Z_Click()
        {
            if (OperationRecorder.Current.Value.Count > 0)
            {
                await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Undoing")).ConfigureAwait(true);

                try
                {
                    foreach (string Action in OperationRecorder.Current.Value.Pop())
                    {
                        string[] SplitGroup = Action.Split("||", StringSplitOptions.RemoveEmptyEntries);

                        switch (SplitGroup[1])
                        {
                            case "Move":
                                {
                                    if (Container.CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[3]), StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (FileSystemStorageItemBase.Open(Path.GetDirectoryName(SplitGroup[0]), ItemFilters.Folder) is FileSystemStorageItemBase OriginFolder)
                                        {
                                            switch (SplitGroup[2])
                                            {
                                                case "File":
                                                    {
                                                        if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], OriginFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else if (FileSystemStorageItemBase.Open(Path.Combine(Container.CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.File) is FileSystemStorageItemBase Item)
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(Item.Path, OriginFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
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
                                                        if (FileSystemStorageItemBase.Open(Path.Combine(Container.CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.Folder) is FileSystemStorageItemBase Item)
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(Item.Path, OriginFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
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
                                        else
                                        {
                                            throw new DirectoryNotFoundException();
                                        }
                                    }
                                    else if (Container.CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[0]), StringComparison.OrdinalIgnoreCase))
                                    {
                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], Container.CurrentFolder.Path, (s, arg) =>
                                                        {
                                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                                            {
                                                                Container.ProBar.IsIndeterminate = false;
                                                                Container.ProBar.Value = arg.ProgressPercentage;
                                                            }
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        string TargetFolderPath = Path.GetDirectoryName(SplitGroup[3]);

                                                        if (WIN_Native_API.CheckExist(TargetFolderPath))
                                                        {
                                                            if (FileSystemStorageItemBase.Open(Path.Combine(TargetFolderPath, Path.GetFileName(SplitGroup[3])), ItemFilters.File) is FileSystemStorageItemBase Item)
                                                            {
                                                                await FullTrustProcessController.Current.MoveAsync(Item.Path, Container.CurrentFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else
                                                            {
                                                                throw new FileNotFoundException();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            throw new DirectoryNotFoundException();
                                                        }
                                                    }

                                                    break;
                                                }
                                            case "Folder":
                                                {
                                                    if (FileSystemStorageItemBase.Open(SplitGroup[3], ItemFilters.Folder) is FileSystemStorageItemBase Item)
                                                    {
                                                        await FullTrustProcessController.Current.MoveAsync(Item.Path, Container.CurrentFolder.Path, (s, arg) =>
                                                        {
                                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                                            {
                                                                Container.ProBar.IsIndeterminate = false;
                                                                Container.ProBar.Value = arg.ProgressPercentage;
                                                            }
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        throw new DirectoryNotFoundException();
                                                    }

                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        if (FileSystemStorageItemBase.Open(Path.GetDirectoryName(SplitGroup[0]), ItemFilters.Folder) is FileSystemStorageItemBase OriginFolder)
                                        {
                                            switch (SplitGroup[2])
                                            {
                                                case "File":
                                                    {
                                                        if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(SplitGroup[3], OriginFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            if (FileSystemStorageItemBase.Open(SplitGroup[3], ItemFilters.File) is FileSystemStorageItemBase File)
                                                            {
                                                                await FullTrustProcessController.Current.MoveAsync(File.Path, OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
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
                                                        if (FileSystemStorageItemBase.Open(SplitGroup[3], ItemFilters.Folder) is FileSystemStorageItemBase Folder)
                                                        {
                                                            await FullTrustProcessController.Current.MoveAsync(Folder.Path, OriginFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            throw new DirectoryNotFoundException();
                                                        }

                                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                        {
                                                            await Container.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            throw new DirectoryNotFoundException();
                                        }
                                    }

                                    break;
                                }
                            case "Copy":
                                {
                                    if (Container.CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[3]), StringComparison.OrdinalIgnoreCase))
                                    {
                                        switch (SplitGroup[2])
                                        {
                                            case "File":
                                                {
                                                    if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                                        {
                                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                                            {
                                                                Container.ProBar.IsIndeterminate = false;
                                                                Container.ProBar.Value = arg.ProgressPercentage;
                                                            }
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else if (FileSystemStorageItemBase.Open(Path.Combine(Container.CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.File) is FileSystemStorageItemBase File)
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(File.Path, true, (s, arg) =>
                                                        {
                                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                                            {
                                                                Container.ProBar.IsIndeterminate = false;
                                                                Container.ProBar.Value = arg.ProgressPercentage;
                                                            }
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
                                                    if (FileSystemStorageItemBase.Open(Path.Combine(Container.CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.Folder) is FileSystemStorageItemBase Folder)
                                                    {
                                                        await FullTrustProcessController.Current.DeleteAsync(Folder.Path, true, (s, arg) =>
                                                        {
                                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                                            {
                                                                Container.ProBar.IsIndeterminate = false;
                                                                Container.ProBar.Value = arg.ProgressPercentage;
                                                            }
                                                        }, true).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        throw new DirectoryNotFoundException();
                                                    }

                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        await FullTrustProcessController.Current.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                        {
                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = arg.ProgressPercentage;
                                            }
                                        }, true).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await Container.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
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

                await Container.LoadingActivation(false).ConfigureAwait(false);
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            List<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    IEnumerable<FileSystemStorageItemBase> StorageItems = SelectedItemsCopy.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                    if (StorageItems.Any())
                    {
                        List<IStorageItem> TempItemList = new List<IStorageItem>();

                        foreach (FileSystemStorageItemBase Item in StorageItems)
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
                    }

                    IEnumerable<FileSystemStorageItemBase> NotStorageItems = SelectedItemsCopy.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                    if (NotStorageItems.Any())
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
            CloseAllFlyout();

            try
            {
                DataPackageView Package = Clipboard.GetContent();

                if (Package.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> ItemList = await Package.GetStorageItemsAsync();

                    if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (ItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == Container.CurrentFolder.Path))
                        {
                            return;
                        }

                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            await FullTrustProcessController.Current.MoveAsync(ItemList.Select((Item) => Item.Path), Container.CurrentFolder.Path, (s, arg) =>
                              {
                                  if (Container.ProBar.Value < arg.ProgressPercentage)
                                  {
                                      Container.ProBar.IsIndeterminate = false;
                                      Container.ProBar.Value = arg.ProgressPercentage;
                                  }
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
                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            await FullTrustProcessController.Current.CopyAsync(ItemList.Select((Item) => Item.Path), Container.CurrentFolder.Path, (s, arg) =>
                              {
                                  if (Container.ProBar.Value < arg.ProgressPercentage)
                                  {
                                      Container.ProBar.IsIndeterminate = false;
                                      Container.ProBar.Value = arg.ProgressPercentage;
                                  }
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
                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                            if (LinkItemsPath.All((Item) => Path.GetDirectoryName(Item) == Container.CurrentFolder.Path))
                            {
                                return;
                            }

                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, Container.CurrentFolder.Path, (s, arg) =>
                                {
                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                    {
                                        Container.ProBar.IsIndeterminate = false;
                                        Container.ProBar.Value = arg.ProgressPercentage;
                                    }
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
                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, Container.CurrentFolder.Path, (s, arg) =>
                                {
                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                    {
                                        Container.ProBar.IsIndeterminate = false;
                                        Container.ProBar.Value = arg.ProgressPercentage;
                                    }
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
                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                await Container.LoadingActivation(false).ConfigureAwait(true);
                FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            List<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    IEnumerable<FileSystemStorageItemBase> StorageItems = SelectedItemsCopy.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                    if (StorageItems.Any())
                    {
                        List<IStorageItem> TempItemList = new List<IStorageItem>();

                        foreach (FileSystemStorageItemBase Item in StorageItems)
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
                    }

                    IEnumerable<FileSystemStorageItemBase> NotStorageItems = SelectedItemsCopy.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                    if (NotStorageItems.Any())
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
                    SelectedItemsCopy.ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity));
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
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                List<string> PathList = SelectedItems.Select((Item) => Item.Path).ToList();

                if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                Retry:
                    try
                    {
                        await FullTrustProcessController.Current.DeleteAsync(PathList, true, (s, arg) =>
                        {
                            if (Container.ProBar.Value < arg.ProgressPercentage)
                            {
                                Container.ProBar.IsIndeterminate = false;
                                Container.ProBar.Value = arg.ProgressPercentage;
                            }
                        }).ConfigureAwait(true);
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
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
                            Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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

                    await Container.LoadingActivation(false).ConfigureAwait(false);
                }
                else
                {
                    DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

                    if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                    Retry:
                        try
                        {
                            await FullTrustProcessController.Current.DeleteAsync(PathList, QueueContenDialog.IsPermanentDelete, (s, arg) =>
                            {
                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                {
                                    Container.ProBar.IsIndeterminate = false;
                                    Container.ProBar.Value = arg.ProgressPercentage;
                                }
                            }).ConfigureAwait(true);
                        }
                        catch (FileNotFoundException)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
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
                                Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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

                        await Container.LoadingActivation(false).ConfigureAwait(false);
                    }
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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
                    FileSystemStorageItemBase RenameItem = SelectedItem;

                    RenameDialog dialog = new RenameDialog(RenameItem);

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (WIN_Native_API.CheckExist(Path.Combine(Path.GetDirectoryName(RenameItem.Path), dialog.DesireName)))
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
                            await FullTrustProcessController.Current.RenameAsync(RenameItem.Path, dialog.DesireName).ConfigureAwait(true);
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
                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                            }
                        }
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SelectedItem.GetStorageItem().ConfigureAwait(true) is StorageFile ShareFile)
            {
                if (!WIN_Native_API.CheckExist(ShareFile.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

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
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (SelectorItem RemovedItem in e.RemovedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item)).OfType<SelectorItem>())
            {
                RemovedItem.CanDrag = false;
            }

            foreach (SelectorItem SelectedItem in e.AddedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item)).OfType<SelectorItem>())
            {
                SelectedItem.CanDrag = true;
            }

            ItemPresenter.UpdateLayout();

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

                    switch (Item.Type.ToLower())
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
                                Transcode.IsEnabled = true;
                                VideoEdit.IsEnabled = true;
                                VideoMerge.IsEnabled = true;
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
                                VideoEdit.IsEnabled = false;
                                VideoMerge.IsEnabled = false;
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
                        case ".msc":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                break;
                            }
                    }
                }
            }

            string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

            if (SelectedItems.Count > 0)
            {
                string SizeInfo = string.Empty;

                if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                {
                    ulong TotalSize = 0;
                    foreach (ulong Size in SelectedItems.Select((Item) => Item.SizeRaw).ToArray())
                    {
                        TotalSize += Size;
                    }

                    SizeInfo = $"  |  {TotalSize.ToFileSizeDescription()}";
                }

                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}{SizeInfo}";
                }
                else
                {
                    StatusTips.Text += $"  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}{SizeInfo}";
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

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
            {
                PointerPoint PointerInfo = e.GetCurrentPoint(null);

                if (PointerInfo.Properties.IsMiddleButtonPressed && Item.StorageType == StorageItemTypes.Folder)
                {
                    SelectionExtention.Disable();
                    SelectedItem = Item;
                    TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(Item.Path);
                }
                else if ((e.OriginalSource as FrameworkElement).FindParentOfType<SelectorItem>() != null)
                {
                    if (ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple)
                    {
                        if (e.KeyModifiers == VirtualKeyModifiers.None)
                        {
                            if (SelectedItems.Contains(Item))
                            {
                                SelectionExtention.Disable();
                            }
                            else
                            {
                                if (PointerInfo.Properties.IsLeftButtonPressed)
                                {
                                    SelectedItem = Item;
                                }

                                if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is TextBlock Block && Block.Name == "EmptyTextblock"))
                                {
                                    SelectionExtention.Enable();
                                }
                                else
                                {
                                    SelectionExtention.Disable();
                                }
                            }
                        }
                        else
                        {
                            SelectionExtention.Disable();
                        }
                    }
                    else
                    {
                        SelectionExtention.Disable();
                    }
                }
            }
            else
            {
                SelectedItem = null;
                SelectionExtention.Enable();
            }
        }

        private async void ViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                e.Handled = true;

                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            SelectedItem = Context;

                            if (Context is HyperlinkStorageItem)
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                            else
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HyperlinkStorageItem)
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                        else
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HyperlinkStorageItem)
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                            else
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
        }

        private async void FileProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (!WIN_Native_API.CheckExist(Item.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                if (Item.Type == ".zip")
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

                    await UnZipAsync(Item, (s, e) =>
                    {
                        if (Container.ProBar.Value < e.ProgressPercentage)
                        {
                            Container.ProBar.IsIndeterminate = false;
                            Container.ProBar.Value = e.ProgressPercentage;
                        }
                    }).ConfigureAwait(true);

                    await Container.LoadingActivation(false).ConfigureAwait(true);
                }
                else
                {
                    ZipDialog dialog = new ZipDialog(Item);

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, (s, e) =>
                        {
                            if (Container.ProBar.Value < e.ProgressPercentage)
                            {
                                Container.ProBar.IsIndeterminate = false;
                                Container.ProBar.Value = e.ProgressPercentage;
                            }
                        }).ConfigureAwait(true);

                        await Container.LoadingActivation(false).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task UnZipAsync(IEnumerable<FileSystemStorageItemBase> FileList, ProgressChangedEventHandler ProgressHandler = null)
        {
            long TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                TotalSize += Convert.ToInt64(Item.SizeRaw);
            }

            if (TotalSize == 0)
            {
                return;
            }

            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

            long Step = 0;

            foreach (FileSystemStorageItemBase Item in FileList)
            {
                await UnZipAsync(Item, (s, e) =>
                {
                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Convert.ToDouble(e.ProgressPercentage * Convert.ToInt64(Item.SizeRaw)) + Step * 100) / TotalSize)), null));
                }).ConfigureAwait(true);

                Step += Convert.ToInt64(Item.SizeRaw);
            }

            await Container.LoadingActivation(false).ConfigureAwait(true);
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="Item">ZIP文件</param>
        /// <returns>无</returns>
        private async Task UnZipAsync(FileSystemStorageItemBase Item, ProgressChangedEventHandler ProgressHandler = null)
        {
            try
            {
                if (FileSystemStorageItemBase.Create(Path.Combine(Path.GetDirectoryName(Item.Path), Path.GetFileNameWithoutExtension(Item.Name)), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFolder)
                {
                    using (FileStream FileStream = Item.GetFileStreamFromFile(AccessMode.Exclusive))
                    using (ZipInputStream InputZipStream = new ZipInputStream(FileStream))
                    {
                        FileStream.Seek(0, SeekOrigin.Begin);

                        InputZipStream.IsStreamOwner = false;

                        while (InputZipStream.GetNextEntry() is ZipEntry Entry)
                        {
                            if (!InputZipStream.CanDecompressEntry)
                            {
                                throw new NotImplementedException();
                            }

                            if (Entry.Name.Contains("/"))
                            {
                                string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

                                string TempFolderPath = NewFolder.Path;

                                for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                                {
                                    if (FileSystemStorageItemBase.Create(Path.Combine(TempFolderPath, SplitFolderPath[i]), StorageItemTypes.Folder, CreateOption.OpenIfExist) is FileSystemStorageItemBase NextFolder)
                                    {
                                        TempFolderPath = NextFolder.Path;
                                    }
                                    else
                                    {
                                        throw new UnauthorizedAccessException("Could not create directory");
                                    }
                                }

                                if (Entry.Name.Last() == '/')
                                {
                                    if (FileSystemStorageItemBase.Create(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.Folder, CreateOption.OpenIfExist) == null)
                                    {
                                        throw new UnauthorizedAccessException("Could not create directory");
                                    }
                                }
                                else
                                {
                                    if (FileSystemStorageItemBase.Create(Path.Combine(TempFolderPath, SplitFolderPath.Last()), StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageItemBase NewFile)
                                    {
                                        using (FileStream NewFileStream = NewFile.GetFileStreamFromFile(AccessMode.Write))
                                        {
                                            await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        throw new UnauthorizedAccessException();
                                    }
                                }
                            }
                            else
                            {
                                if (FileSystemStorageItemBase.Create(Path.Combine(NewFolder.Path, Entry.Name), StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageItemBase NewFile)
                                {
                                    using (FileStream NewFileStream = NewFile.GetFileStreamFromFile(AccessMode.Write))
                                    {
                                        await InputZipStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                                    }
                                }
                                else
                                {
                                    throw new UnauthorizedAccessException();
                                }
                            }

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(FileStream.Position * 100d / FileStream.Length)), null));
                        }
                    }
                }
                else
                {
                    throw new UnauthorizedAccessException();
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
                    _ = await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(Item.Path));
                }
            }
            catch (NotImplementedException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CanNotDecompressEncrypted_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="ZipTarget">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        private async Task CreateZipAsync(FileSystemStorageItemBase ZipTarget, string NewZipName, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            try
            {
                if (FileSystemStorageItemBase.Create(Path.Combine(Container.CurrentFolder.Path, NewZipName), StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFile)
                {
                    using (FileStream NewFileStream = NewFile.GetFileStreamFromFile(AccessMode.Exclusive))
                    using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                    {
                        OutputStream.SetLevel(ZipLevel);
                        OutputStream.UseZip64 = UseZip64.Dynamic;
                        OutputStream.IsStreamOwner = false;

                        if (ZipTarget.StorageType == StorageItemTypes.File)
                        {
                            using (FileStream FileStream = ZipTarget.GetFileStreamFromFile(AccessMode.Read))
                            {
                                ZipEntry NewEntry = new ZipEntry(ZipTarget.Name)
                                {
                                    DateTime = DateTime.Now,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);
                            }

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
                            });
                        }
                        else
                        {
                            await ZipFolderCore(ZipTarget, OutputStream, ZipTarget.Name, ProgressHandler).ConfigureAwait(false);
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
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
                            _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                        }
                    });
                }
            }
            catch (Exception e)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                });
            }
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="ZipItemGroup">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="ProgressHandler">进度通知</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(IEnumerable<FileSystemStorageItemBase> ZipItemGroup, string NewZipName, int ZipLevel, ProgressChangedEventHandler ProgressHandler = null)
        {
            try
            {
                if (FileSystemStorageItemBase.Create(Path.Combine(Container.CurrentFolder.Path, NewZipName), StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFile)
                {
                    using (FileStream NewFileStream = NewFile.GetFileStreamFromFile(AccessMode.Exclusive))
                    using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                    {
                        OutputStream.SetLevel(ZipLevel);
                        OutputStream.UseZip64 = UseZip64.Dynamic;
                        OutputStream.IsStreamOwner = false;

                        long TotalSize = 0;

                        foreach (FileSystemStorageItemBase StorageItem in ZipItemGroup)
                        {
                            if (StorageItem.StorageType == StorageItemTypes.File)
                            {
                                TotalSize += Convert.ToInt64(StorageItem.SizeRaw);
                            }
                            else
                            {
                                TotalSize += Convert.ToInt64(await Task.Run(() => WIN_Native_API.CalculateFolderSize(StorageItem.Path)).ConfigureAwait(false));
                            }
                        }

                        long CurrentPosition = 0;

                        foreach (FileSystemStorageItemBase StorageItem in ZipItemGroup)
                        {
                            if (StorageItem.StorageType == StorageItemTypes.File)
                            {
                                using (FileStream FileStream = StorageItem.GetFileStreamFromFile(AccessMode.Read))
                                {
                                    ZipEntry NewEntry = new ZipEntry(StorageItem.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);
                                }

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += Convert.ToInt64(StorageItem.SizeRaw);

                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                                    });
                                }
                            }
                            else
                            {
                                long InnerFolderSize = Convert.ToInt64(await Task.Run(() => WIN_Native_API.CalculateFolderSize(StorageItem.Path)).ConfigureAwait(false));

                                await ZipFolderCore(StorageItem, OutputStream, StorageItem.Name, (s, e) =>
                                {
                                    if (TotalSize > 0)
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                                    }
                                }).ConfigureAwait(false);

                                if (TotalSize > 0)
                                {
                                    CurrentPosition += InnerFolderSize;

                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                    {
                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                                    });
                                }
                            }
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
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
                            _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                        }
                    });
                }
            }
            catch (Exception e)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                });
            }
        }

        private async Task ZipFolderCore(FileSystemStorageItemBase Folder, ZipOutputStream OutputStream, string BaseFolderName, ProgressChangedEventHandler ProgressHandler = null)
        {
            List<FileSystemStorageItemBase> PathList = Folder.GetChildrenItems(true);

            if (PathList.Count == 0)
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
                long TotalSize = Convert.ToInt64(await Task.Run(() => WIN_Native_API.CalculateFolderSize(Folder.Path)).ConfigureAwait(false));

                long CurrentPosition = 0;

                foreach (FileSystemStorageItemBase Item in PathList)
                {
                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        long InnerFolderSize = Convert.ToInt64(await Task.Run(() => WIN_Native_API.CalculateFolderSize(Item.Path)).ConfigureAwait(false));

                        await ZipFolderCore(Item, OutputStream, $"{BaseFolderName}/{Item.Name}", ProgressHandler: (s, e) =>
                        {
                            if (TotalSize > 0)
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((CurrentPosition + Convert.ToInt64(e.ProgressPercentage / 100d * InnerFolderSize)) * 100d / TotalSize)), null));
                            }
                        }).ConfigureAwait(false);

                        if (TotalSize > 0)
                        {
                            CurrentPosition += InnerFolderSize;

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            });
                        }
                    }
                    else if (Item.StorageType == StorageItemTypes.File)
                    {
                        using (FileStream FileStream = Item.GetFileStreamFromFile(AccessMode.Read))
                        {
                            ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{Item.Name}")
                            {
                                DateTime = DateTime.Now,
                                CompressionMethod = CompressionMethod.Deflated,
                                Size = FileStream.Length
                            };

                            OutputStream.PutNextEntry(NewEntry);

                            await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                            OutputStream.CloseEntry();
                        }

                        if (TotalSize > 0)
                        {
                            CurrentPosition += Convert.ToInt64(Item.SizeRaw);

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling(CurrentPosition * 100d / TotalSize)), null));
                            });
                        }
                    }
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
            else if (e.OriginalSource is Grid)
            {
                if (Path.GetPathRoot(Container.CurrentFolder?.Path) == Container.CurrentFolder?.Path)
                {
                    MainPage.ThisPage.NavView_BackRequested(null, null);
                }
                else
                {
                    Container.GoParentFolder_Click(null, null);
                }
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!WIN_Native_API.CheckExist(SelectedItem.Path))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

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

            switch (SelectedItem.Type.ToLower())
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
                        if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Source)
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    string DestFilePath = Path.Combine(Container.CurrentFolder.Path, $"{Source.Path}.{dialog.MediaTranscodeEncodingProfile.ToLower()}");

                                    FileSystemStorageItemBase.Create(DestFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName);

                                    StorageFile DestinationFile = await StorageFile.GetFileFromPathAsync(DestFilePath);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp).ConfigureAwait(true);
                                }
                                catch (Exception)
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
                                        _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                        using (IRandomAccessStream OriginStream = await SelectedItem.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                            Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                        }

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Transcoding")).ConfigureAwait(true);

                            await GeneralTransformer.TranscodeFromImageAsync(SelectedItem, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                            await Container.LoadingActivation(false).ConfigureAwait(true);
                        }
                        break;
                    }
            }
        }

        private async void FolderProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!WIN_Native_API.CheckExist(SelectedItem.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);

            _ = await Dialog.ShowAsync().ConfigureAwait(false);
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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

            using (MD5 MD5Alg = MD5.Create())
            {
                string Hash = MD5Alg.GetHash(SelectedItem.Path);
                QRText.Text = WiFiProvider.CurrentUri + Hash;
                WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, SelectedItem.Path);
            }

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
            CloseAllFlyout();

            _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
        }

        private async void ParentProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!WIN_Native_API.CheckExist(Container.CurrentFolder.Path))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if (Container.CurrentFolder.Path.Equals(Path.GetPathRoot(Container.CurrentFolder.Path), StringComparison.OrdinalIgnoreCase))
            {
                if (CommonAccessCollection.HardDeviceList.FirstOrDefault((Device) => Device.Folder.Path.Equals(Container.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    PropertyDialog Dialog = new PropertyDialog(Container.CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                PropertyDialog Dialog = new PropertyDialog(Container.CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (WIN_Native_API.CheckExist(Container.CurrentFolder.Path))
            {
                if (FileSystemStorageItemBase.Create(Path.Combine(Container.CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFolder)
                {
                    while (true)
                    {
                        if (FileCollection.FirstOrDefault((Item) => Item == NewFolder) is FileSystemStorageItemBase NewItem)
                        {
                            ItemPresenter.UpdateLayout();

                            ItemPresenter.ScrollIntoView(NewItem);

                            SelectedItem = NewItem;

                            if ((ItemPresenter.ContainerFromItem(NewItem) as SelectorItem)?.ContentTemplateRoot is FrameworkElement Element)
                            {
                                if (Element.FindName("NameLabel") is TextBlock NameLabel)
                                {
                                    NameLabel.Visibility = Visibility.Collapsed;
                                }

                                if (Element.FindName("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.Tag = NewItem;
                                    EditBox.Text = NewItem.Name;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);
                                }

                                MainPage.ThisPage.IsAnyTaskRunning = true;
                            }

                            break;
                        }
                        else
                        {
                            await Task.Delay(500).ConfigureAwait(true);
                        }
                    }
                }
                else
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
                        _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                    }
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
            CloseAllFlyout();

            if (!WIN_Native_API.CheckExist(SelectedItem.Path))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile ShareItem)
                {
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
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (WIN_Native_API.CheckExist(Container.CurrentFolder.Path))
                {
                    await Container.DisplayItemsInFolder(Container.CurrentFolder, true).ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(Refresh_Click)} throw an exception");
            }
        }

        private async void ViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple && e.ClickedItem is FileSystemStorageItemBase ReFile)
            {
                CoreVirtualKeyStates CtrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                CoreVirtualKeyStates ShiftState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                {
                    await EnterSelectedItem(ReFile).ConfigureAwait(false);
                }
            }
        }

        public async Task EnterSelectedItem(FileSystemStorageItemBase ReFile, bool RunAsAdministrator = false)
        {
            if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
            {
                try
                {
                    if (TabTarget.StorageType == StorageItemTypes.File)
                    {
                        if (!WIN_Native_API.CheckExist(TabTarget.Path))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            return;
                        }

                        string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(TabTarget.Type).ConfigureAwait(true);

                        if (!string.IsNullOrEmpty(AdminExecutablePath) && AdminExecutablePath != Package.Current.Id.FamilyName)
                        {
                            if (Path.IsPathRooted(AdminExecutablePath))
                            {
                            Retry:
                                try
                                {
                                    await FullTrustProcessController.Current.RunAsync(AdminExecutablePath, false, false, false, TabTarget.Path).ConfigureAwait(true);
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
                                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                            else
                            {
                                if ((await Launcher.FindFileHandlersAsync(TabTarget.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                {
                                    if (await TabTarget.GetStorageItem().ConfigureAwait(true) is StorageFile File)
                                    {
                                        if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                        {
                                            ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                                {
                                                    switch (TabTarget.Type.ToLower())
                                                    {
                                                        case ".jpg":
                                                        case ".png":
                                                        case ".bmp":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                                    Container.Frame.Navigate(typeof(MediaPlayer), File, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(MediaPlayer), File, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                        case ".txt":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(TextViewer), File, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(TextViewer), File, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                        case ".pdf":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(PdfReader), File, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(PdfReader), File, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                    }
                                                }
                                                else
                                                {
                                                    if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                                    {
                                                    Retry:
                                                        try
                                                        {
                                                            await FullTrustProcessController.Current.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
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
                                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                                    else
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                        {
                                                            if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                            {
                                                                ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                            }

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
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                                else
                                {
                                    ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                        {
                                            switch (TabTarget.Type.ToLower())
                                            {
                                                case ".jpg":
                                                case ".png":
                                                case ".bmp":
                                                    {
                                                        if (AnimationController.Current.IsEnableAnimation)
                                                        {
                                                            Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                        }
                                                        else
                                                        {
                                                            Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                    {
                                                        if (AnimationController.Current.IsEnableAnimation)
                                                        {
                                                            Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                                        }
                                                        else
                                                        {
                                                            Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                                        }
                                                        break;
                                                    }
                                                case ".txt":
                                                    {
                                                        if (AnimationController.Current.IsEnableAnimation)
                                                        {
                                                            Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                                        }
                                                        else
                                                        {
                                                            Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                                        }
                                                        break;
                                                    }
                                                case ".pdf":
                                                    {
                                                        if (AnimationController.Current.IsEnableAnimation)
                                                        {
                                                            Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                                        }
                                                        else
                                                        {
                                                            Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                                        }
                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                            {
                                            Retry:
                                                try
                                                {
                                                    await FullTrustProcessController.Current.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
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
                                                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                            else
                                            {
                                                if (await TabTarget.GetStorageItem().ConfigureAwait(true) is StorageFile File)
                                                {
                                                    if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                    {
                                                        if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                        {
                                                            ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                        }

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
                                                else
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            switch (TabTarget.Type.ToLower())
                            {
                                case ".jpg":
                                case ".png":
                                case ".bmp":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".txt":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".pdf":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".lnk":
                                    {
                                        if (TabTarget is HyperlinkStorageItem Item)
                                        {
                                            await FullTrustProcessController.Current.RunAsync(Item.LinkTargetPath, Item.NeedRunAsAdmin, false, false, Item.Arguments).ConfigureAwait(true);
                                        }

                                        break;
                                    }
                                case ".exe":
                                case ".bat":
                                    {
                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.RunAsync(TabTarget.Path, RunAsAdministrator).ConfigureAwait(true);
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                case ".msc":
                                    {
                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.RunAsync("powershell.exe", false, true, false, "-Command", TabTarget.Path).ConfigureAwait(true);
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                            {
                                                switch (TabTarget.Type.ToLower())
                                                {
                                                    case ".jpg":
                                                    case ".png":
                                                    case ".bmp":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                    case ".txt":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                    case ".pdf":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                                {
                                                Retry:
                                                    try
                                                    {
                                                        await FullTrustProcessController.Current.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
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
                                                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                                else
                                                {
                                                    if (await TabTarget.GetStorageItem().ConfigureAwait(true) is StorageFile File)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                        {
                                                            if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                            {
                                                                ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                            }

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
                                                    else
                                                    {
                                                        QueueContentDialog dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        if (WIN_Native_API.CheckExist(TabTarget.Path))
                        {
                            await Container.DisplayItemsInFolder(TabTarget).ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(EnterSelectedItem)} throw an exception");
                }
                finally
                {
                    Interlocked.Exchange(ref TabTarget, null);
                }
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                {
                    VideoEditDialog Dialog = new VideoEditDialog(File);

                    if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (await Container.CurrentFolder.GetStorageItem().ConfigureAwait(true) is StorageFolder Folder)
                        {
                            StorageFile ExportFile = await Folder.CreateFileAsync($"{TabTarget.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                            await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference).ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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
                    if (await Container.CurrentFolder.GetStorageItem().ConfigureAwait(true) is StorageFolder Folder)
                    {
                        StorageFile ExportFile = await Folder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                        await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding).ConfigureAwait(true);
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            ProgramPickerDialog Dialog = new ProgramPickerDialog(SelectedItem);

            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                {
                    switch (SelectedItem.Type.ToLower())
                    {
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(PhotoViewer), SelectedItem.Path, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(PhotoViewer), SelectedItem.Path, new SuppressNavigationTransitionInfo());
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
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(MediaPlayer), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(MediaPlayer), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                        case ".txt":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(TextViewer), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(TextViewer), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                        case ".pdf":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(PdfReader), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(PdfReader), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                    }
                }
                else
                {
                    if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                    {
                    Retry:
                        try
                        {
                            await FullTrustProcessController.Current.RunAsync(Dialog.SelectedProgram.Path, false, false, false, SelectedItem.Path).ConfigureAwait(true);
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
                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                    else
                    {
                        if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
                        {
                            if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                            {
                                if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                                {
                                    ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace($"{Item.FileType}|{Item.Name};", string.Empty);
                                }

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
                                        LauncherOptions Options = new LauncherOptions
                                        {
                                            DisplayApplicationPicker = true
                                        };

                                        _ = await Launcher.LaunchFileAsync(Item, Options);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private async void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await EnterSelectedItem(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private async void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Name, SortDirection.Descending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Name, SortDirection.Ascending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Descending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Ascending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Type, SortDirection.Descending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Type, SortDirection.Ascending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Size, SortDirection.Descending).ConfigureAwait(true);

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);
                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    FileCollection.Add(Item);
                }

            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Size, SortDirection.Ascending).ConfigureAwait(true);

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
            CloseAllFlyout();

            NewFileDialog Dialog = new NewFileDialog();

            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                try
                {
                    switch (Path.GetExtension(Dialog.NewFileName))
                    {
                        case ".zip":
                            {
                                SpecialTypeGenerator.Current.CreateZipFile(Container.CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".rtf":
                            {
                                SpecialTypeGenerator.Current.CreateRtfFile(Container.CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".xlsx":
                            {
                                SpecialTypeGenerator.Current.CreateExcelFile(Container.CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".lnk":
                            {
                                LinkOptionsDialog dialog = new LinkOptionsDialog();

                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (!await FullTrustProcessController.Current.CreateLinkAsync(Path.Combine(Container.CurrentFolder.Path, Dialog.NewFileName), dialog.Path, dialog.Description, dialog.Arguments).ConfigureAwait(true))
                                    {
                                        throw new UnauthorizedAccessException();
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (FileSystemStorageItemBase.Create(Path.Combine(Container.CurrentFolder.Path, Dialog.NewFileName), StorageItemTypes.File, CreateOption.GenerateUniqueName) == null)
                                {
                                    throw new UnauthorizedAccessException();
                                }

                                break;
                            }
                    }
                }
                catch (Exception)
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
                        _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                    }
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (!WIN_Native_API.CheckExist(Item.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                ZipDialog dialog = new ZipDialog(Item);

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, ProgressHandler: (s, e) =>
                    {
                        if (Container.ProBar.Value < e.ProgressPercentage)
                        {
                            Container.ProBar.IsIndeterminate = false;
                            Container.ProBar.Value = e.ProgressPercentage;
                        }
                    }).ConfigureAwait(true);

                    await Container.LoadingActivation(false).ConfigureAwait(true);
                }
            }
        }

        private async void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Html))
                {
                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Container.CurrentFolder.Name}";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Container.CurrentFolder.Name}";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                }
                else if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Container.CurrentFolder.Name}";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Container.CurrentFolder.Name}";
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void Item_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref DropLock, 1) == 0)
            {
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Html))
                    {
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
                                switch (e.AcceptedOperation)
                                {
                                    case DataPackageOperation.Copy:
                                        {
                                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                        Retry:
                                            try
                                            {
                                                await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, Item.Path, (s, arg) =>
                                                {
                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                    {
                                                        Container.ProBar.IsIndeterminate = false;
                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                    }
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
                                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                        Retry:
                                            try
                                            {
                                                await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, Item.Path, (s, arg) =>
                                                {
                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                    {
                                                        Container.ProBar.IsIndeterminate = false;
                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                    }
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
                                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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

                        if ((sender as SelectorItem).Content is FileSystemStorageItemBase TargetFolder)
                        {
                            if (DragItemList.Any((Item) => Item.Path.Equals(TargetFolder.Path, StringComparison.OrdinalIgnoreCase)))
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
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(DragItemList.Select((Item) => Item.Path), TargetFolder.Path, (s, arg) =>
                                              {
                                                  if (Container.ProBar.Value < arg.ProgressPercentage)
                                                  {
                                                      Container.ProBar.IsIndeterminate = false;
                                                      Container.ProBar.Value = arg.ProgressPercentage;
                                                  }
                                              }).ConfigureAwait(true);

                                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                                            {
                                                await Container.FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(DragItemList.Select((Item) => Item.Path), TargetFolder.Path, (s, arg) =>
                                              {
                                                  if (Container.ProBar.Value < arg.ProgressPercentage)
                                                  {
                                                      Container.ProBar.IsIndeterminate = false;
                                                      Container.ProBar.Value = arg.ProgressPercentage;
                                                  }
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                    await Container.LoadingActivation(false).ConfigureAwait(true);

                    _ = Interlocked.Exchange(ref DropLock, 0);
                }
            }
        }

        private void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
                args.ItemContainer.Drop -= Item_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            }
            else
            {
                args.ItemContainer.UseSystemFocusVisuals = false;

                if (args.Item is FileSystemStorageItemBase Item)
                {
                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        args.ItemContainer.AllowDrop = true;
                        args.ItemContainer.Drop += Item_Drop;
                        args.ItemContainer.DragOver += ItemContainer_DragOver;
                    }

                    args.ItemContainer.DragStarting += ItemContainer_DragStarting;
                    args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;

                    args.RegisterUpdateCallback(async (s, e) =>
                    {
                        if (e.Item is FileSystemStorageItemBase Item)
                        {
                            await Item.LoadMorePropertyAsync().ConfigureAwait(false);
                        }
                    });
                }
            }
        }

        private async void ItemContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            DragOperationDeferral Deferral = args.GetDeferral();

            try
            {
                List<FileSystemStorageItemBase> DragList = SelectedItems;

                foreach (FileSystemStorageItemBase Item in DragList)
                {
                    if (ItemPresenter.ContainerFromItem(Item) is SelectorItem SItem && SItem.ContentTemplateRoot.FindChildOfType<TextBox>() is TextBox NameEditBox)
                    {
                        NameEditBox.Visibility = Visibility.Collapsed;
                    }
                }

                IEnumerable<FileSystemStorageItemBase> StorageItems = DragList.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                if (StorageItems.Any())
                {
                    List<IStorageItem> TempList = new List<IStorageItem>();

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
                    {
                        if (await StorageItem.GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                        {
                            TempList.Add(Item);
                        }
                    }

                    if (TempList.Count > 0)
                    {
                        args.Data.SetStorageItems(TempList, false);
                    }
                }

                IEnumerable<FileSystemStorageItemBase> NotStorageItems = DragList.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                if (NotStorageItems.Any())
                {
                    StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TransferNotStorageItem</head>");

                    foreach (FileSystemStorageItemBase Item in NotStorageItems)
                    {
                        Builder.Append($"<p>{Item.Path}</p>");
                    }

                    args.Data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void ItemContainer_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if ((sender as SelectorItem)?.Content is FileSystemStorageItemBase Item)
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
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
                    else if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
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
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
                e.Handled = true;
            }
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple && e.KeyModifiers != VirtualKeyModifiers.Control && e.KeyModifiers != VirtualKeyModifiers.Shift)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    SelectedItem = Item;
                }
            }
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref ViewDropLock, 1) == 0)
            {
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            HtmlNodeCollection BodyNode = Document.DocumentNode.SelectNodes("/p");
                            List<string> LinkAndHiddensItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(LinkAndHiddensItemsPath, Container.CurrentFolder.Path, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                        if (LinkAndHiddensItemsPath.All((Item) => Path.GetDirectoryName(Item) == Container.CurrentFolder.Path))
                                        {
                                            return;
                                        }

                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(LinkAndHiddensItemsPath, Container.CurrentFolder.Path, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
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
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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

                        if (DragItemList.Any((Item) => Item.Path.Equals(Container.CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
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
                                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        await FullTrustProcessController.Current.CopyAsync(DragItemList.Select((Item) => Item.Path), Container.CurrentFolder.Path, (s, arg) =>
                                          {
                                              if (Container.ProBar.Value < arg.ProgressPercentage)
                                              {
                                                  Container.ProBar.IsIndeterminate = false;
                                                  Container.ProBar.Value = arg.ProgressPercentage;
                                              }
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
                                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                                    if (DragItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == Container.CurrentFolder.Path))
                                    {
                                        return;
                                    }

                                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        await FullTrustProcessController.Current.MoveAsync(DragItemList.Select((Item) => Item.Path), Container.CurrentFolder.Path, (s, arg) =>
                                          {
                                              if (Container.ProBar.Value < arg.ProgressPercentage)
                                              {
                                                  Container.ProBar.IsIndeterminate = false;
                                                  Container.ProBar.Value = arg.ProgressPercentage;
                                              }
                                          }).ConfigureAwait(true);
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
                                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                    await Container.LoadingActivation(false).ConfigureAwait(true);
                    _ = Interlocked.Exchange(ref ViewDropLock, 0);
                }
            }
        }

        private async void ViewControl_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                e.Handled = true;

                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            MixZip.IsEnabled = true;

                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            SelectedItem = Context;

                            if (Context is HyperlinkStorageItem)
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                            else
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;

                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    MixZip.IsEnabled = true;

                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HyperlinkStorageItem)
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                        else
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HyperlinkStorageItem)
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                            else
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
        }

        private async void MixZip_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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

            if (SelectedItems.Where((Item) => Item.StorageType == StorageItemTypes.Folder).Any((Item) => !WIN_Native_API.CheckExist(Item.Path)))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            else if (SelectedItems.Where((Item) => Item.StorageType == StorageItemTypes.File).Any((Item) => !WIN_Native_API.CheckExist(Item.Path)))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
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
                    ZipDialog dialog = new ZipDialog();

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                        await CreateZipAsync(SelectedItems, dialog.FileName, (int)dialog.Level, ProgressHandler: (s, e) =>
                        {
                            if (Container.ProBar.Value < e.ProgressPercentage)
                            {
                                Container.ProBar.IsIndeterminate = false;
                                Container.ProBar.Value = e.ProgressPercentage;
                            }
                        }).ConfigureAwait(true);

                        await Container.LoadingActivation(false).ConfigureAwait(true);
                    }
                }
                else
                {
                    await UnZipAsync(SelectedItems, (s, e) =>
                    {
                        if (Container.ProBar.Value < e.ProgressPercentage)
                        {
                            Container.ProBar.IsIndeterminate = false;
                            Container.ProBar.Value = e.ProgressPercentage;
                        }
                    }).ConfigureAwait(true);
                }
            }
        }

        private async void TryUnlock_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.File)
            {
                try
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Unlock")).ConfigureAwait(true);

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
                    await Container.LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        private async void CalculateHash_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (WIN_Native_API.CheckExist(SelectedItem.Path))
            {
                try
                {
                    if (HashTeachTip.IsOpen)
                    {
                        HashTeachTip.IsOpen = false;
                    }

                    Hash_SHA1.IsEnabled = false;
                    Hash_SHA256.IsEnabled = false;
                    Hash_MD5.IsEnabled = false;

                    Hash_SHA1.Text = string.Empty;
                    Hash_SHA256.Text = string.Empty;
                    Hash_MD5.Text = string.Empty;

                    await Task.Delay(500).ConfigureAwait(true);

                    HashTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                    HashTeachTip.IsOpen = true;

                    using (CancellationTokenSource HashCancellation = new CancellationTokenSource())
                    {
                        try
                        {
                            HashTeachTip.Tag = HashCancellation;

                            using (FileStream Stream1 = SelectedItem.GetFileStreamFromFile(AccessMode.Read))
                            using (FileStream Stream2 = SelectedItem.GetFileStreamFromFile(AccessMode.Read))
                            using (FileStream Stream3 = SelectedItem.GetFileStreamFromFile(AccessMode.Read))
                            using (SHA256 SHA256Alg = SHA256.Create())
                            using (MD5 MD5Alg = MD5.Create())
                            using (SHA1 SHA1Alg = SHA1.Create())
                            {
                                Task Task1 = SHA256Alg.GetHashAsync(Stream1, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_SHA256.Text = beforeTask.Result;
                                    Hash_SHA256.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                Task Task2 = MD5Alg.GetHashAsync(Stream2, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_MD5.Text = beforeTask.Result;
                                    Hash_MD5.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                Task Task3 = SHA1Alg.GetHashAsync(Stream3, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_SHA1.Text = beforeTask.Result;
                                    Hash_SHA1.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                await Task.WhenAll(Task1, Task2, Task3).ConfigureAwait(true);
                            }
                        }
                        finally
                        {
                            HashTeachTip.Tag = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Error: CalculateHash failed");
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
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
            if (sender.Tag is CancellationTokenSource Source)
            {
                Source.Cancel();
            }
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SQLite.Current.GetTerminalProfileByName(Convert.ToString(ApplicationData.Current.LocalSettings.Values["DefaultTerminal"])).ConfigureAwait(true) is TerminalProfile Profile)
            {
            Retry:
                try
                {
                    await FullTrustProcessController.Current.RunAsync(Profile.Path, Profile.RunAsAdmin, false, false, Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value.Contains("[CurrentLocation]") ? Mat.Value.Replace("[CurrentLocation]", Container.CurrentFolder.Path) : Mat.Value).ToArray()).ConfigureAwait(false);
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
                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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

        private void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(Item.Path);
            }
        }

        private void NameLabel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            TextBlock NameLabel = (TextBlock)sender;

            if ((e.GetCurrentPoint(NameLabel).Properties.IsLeftButtonPressed || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) && SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (SelectedItem == Item)
                    {
                        TimeSpan ClickSpan = DateTimeOffset.Now - LastClickTime;

                        if (ClickSpan.TotalMilliseconds > 1500 && ClickSpan.TotalMilliseconds < 3000)
                        {
                            NameLabel.Visibility = Visibility.Collapsed;

                            if ((NameLabel.Parent as FrameworkElement).FindName("NameEditBox") is TextBox EditBox)
                            {
                                EditBox.Tag = Item;
                                EditBox.Text = NameLabel.Text;
                                EditBox.Visibility = Visibility.Visible;
                                EditBox.Focus(FocusState.Programmatic);
                            }

                            MainPage.ThisPage.IsAnyTaskRunning = true;
                        }
                    }

                    LastClickTime = DateTimeOffset.Now;
                }
            }
        }

        private async void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox NameEditBox = (TextBox)sender;

            if ((NameEditBox?.Parent as FrameworkElement)?.FindName("NameLabel") is TextBlock NameLabel && NameEditBox.Tag is FileSystemStorageItemBase CurrentEditItem)
            {
                try
                {
                    if (!FileSystemItemNameChecker.IsValid(NameEditBox.Text))
                    {
                        InvalidNameTip.Target = NameLabel;
                        InvalidNameTip.IsOpen = true;
                        return;
                    }

                    if (CurrentEditItem.Name == NameEditBox.Text)
                    {
                        return;
                    }

                    if (WIN_Native_API.CheckExist(Path.Combine(Path.GetDirectoryName(CurrentEditItem.Path), NameEditBox.Text)))
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
                        await FullTrustProcessController.Current.RenameAsync(CurrentEditItem.Path, NameEditBox.Text).ConfigureAwait(true);
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
                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
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
                        _ = await Launcher.LaunchFolderPathAsync(Container.CurrentFolder.Path);
                    }
                }
                finally
                {
                    NameEditBox.Visibility = Visibility.Collapsed;

                    NameLabel.Visibility = Visibility.Visible;

                    LastClickTime = DateTimeOffset.MaxValue;

                    MainPage.ThisPage.IsAnyTaskRunning = false;
                }
            }
        }

        private void GetFocus_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ItemPresenter.Focus(FocusState.Programmatic);
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(Item.Path)}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await Ctrl_Z_Click().ConfigureAwait(false);
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

        private async void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Name, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private async void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.ModifiedTime, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private async void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Type, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private async void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortTarget.Size, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private async void Desc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortDirection: SortDirection.Descending).ConfigureAwait(true);

            List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection);

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in SortResult)
            {
                FileCollection.Add(Item);
            }
        }

        private async void Asc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(Container.CurrentFolder.Path, SortDirection: SortDirection.Ascending).ConfigureAwait(true);

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

            AppBarButton MultiSelectButton = new AppBarButton
            {
                Icon = new FontIcon { Glyph = "\uE762" },
                Label = Globalization.GetString("Operate_Text_MultiSelect")
            };
            MultiSelectButton.Click += MulSelect_Click;
            BottomCommandBar.PrimaryCommands.Add(MultiSelectButton);

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

                if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
                {
                    if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                    {
                        if (SelectedItems.All((Item) => Item.Type.ToLower() == ".zip"))
                        {
                            MixZipButtonText = Globalization.GetString("Operate_Text_Decompression");
                        }
                        else if (SelectedItems.All((Item) => Item.Type.ToLower() != ".zip"))
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
                        if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type.ToLower() == ".zip"))
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

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

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
                        PropertyButton.Click += FileProperty_Click;
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

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Tag),
                            Label = Globalization.GetString("Operate_Text_Property")
                        };
                        PropertyButton.Click += FolderProperty_Click;
                        BottomCommandBar.SecondaryCommands.Add(PropertyButton);
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

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.GetString("Operate_Text_Property")
                    };
                    PropertyButton.Click += ParentProperty_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                }
            }
        }

        private void ListHeader_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void LnkOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is HyperlinkStorageItem Item)
            {
                if (string.IsNullOrEmpty(Item.LinkTargetPath))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    if (FileSystemStorageItemBase.Open(Item.LinkTargetPath, ItemFilters.Folder) is FileSystemStorageItemBase ParentFolder)
                    {
                        await Container.DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);

                        if (FileCollection.FirstOrDefault((SItem) => SItem.Path.Equals(Item.LinkTargetPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Target)
                        {
                            ItemPresenter.ScrollIntoView(Target);
                            SelectedItem = Target;
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private async void ViewControlRefreshContainer_RefreshRequested(Microsoft.UI.Xaml.Controls.RefreshContainer sender, Microsoft.UI.Xaml.Controls.RefreshRequestedEventArgs args)
        {
            Windows.Foundation.Deferral Deferral = args.GetDeferral();

            try
            {
                if (WIN_Native_API.CheckExist(Container.CurrentFolder.Path))
                {
                    await Container.DisplayItemsInFolder(Container.CurrentFolder, true).ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Refresh ItemPresenter failed");
            }
            finally
            {
                await Task.Delay(700).ConfigureAwait(true);

                Deferral.Complete();
            }
        }

        private void MulSelect_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (ItemPresenter.SelectionMode == ListViewSelectionMode.Extended)
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Extended;
            }
        }

        private void ViewControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }

        private void ListHeaderRelativePanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                NameFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                ModTimeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                TypeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                SizeFilterBtn.Visibility = Visibility.Visible;
            }
        }

        private void ListHeaderRelativePanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                if (!NameFilterBtn.Flyout.IsOpen)
                {
                    NameFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                if (!ModTimeFilterBtn.Flyout.IsOpen)
                {
                    ModTimeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                if (!TypeFilterBtn.Flyout.IsOpen)
                {
                    TypeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                if (!SizeFilterBtn.Flyout.IsOpen)
                {
                    SizeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void FilterFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            sender.Target.Visibility = Visibility.Collapsed;
        }

        private void Filter_RefreshListRequested(object sender, List<FileSystemStorageItemBase> e)
        {
            FileCollection.Clear();

            foreach (var Item in e)
            {
                FileCollection.Add(Item);
            }
        }
    }
}

