using ICSharpCode.SharpZipLib.Zip;
using OpenCV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Search;
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

namespace FileManager
{
    public sealed partial class FilePresenter : Page
    {
        public IncrementalLoadingCollection<FileSystemStorageItem> FileCollection { get; private set; }
        private static IStorageItem[] CopyFiles;
        private static IStorageItem[] CutFiles;
        private TreeViewNode LastNode;
        public Dictionary<string, bool> SortMap { get; private set; } = new Dictionary<string, bool>
        {
            {"System.ItemNameDisplay",true },
            {"System.Size",true },
            {"System.DateModified",true }
        };

        private FileControl FileControlInstance;

        private bool useGridorList = true;

        private bool IsInputFromPrimaryButton = true;

        public bool UseGridOrList
        {
            get
            {
                return useGridorList;
            }
            set
            {
                useGridorList = value;
                if (value)
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

        WiFiShareProvider WiFiProvider;
        FileSystemStorageItem TabTarget = null;

        private int SelectedIndex
        {
            get
            {
                if (UseGridOrList)
                {
                    return GridViewControl.SelectedIndex;
                }
                else
                {
                    return ListViewControl.SelectedIndex;
                }
            }
            set
            {
                if (UseGridOrList)
                {
                    GridViewControl.SelectedIndex = value;
                }
                else
                {
                    ListViewControl.SelectedIndex = value;
                }
            }
        }

        private FlyoutBase ControlContextFlyout
        {
            get
            {
                if (UseGridOrList)
                {
                    return GridViewControl.ContextFlyout;
                }
                else
                {
                    return ListViewControl.ContextFlyout;
                }
            }
            set
            {
                if (UseGridOrList)
                {
                    GridViewControl.ContextFlyout = value;
                }
                else
                {
                    ListViewControl.ContextFlyout = value;
                }
            }
        }

        private object SelectedItem
        {
            get
            {
                if (UseGridOrList)
                {
                    return GridViewControl.SelectedItem;
                }
                else
                {
                    return ListViewControl.SelectedItem;
                }
            }
            set
            {
                if (UseGridOrList)
                {
                    GridViewControl.SelectedItem = value;
                }
                else
                {
                    ListViewControl.SelectedItem = value;
                }
            }
        }

        private FileSystemStorageItem[] SelectedItems
        {
            get
            {
                if (UseGridOrList)
                {
                    return GridViewControl.SelectedItems.Select((Item) => Item as FileSystemStorageItem).ToArray();
                }
                else
                {
                    return ListViewControl.SelectedItems.Select((Item) => Item as FileSystemStorageItem).ToArray();
                }
            }
        }

        public FilePresenter()
        {
            InitializeComponent();

            FileCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Application.Current.Suspending += Current_Suspending;
            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
            CoreWindow.GetForCurrentThread().PointerPressed -= FilePresenter_PointerPressed;
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
            CoreWindow.GetForCurrentThread().PointerPressed += FilePresenter_PointerPressed;
        }

        private void FilePresenter_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (BackButtonPressed)
            {
                args.Handled = true;
                IsInputFromPrimaryButton = false;

                if (FileControlInstance.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting && FileControlInstance.GoBackRecord.IsEnabled)
                {
                    FileControlInstance.GoBackRecord_Click(null, null);
                }
            }
            else if (ForwardButtonPressed)
            {
                args.Handled = true;
                IsInputFromPrimaryButton = false;

                if (FileControlInstance.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting && FileControlInstance.GoForwardRecord.IsEnabled)
                {
                    FileControlInstance.GoForwardRecord_Click(null, null);
                }
            }
            else
            {
                IsInputFromPrimaryButton = true;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, Frame> Parameters)
            {
                FileControlInstance = Parameters.Item1;

                if (!TabViewContainer.ThisPage.FFInstanceContainer.ContainsKey(Parameters.Item1))
                {
                    TabViewContainer.ThisPage.FFInstanceContainer.Add(Parameters.Item1, this);
                }
            }
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            var WindowInstance = CoreWindow.GetForCurrentThread();
            var CtrlState = WindowInstance.GetKeyState(VirtualKey.Control);
            var ShiftState = WindowInstance.GetKeyState(VirtualKey.Shift);

            if (!FileControlInstance.IsSearchOrPathBoxFocused)
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Delete:
                        {
                            Delete_Click(null, null);
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
                    case VirtualKey.Right when SelectedIndex == -1:
                        {
                            if (UseGridOrList)
                            {
                                GridViewControl.Focus(FocusState.Programmatic);
                            }
                            else
                            {
                                ListViewControl.Focus(FocusState.Programmatic);
                            }
                            SelectedIndex = 0;
                            break;
                        }
                    case VirtualKey.Enter when !QueueContentDialog.IsRunningOrWaiting && SelectedItem is FileSystemStorageItem Item:
                        {
                            if (UseGridOrList)
                            {
                                GridViewControl.Focus(FocusState.Programmatic);
                            }
                            else
                            {
                                ListViewControl.Focus(FocusState.Programmatic);
                            }
                            EnterSelectedItem(Item);
                            break;
                        }
                    case VirtualKey.Back when FileControlInstance.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting && FileControlInstance.GoBackRecord.IsEnabled:
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
                            NewFolder_Click(null, null);
                            break;
                        }
                }
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            WiFiProvider?.Dispose();
        }

        private async Task<IEnumerable<FileSystemStorageItem>> GetMoreItemsFunction(uint Index, uint Num, StorageItemQueryResult Query)
        {
            List<FileSystemStorageItem> ItemList = new List<FileSystemStorageItem>();
            foreach (var Item in await Query.GetItemsAsync(Index, Num))
            {
                var Size = await Item.GetSizeDescriptionAsync().ConfigureAwait(true);
                var Thumbnail = await Item.GetThumbnailBitmapAsync().ConfigureAwait(true) ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync().ConfigureAwait(true);
                ItemList.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }
            return ItemList;
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
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CutFiles != null)
            {
                CutFiles = null;
            }

            List<IGrouping<ContentType, FileSystemStorageItem>> GroupItem = SelectedItems.GroupBy((Item) => Item.ContentType).ToList();
            CopyFiles = GroupItem.Where((Item) => Item.Key == ContentType.File).Select((It) => (IStorageItem)It.FirstOrDefault().File).Concat(GroupItem.Where((Item) => Item.Key == ContentType.Folder).Select((It) => (IStorageItem)It.FirstOrDefault().Folder)).ToArray();
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CutFiles != null)
            {
                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在剪切" : "Cutting").ConfigureAwait(true);

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;
                bool IsCaptured = false;

                foreach (IStorageItem Item in CutFiles)
                {
                    try
                    {
                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            await File.MoveAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                            await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            if (FileCollection.Where((It) => It.ContentType == ContentType.Folder).All((Item) => Item.Folder.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }

                            if (LastNode.IsExpanded)
                            {
                                LastNode.Children.Remove(LastNode.Children.Where((Node) => (Node.Content as StorageFolder).Name == Folder.Name).FirstOrDefault());
                            }
                            else
                            {
                                if ((await (LastNode.Content as StorageFolder).CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync()) == 0)
                                {
                                    LastNode.HasUnrealizedChildren = false;
                                }
                            }

                            if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                            {
                                if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as StorageFolder).Name == NewFolder.Name) is TreeViewNode ExistNode)
                                {
                                    ExistNode.HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0;
                                }
                                else
                                {
                                    FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                    {
                                        Content = NewFolder,
                                        HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0
                                    });
                                }
                            }
                            FileControlInstance.CurrentNode.IsExpanded = true;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        IsUnauthorized = true;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        IsSpaceError = true;
                    }
                    catch (Exception)
                    {
                        IsCaptured = true;
                    }
                }

                if (IsItemNotFound)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "部分文件不存在，无法移动到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files do not exist and cannot be moved to the specified location",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else if (IsUnauthorized)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                }
                else if (IsSpaceError)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，部分文件无法移动",
                            CloseButtonText = "确定"
                        };
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files cannot be moved due to insufficient free space on the device",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else if (IsCaptured)
                {
                    QueueContentDialog dialog;

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "部分文件正在被其他应用程序使用，因此无法移动",
                            CloseButtonText = "确定"
                        };
                    }
                    else
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files are in use by other applications and cannot be moved",
                            CloseButtonText = "Got it"
                        };
                    }

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else if (CopyFiles != null)
            {
                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在复制" : "Copying").ConfigureAwait(true);

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;

                foreach (IStorageItem Item in CopyFiles)
                {
                    try
                    {
                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFile NewFile = await File.CopyAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                            if (FileCollection.Where((It) => It.ContentType == ContentType.Folder).All((Item) => Item.Folder.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }

                            if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                            {
                                if (FileControlInstance.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as StorageFolder).Name == NewFolder.Name) is TreeViewNode ExistNode)
                                {
                                    ExistNode.HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0;
                                }
                                else
                                {
                                    FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                                    {
                                        Content = NewFolder,
                                        HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0
                                    });
                                }
                            }
                            FileControlInstance.CurrentNode.IsExpanded = true;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        IsUnauthorized = true;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        IsSpaceError = true;
                    }
                }

                if (IsItemNotFound)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "部分文件不存在，无法复制到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files do not exist and cannot be copyed to the specified location",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else if (IsUnauthorized)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                }
                else if (IsSpaceError)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，部分文件无法复制",
                            CloseButtonText = "确定"
                        };
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files cannot be copyed due to insufficient free space on the device",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }

            CutFiles = null;
            CopyFiles = null;
            Paste.IsEnabled = false;

            await LoadingActivation(false).ConfigureAwait(false);
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CopyFiles != null)
            {
                CopyFiles = null;
            }

            LastNode = FileControlInstance.CurrentNode;

            List<IGrouping<ContentType, FileSystemStorageItem>> GroupItem = SelectedItems.GroupBy((Item) => Item.ContentType).ToList();
            CutFiles = GroupItem.Where((Item) => Item.Key == ContentType.File).Select((It) => (IStorageItem)It.FirstOrDefault().File).Concat(GroupItem.Where((Item) => Item.Key == ContentType.Folder).Select((It) => (IStorageItem)It.FirstOrDefault().Folder)).ToArray();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            bool IsItemNotFound = false;
            bool IsUnauthorized = false;
            bool IsCaptured = false;

            if (SelectedItems.Length == 1)
            {
                FileSystemStorageItem ItemToDelete = SelectedItems.FirstOrDefault();

                QueueContentDialog QueueContenDialog;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContenDialog = new QueueContentDialog
                    {
                        Title = "警告",
                        PrimaryButtonText = "是",
                        Content = "此操作将永久删除 \" " + ItemToDelete.Name + " \"\r\r是否继续?",
                        CloseButtonText = "否"
                    };
                }
                else
                {
                    QueueContenDialog = new QueueContentDialog
                    {
                        Title = "Warning",
                        PrimaryButtonText = "Continue",
                        Content = "This action will permanently delete \" " + ItemToDelete.Name + " \"\r\rWhether to continue?",
                        CloseButtonText = "Cancel"
                    };
                }

                if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在删除" : "Deleting").ConfigureAwait(true);

                    if (ItemToDelete.ContentType == ContentType.File)
                    {
                        if (!await ItemToDelete.File.CheckExist().ConfigureAwait(true))
                        {
                            IsItemNotFound = true;
                        }

                        try
                        {
                            await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            FileCollection.Remove(ItemToDelete);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            IsUnauthorized = true;
                        }
                        catch (Exception)
                        {
                            IsCaptured = true;
                        }
                    }
                    else
                    {
                        if (!await ItemToDelete.Folder.CheckExist().ConfigureAwait(true))
                        {
                            IsItemNotFound = true;
                        }

                        try
                        {
                            await ItemToDelete.Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                            await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            FileCollection.Remove(ItemToDelete);

                            if (FileControlInstance.CurrentNode.IsExpanded)
                            {
                                FileControlInstance.CurrentNode.Children.Remove(FileControlInstance.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).Name == ItemToDelete.Name).FirstOrDefault());
                            }
                            else
                            {
                                if ((await FileControlInstance.CurrentFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync()) == 0)
                                {
                                    FileControlInstance.CurrentNode.HasUnrealizedChildren = false;
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            IsUnauthorized = true;
                        }
                        catch (Exception)
                        {
                            IsCaptured = true;
                        }
                    }
                }
            }
            else
            {
                QueueContentDialog QueueContenDialog;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContenDialog = new QueueContentDialog
                    {
                        Title = "警告",
                        PrimaryButtonText = "是",
                        Content = "此操作将永久删除这 " + SelectedItems.Length + " 项\r\r是否继续?",
                        CloseButtonText = "否"
                    };
                }
                else
                {
                    QueueContenDialog = new QueueContentDialog
                    {
                        Title = "Warning",
                        PrimaryButtonText = "Continue",
                        Content = "This action will permanently delete these " + SelectedItems.Length + " items\r\rWhether to continue?",
                        CloseButtonText = "Cancel"
                    };
                }

                if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在删除" : "Deleting").ConfigureAwait(true);

                    foreach (FileSystemStorageItem ItemToDelete in SelectedItems)
                    {
                        if (ItemToDelete.ContentType == ContentType.File)
                        {
                            if (!await ItemToDelete.File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                            }

                            try
                            {
                                await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                FileCollection.Remove(ItemToDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                IsUnauthorized = true;
                            }
                            catch (Exception)
                            {
                                IsCaptured = true;
                            }
                        }
                        else
                        {
                            if (!await ItemToDelete.Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                            }

                            try
                            {
                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                FileCollection.Remove(ItemToDelete);

                                if (FileControlInstance.CurrentNode.IsExpanded)
                                {
                                    FileControlInstance.CurrentNode.Children.Remove(FileControlInstance.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).Name == ItemToDelete.Name).FirstOrDefault());
                                }
                                else
                                {
                                    if ((await FileControlInstance.CurrentFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync()) == 0)
                                    {
                                        FileControlInstance.CurrentNode.HasUnrealizedChildren = false;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                IsUnauthorized = true;
                            }
                            catch (Exception)
                            {
                                IsCaptured = true;
                            }
                        }
                    }
                }
            }

            if (IsItemNotFound)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法删除部分文件/文件夹，该文件/文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to delete some files/folders, the file/folders may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(true);
            }
            else if (IsUnauthorized)
            {
                QueueContentDialog dialog;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权删除此处的文件/文件夹，可能是您无权访问此文件/文件夹\r\r是否立即进入系统文件管理器进行相应操作?",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to delete, it may be that you do not have access to this files/folders\r\rEnter the system file manager immediately ?",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                }

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
            else if (IsCaptured)
            {
                QueueContentDialog dialog;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "部分文件/文件夹正在被其他应用程序使用，因此无法删除",
                        CloseButtonText = "确定"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Some files/folders are in use by other applications and cannot be deleted",
                        CloseButtonText = "Got it"
                    };
                }

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }

            await LoadingActivation(false).ConfigureAwait(false);
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        /// <param name="DisableProbarIndeterminate">是否使用条状进度条替代圆形进度条</param>
        private async Task LoadingActivation(bool IsLoading, string Info = null)
        {
            if (IsLoading)
            {
                if (HasFile.Visibility == Visibility.Visible)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                }

                ProgressInfo.Text = Info + "...";

                MainPage.ThisPage.IsAnyTaskRunning = true;
            }
            else
            {
                await Task.Delay(1000).ConfigureAwait(true);
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }

            LoadingControl.IsLoading = IsLoading;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Length > 1)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "此操作一次仅允许重命名一个对象",
                        CloseButtonText = "确定"
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "This operation allows only one object to be renamed at a time",
                        CloseButtonText = "Got it"
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                return;
            }

            if (SelectedItem is FileSystemStorageItem RenameItem)
            {
                if (RenameItem.ContentType == ContentType.File)
                {
                    if (!await RenameItem.File.CheckExist().ConfigureAwait(true))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding file, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.File.Name);
                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            if (dialog.DesireName == RenameItem.File.FileType)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "文件名不能为空，重命名失败",
                                    CloseButtonText = "确定"
                                };
                                await content.ShowAsync().ConfigureAwait(true);
                                return;
                            }

                            try
                            {
                                await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权重命名此处的文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后"
                                };
                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                        }
                        else
                        {
                            if (dialog.DesireName == RenameItem.File.FileType)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "File name cannot be empty, rename failed",
                                    CloseButtonText = "Confirm"
                                };
                                await content.ShowAsync().ConfigureAwait(true);
                                return;
                            }

                            try
                            {
                                await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to rename, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later"
                                };
                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!await RenameItem.Folder.CheckExist().ConfigureAwait(true))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.Folder.Name);
                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (string.IsNullOrWhiteSpace(dialog.DesireName))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "文件夹名不能为空，重命名失败",
                                    CloseButtonText = "确定"
                                };
                                await content.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Folder name cannot be empty, rename failed",
                                    CloseButtonText = "Confirm"
                                };
                                await content.ShowAsync().ConfigureAwait(true);
                            }
                            return;
                        }

                        if (FileControlInstance.CurrentNode.Children.Count != 0)
                        {
                            IList<TreeViewNode> ChildCollection = FileControlInstance.CurrentNode.Children;
                            TreeViewNode TargetNode = FileControlInstance.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).Name == RenameItem.Folder.Name).FirstOrDefault();
                            int index = FileControlInstance.CurrentNode.Children.IndexOf(TargetNode);

                            if (TargetNode.HasUnrealizedChildren)
                            {
                                ChildCollection.Insert(index, new TreeViewNode()
                                {
                                    Content = RenameItem.Folder,
                                    HasUnrealizedChildren = true,
                                    IsExpanded = false
                                });
                                ChildCollection.Remove(TargetNode);
                            }
                            else if (TargetNode.HasChildren)
                            {
                                var NewNode = new TreeViewNode()
                                {
                                    Content = RenameItem.Folder,
                                    HasUnrealizedChildren = false,
                                    IsExpanded = true
                                };

                                foreach (var SubNode in TargetNode.Children)
                                {
                                    NewNode.Children.Add(SubNode);
                                }

                                ChildCollection.Insert(index, NewNode);
                                ChildCollection.Remove(TargetNode);
                                await NewNode.UpdateAllSubNodeFolder().ConfigureAwait(true);
                            }
                            else
                            {
                                ChildCollection.Insert(index, new TreeViewNode()
                                {
                                    Content = RenameItem.Folder,
                                    HasUnrealizedChildren = false,
                                    IsExpanded = false
                                });
                                ChildCollection.Remove(TargetNode);
                            }
                        }

                        try
                        {
                            await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后"
                                };
                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to rename the folder, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later"
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

            FileSystemStorageItem ShareFile = SelectedItem as FileSystemStorageItem;

            if (!await ShareFile.File.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

            if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
            {
                BluetoothUI Bluetooth = new BluetoothUI();
                if ((await Bluetooth.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer(ShareFile.File);

                    _ = await FileTransfer.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "请开启蓝牙开关后再试",
                        CloseButtonText = "确定"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "Please turn on Bluetooth and try again.",
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private void GridViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (SelectedItem is FileSystemStorageItem Item)
                {
                    if (Item.ContentType == ContentType.File)
                    {
                        Transcode.IsEnabled = false;
                        VideoEdit.IsEnabled = false;
                        VideoMerge.IsEnabled = false;
                        ChooseOtherApp.IsEnabled = true;
                        RunWithSystemAuthority.IsEnabled = false;

                        Zip.Label = Globalization.Language == LanguageEnum.Chinese
                                    ? "Zip压缩"
                                    : "Zip Compression";
                        switch (Item.Type)
                        {
                            case ".zip":
                                {
                                    Zip.Label = Globalization.Language == LanguageEnum.Chinese
                                                ? "Zip解压"
                                                : "Zip Decompression";
                                    break;
                                }
                            case ".mp4":
                            case ".wmv":
                                {
                                    VideoEdit.IsEnabled = true;
                                    Transcode.IsEnabled = true;
                                    VideoMerge.IsEnabled = true;
                                    break;
                                }
                            case ".mkv":
                            case ".m4a":
                            case ".mov":
                                {
                                    Transcode.IsEnabled = true;
                                    break;
                                }
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
                                    Transcode.IsEnabled = true;
                                    break;
                                }
                            case ".exe":
                                {
                                    ChooseOtherApp.IsEnabled = false;
                                    RunWithSystemAuthority.IsEnabled = true;
                                    break;
                                }
                        }
                    }
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SelectedIndex = -1;
            FileControlInstance.IsSearchOrPathBoxFocused = false;
        }

        private void GridViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
            {
                SelectedIndex = FileCollection.IndexOf(Context);

                if (Context.ContentType == ContentType.Folder)
                {
                    ControlContextFlyout = FolderFlyout;
                }
                else
                {
                    ControlContextFlyout = FileFlyout;
                }
            }
            else
            {
                ControlContextFlyout = EmptyFlyout;
            }

            e.Handled = true;
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Device = SelectedItem as FileSystemStorageItem;

            if (!await Device.File.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.File);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Item = SelectedItem as FileSystemStorageItem;

            if (!await Item.File.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            if (Item.Type == ".zip")
            {
                if ((await UnZipAsync(Item.File).ConfigureAwait(true)) is StorageFolder NewFolder)
                {
                    TreeViewNode CurrentNode = null;
                    if (FileControlInstance.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
                    {
                        FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                        if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                        {
                            CurrentNode = new TreeViewNode
                            {
                                Content = NewFolder,
                                HasUnrealizedChildren = (await NewFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count > 0
                            };
                            FileControlInstance.CurrentNode.Children.Add(CurrentNode);
                        }
                        FileControlInstance.CurrentNode.IsExpanded = true;
                    }
                }
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在压缩" : "Compressing").ConfigureAwait(true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(Item.File, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                    }
                    else
                    {
                        await CreateZipAsync(Item.File, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                    }
                }
            }

            await LoadingActivation(false).ConfigureAwait(false);
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
                            await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在解压" : "Extracting").ConfigureAwait(true);
                            ZipEntries.Password = Dialog.Password;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在解压" : "Extracting").ConfigureAwait(true);
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
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权在此处解压Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX does not have permission to extract the Zip file here, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "解压文件时发生异常\r\r错误信息：\r\r" + e.Message,
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An exception occurred while extracting the file\r\rError Message：\r\r" + e.Message,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
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
                            await ZipFolderCore(ZipFolder, OutputStream, string.Empty, EnableCryption, Size, Password).ConfigureAwait(true);
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                        OutputStream.Finish();
                    }
                    catch (Exception e)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "An exception occurred while compressing the file\r\rError Message：\r\r" + e.Message,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }

                if (FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File) is FileSystemStorageItem Item)
                {
                    FileCollection.Insert(FileCollection.IndexOf(Item), new FileSystemStorageItem(Newfile, await Newfile.GetSizeDescriptionAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
                else
                {
                    FileCollection.Add(new FileSystemStorageItem(Newfile, await Newfile.GetSizeDescriptionAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
            }
            catch (UnauthorizedAccessException)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此处创建Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create the Zip file here, it may be that you do not have access to this file.\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
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
                        if (string.IsNullOrEmpty(BaseFolderName))
                        {
                            if (EnableCryption)
                            {
                                await ZipFolderCore(InnerFolder, OutputStream, $"{InnerFolder.Name}/", true, Size, Password).ConfigureAwait(false);
                            }
                            else
                            {
                                await ZipFolderCore(InnerFolder, OutputStream, $"{InnerFolder.Name}/").ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            if (EnableCryption)
                            {
                                await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}{InnerFolder.Name}/", true, Size, Password).ConfigureAwait(false);
                            }
                            else
                            {
                                await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}{InnerFolder.Name}/").ConfigureAwait(false);
                            }
                        }
                    }
                    else if (Item is StorageFile InnerFile)
                    {
                        if (string.IsNullOrEmpty(BaseFolderName))
                        {
                            if (EnableCryption)
                            {
                                using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry(InnerFile.Name)
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
                                using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry(InnerFile.Name)
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
                        else
                        {
                            if (EnableCryption)
                            {
                                using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}{InnerFile.Name}")
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
                                using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(false))
                                {
                                    ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}{InnerFile.Name}")
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
        }

        private void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (IsInputFromPrimaryButton && (e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Source)
            {
                if (!await Source.File.CheckExist().ConfigureAwait(true))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                    return;
                }

                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "已存在正在进行中的任务，请等待其完成",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "There is already an ongoing task, please wait for it to complete",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    return;
                }

                switch (Source.Type)
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
                            TranscodeDialog dialog = new TranscodeDialog(Source.File);

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    StorageFile DestinationFile = await FileControlInstance.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.GenerateUniqueName);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source.File, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp).ConfigureAwait(true);

                                    if (Path.GetDirectoryName(DestinationFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string Status && Status == "Success")
                                    {
                                        FileCollection.Add(new FileSystemStorageItem(DestinationFile, await DestinationFile.GetSizeDescriptionAsync().ConfigureAwait(true), await DestinationFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await DestinationFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "RX无权在此处创建转码文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                            PrimaryButtonText = "立刻",
                                            CloseButtonText = "稍后"
                                        };
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "RX does not have permission to create transcode file, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                            PrimaryButtonText = "Enter",
                                            CloseButtonText = "Later"
                                        };
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                        }
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
                            using (var OriginStream = await Source.File.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                            }

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在转码" : "Transcoding").ConfigureAwait(true);

                                await GeneralTransformer.TranscodeFromImageAsync(Source.File, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                                await LoadingActivation(false).ConfigureAwait(true);
                            }
                            break;
                        }
                }
            }
        }

        private void FolderOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Item)
            {
                EnterSelectedItem(Item);
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Device = SelectedItem as FileSystemStorageItem;
            if (!await Device.Folder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.Folder);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Item = SelectedItem as FileSystemStorageItem;

            if (!await Item.File.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            if (QRTeachTip.IsOpen)
            {
                QRTeachTip.IsOpen = false;
            }

            while (WiFiProvider != null)
            {
                await Task.Delay(300).ConfigureAwait(true);
            }

            WiFiProvider = new WiFiShareProvider();
            WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;

            string Hash = ComputeMD5Hash(Item.Path);
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
            using (SoftwareBitmap TransferImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 400, 250, BitmapAlphaMode.Premultiplied))
            {
                OpenCVLibrary.ExtendImageBorder(PreTransImage, TransferImage, Colors.White, 0, 75, 75, 0);
                SoftwareBitmapSource Source = new SoftwareBitmapSource();
                QRImage.Source = Source;
                await Source.SetBitmapAsync(TransferImage);
            }

            if (UseGridOrList)
            {
                QRTeachTip.Target = GridViewControl.ContainerFromItem(Item) as GridViewItem;
            }
            else
            {
                QRTeachTip.Target = ListViewControl.ContainerFromItem(Item) as ListViewItem;
            }

            QRTeachTip.IsOpen = true;

            await WiFiProvider.StartToListenRequest().ConfigureAwait(false);
        }

        public static string ComputeMD5Hash(string Data)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(Data));

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    _ = builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "WIFI传输出现意外错误：\r" + e.Message,
                        CloseButtonText = "确定"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "WIFI transmission has an unexpected error：\r" + e.Message,
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
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

        private async void ParentAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            if (FileControlInstance.CurrentNode == FileControlInstance.FolderTree.RootNodes.FirstOrDefault())
            {
                if (TabViewContainer.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == FileControlInstance.CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    AttributeDialog Dialog = new AttributeDialog(FileControlInstance.CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog(FileControlInstance.CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void AddToLibray_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder folder = (SelectedItem as FileSystemStorageItem).Folder;

            if (!await folder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            if (TabViewContainer.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "This folder has been added to the home page, can not be added repeatedly",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync().ConfigureAwait(true);
                TabViewContainer.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserCustom));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path).ConfigureAwait(false);
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            try
            {
                var NewFolder = Globalization.Language == LanguageEnum.Chinese
                    ? await FileControlInstance.CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await FileControlInstance.CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

                var Size = await NewFolder.GetSizeDescriptionAsync().ConfigureAwait(true);
                var Thumbnail = await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true) ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true);

                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, Size, Thumbnail, ModifiedTime));

                if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                {
                    FileControlInstance.CurrentNode.Children.Add(new TreeViewNode
                    {
                        Content = NewFolder,
                        HasUnrealizedChildren = false
                    });
                }
                FileControlInstance.CurrentNode.IsExpanded = true;
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog;
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                }

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        private void EmptyFlyout_Opening(object sender, object e)
        {
            if (CutFiles != null || CopyFiles != null)
            {
                Paste.IsEnabled = true;
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem ShareItem)
            {
                if (!await ShareItem.File.CheckExist().ConfigureAwait(true))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                    return;
                }

                DataTransferManager.GetForCurrentView().DataRequested += (s, args) =>
                {
                    DataPackage Package = new DataPackage();
                    Package.Properties.Title = ShareItem.DisplayName;
                    Package.Properties.Description = ShareItem.DisplayType;
                    Package.SetStorageItems(new StorageFile[] { ShareItem.File });
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
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
        }

        private void GridViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            FileControlInstance.IsSearchOrPathBoxFocused = false;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void EnterSelectedItem(FileSystemStorageItem ReFile, bool RunAsAdministrator = false)
        {
            try
            {
                if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
                {
                    if (TabTarget.ContentType == ContentType.File)
                    {
                        if (!await TabTarget.File.CheckExist().ConfigureAwait(true))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                    CloseButtonText = "刷新"
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Could not find the corresponding file, it may have been moved or deleted",
                                    CloseButtonText = "Refresh"
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        string AdminExcuteProgram = null;
                        if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                        {
                            string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == TabTarget.Type);
                            if (!string.IsNullOrEmpty(SaveUnit))
                            {
                                AdminExcuteProgram = SaveUnit.Split('|')[1];
                            }
                        }

                        if (!string.IsNullOrEmpty(AdminExcuteProgram) && AdminExcuteProgram != "RX内置查看器" && AdminExcuteProgram != "RX built-in viewer")
                        {
                            if ((await Launcher.FindFileHandlersAsync(TabTarget.Type)).FirstOrDefault((Item) => Item.DisplayInfo.DisplayName == AdminExcuteProgram) is AppInfo Info)
                            {
                                await Launcher.LaunchFileAsync(TabTarget.File, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false });
                            }
                            else
                            {
                                List<string> PickerRecord = await SQLite.Current.GetProgramPickerRecordAsync().ConfigureAwait(false);
                                foreach (var Path in PickerRecord)
                                {
                                    try
                                    {
                                        StorageFile ExcuteFile = await StorageFile.GetFileFromPathAsync(Path);
                                        string AppName = (await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"].ToString();
                                        if (AppName == AdminExcuteProgram)
                                        {
                                            await FullTrustExcutorController.Run(Path, TabTarget.Path).ConfigureAwait(false);
                                            break;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        await SQLite.Current.DeleteProgramPickerRecordAsync(Path).ConfigureAwait(false);
                                    }
                                }

                            }
                        }
                        else
                        {
                            switch (TabTarget.File.FileType)
                            {
                                case ".jpg":
                                case ".png":
                                case ".bmp":
                                case ".heic":
                                case ".gif":
                                case ".tiff":
                                    {
                                        FileControlInstance.Nav.Navigate(typeof(PhotoViewer), new Tuple<FileControl, string>(FileControlInstance, TabTarget.File.FolderRelativeId), new DrillInNavigationTransitionInfo());
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
                                        FileControlInstance.Nav.Navigate(typeof(MediaPlayer), TabTarget.File, new DrillInNavigationTransitionInfo());
                                        break;
                                    }
                                case ".txt":
                                    {
                                        FileControlInstance.Nav.Navigate(typeof(TextViewer), new Tuple<FileControl, FileSystemStorageItem>(FileControlInstance, TabTarget), new DrillInNavigationTransitionInfo());
                                        break;
                                    }
                                case ".pdf":
                                    {
                                        FileControlInstance.Nav.Navigate(typeof(PdfReader), new Tuple<Frame, StorageFile>(FileControlInstance.Nav, TabTarget.File), new DrillInNavigationTransitionInfo());
                                        break;
                                    }
                                case ".exe":
                                    {
                                        if (RunAsAdministrator)
                                        {
                                            await FullTrustExcutorController.RunAsAdministrator(TabTarget.Path).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await FullTrustExcutorController.Run(TabTarget.Path).ConfigureAwait(false);
                                        }
                                        break;
                                    }
                                default:
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget.File);
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.OpenFailed)
                                            {
                                                if (Globalization.Language == LanguageEnum.Chinese)
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = "提示",
                                                        Content = "  RX文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                                                        PrimaryButtonText = "默认应用打开",
                                                        CloseButtonText = "取消"
                                                    };
                                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(TabTarget.File))
                                                        {
                                                            LauncherOptions options = new LauncherOptions
                                                            {
                                                                DisplayApplicationPicker = true
                                                            };
                                                            _ = await Launcher.LaunchFileAsync(TabTarget.File, options);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    QueueContentDialog dialog = new QueueContentDialog
                                                    {
                                                        Title = "Tips",
                                                        Content = "  RX FileManager could not open this file\r\r  But it can be opened with other applications",
                                                        PrimaryButtonText = "Open with default app",
                                                        CloseButtonText = "Cancel"
                                                    };
                                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(TabTarget.File))
                                                        {
                                                            LauncherOptions options = new LauncherOptions
                                                            {
                                                                DisplayApplicationPicker = true
                                                            };
                                                            _ = await Launcher.LaunchFileAsync(TabTarget.File, options);
                                                        }
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
                        if (!await ReFile.Folder.CheckExist().ConfigureAwait(true))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到对应的文件夹，该文件可能已被移动或删除",
                                    CloseButtonText = "刷新"
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                    CloseButtonText = "Refresh"
                                };
                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        if (FileControlInstance.CurrentNode.HasUnrealizedChildren && !FileControlInstance.CurrentNode.IsExpanded)
                        {
                            FileControlInstance.CurrentNode.IsExpanded = true;
                        }

                        while (true)
                        {
                            TreeViewNode TargetNode = FileControlInstance.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).Name == TabTarget.Name).FirstOrDefault();
                            if (TargetNode != null)
                            {
                                FileControlInstance.FolderTree.SelectNode(TargetNode);
                                await FileControlInstance.DisplayItemsInFolder(TargetNode).ConfigureAwait(true);
                                break;
                            }
                            else
                            {
                                await Task.Delay(200).ConfigureAwait(true);
                            }
                        }
                        //TreeViewNode Node = new TreeViewNode
                        //{
                        //    Content = TabTarget.Folder
                        //};
                        //await FileControlInstance.DisplayItemsInFolder(Node).ConfigureAwait(true);
                    }
                    Interlocked.Exchange(ref TabTarget, null);
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "已存在正在进行中的任务，请等待其完成",
                        CloseButtonText = "确定"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "There is already an ongoing task, please wait for it to complete",
                        CloseButtonText = "Got it"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            if (SelectedItem is FileSystemStorageItem Item)
            {
                VideoEditDialog Dialog = new VideoEditDialog(Item.File);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {(Globalization.Language == LanguageEnum.Chinese ? "裁剪" : "Cropped")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference).ConfigureAwait(true);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeDescriptionAsync().ConfigureAwait(true), await ExportFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await ExportFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
            }
        }

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "已存在正在进行中的任务，请等待其完成",
                        CloseButtonText = "确定"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "There is already an ongoing task, please wait for it to complete",
                        CloseButtonText = "Got it"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            if (SelectedItem is FileSystemStorageItem Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item.File);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {(Globalization.Language == LanguageEnum.Chinese ? "合并" : "Merged")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding).ConfigureAwait(true);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeDescriptionAsync().ConfigureAwait(true), await ExportFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await ExportFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Item)
            {
                ProgramPickerDialog Dialog = new ProgramPickerDialog(Item.File);
                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    if (Dialog.OpenFailed)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "提示",
                                Content = "  RX文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                                PrimaryButtonText = "默认应用打开",
                                CloseButtonText = "取消"
                            };
                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (!await Launcher.LaunchFileAsync(Item.File))
                                {
                                    LauncherOptions options = new LauncherOptions
                                    {
                                        DisplayApplicationPicker = true
                                    };
                                    _ = await Launcher.LaunchFileAsync(Item.File, options);
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Tips",
                                Content = "  RX FileManager could not open this file\r\r  But it can be opened with other applications",
                                PrimaryButtonText = "Open with default app",
                                CloseButtonText = "Cancel"
                            };
                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                if (!await Launcher.LaunchFileAsync(Item.File))
                                {
                                    LauncherOptions options = new LauncherOptions
                                    {
                                        DisplayApplicationPicker = true
                                    };
                                    _ = await Launcher.LaunchFileAsync(Item.File, options);
                                }
                            }
                        }
                    }
                    else if (Dialog.ContinueUseInnerViewer)
                    {
                        EnterSelectedItem(Item);
                    }
                }
            }
        }

        private void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile, true);
            }
        }

        private async void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            SortMap["System.ItemNameDisplay"] = !SortMap["System.ItemNameDisplay"];
            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true, new KeyValuePair<string, bool>[] { new KeyValuePair<string, bool>("System.ItemNameDisplay", SortMap["System.ItemNameDisplay"]) }).ConfigureAwait(false);
        }

        private async void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            SortMap["System.DateModified"] = !SortMap["System.DateModified"];
            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true, new KeyValuePair<string, bool>[] { new KeyValuePair<string, bool>("System.DateModified", SortMap["System.DateModified"]) }).ConfigureAwait(false);
        }

        private async void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            SortMap["System.Size"] = !SortMap["System.Size"];
            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true, new KeyValuePair<string, bool>[] { new KeyValuePair<string, bool>("System.Size", SortMap["System.Size"]) }).ConfigureAwait(false);
        }

        private void QRTeachTip_Closing(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosingEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
            WiFiProvider = null;
        }

        private async void NewFile_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            NewFileDialog Dialog = new NewFileDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                try
                {
                    StorageFile NewFile = await FileControlInstance.CurrentFolder.CreateFileAsync(Dialog.NewFileName, CreationCollisionOption.GenerateUniqueName);

                    int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                    if (Index == -1)
                    {
                        FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                    else
                    {
                        FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX没有足够的权限在此文件夹新建文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have sufficient permissions to create new files in this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                        }
                    }
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Item = SelectedItem as FileSystemStorageItem;

            if (!await Item.Folder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                }
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                return;
            }

            ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在压缩" : "Compressing").ConfigureAwait(true);

                if (dialog.IsCryptionEnable)
                {
                    await CreateZipAsync(Item.Folder, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                }
                else
                {
                    await CreateZipAsync(Item.Folder, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                }
            }

            await LoadingActivation(false).ConfigureAwait(true);
        }
    }
}

