using ICSharpCode.SharpZipLib.Core;
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
        public IncrementalLoadingCollection<FileSystemStorageItem> FileCollection;
        public static FilePresenter ThisPage { get; private set; }
        public List<GridViewItem> ZipCollection = new List<GridViewItem>();
        private static IStorageItem[] CopyFiles;
        private static IStorageItem[] CutFiles;
        private TreeViewNode LastNode;

        private bool useGridorList = true;

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

        Frame Nav;
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
            ThisPage = this;

            FileCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            //必须注册这个东西才能使用中文解码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Application.Current.Suspending += Current_Suspending;
        }

        private void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name == nameof(FileControl))
            {
                var WindowInstance = CoreWindow.GetForCurrentThread();
                var CtrlState = WindowInstance.GetKeyState(VirtualKey.Control);
                var ShiftState = WindowInstance.GetKeyState(VirtualKey.Shift);

                if (!FileControl.ThisPage.IsSearchOrPathBoxFocused)
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
                        case VirtualKey.Back when FileControl.ThisPage.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting:
                            {
                                FileControl.ThisPage.GoParentFolder_Click(null, null);
                                break;
                            }
                        case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                FileControl.ThisPage.AddressBox.Focus(FocusState.Programmatic);
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
                                FileControl.ThisPage.GlobeSearch.Focus(FocusState.Programmatic);
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
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            WiFiProvider?.Dispose();
        }

        private async Task<IEnumerable<FileSystemStorageItem>> GetMoreItemsFunction(uint Index, uint Num, StorageItemQueryResult Query)
        {
            List<FileSystemStorageItem> ItemList = new List<FileSystemStorageItem>();
            foreach (var Item in await Query.GetItemsAsync(Index, Num))
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
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

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Nav = e.Parameter as Frame;
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
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
                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在剪切" : "Cutting");

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;

                foreach (IStorageItem Item in CutFiles)
                {
                    try
                    {
                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist())
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            await File.MoveAsync(FileControl.ThisPage.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync(), await File.GetThumbnailBitmapAsync(), await File.GetModifiedTimeAsync()));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync(), await File.GetThumbnailBitmapAsync(), await File.GetModifiedTimeAsync()));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeDescriptionAsync(), await File.GetThumbnailBitmapAsync(), await File.GetModifiedTimeAsync()));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist())
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFolder NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder);

                            await Folder.DeleteAllSubFilesAndFolders();
                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            if (FileCollection.Where((It) => It.ContentType == ContentType.Folder).All((Item) => Item.Folder.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
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

                            if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                            {
                                if (FileControl.ThisPage.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as StorageFolder).Name == NewFolder.Name) is TreeViewNode ExistNode)
                                {
                                    ExistNode.HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0;
                                }
                                else
                                {
                                    FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                                    {
                                        Content = NewFolder,
                                        HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0
                                    });
                                }
                            }
                            FileControl.ThisPage.CurrentNode.IsExpanded = true;
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
                            Content = "部分文件不存在，无法移动到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files do not exist and cannot be moved to the specified location",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        _ = await QueueContenDialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files cannot be moved due to insufficient free space on the device",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }
            else if (CopyFiles != null)
            {
                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在复制" : "Copying");

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;

                foreach (IStorageItem Item in CopyFiles)
                {
                    try
                    {
                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist())
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFile NewFile = await File.CopyAsync(FileControl.ThisPage.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist())
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            StorageFolder NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder);

                            if (FileCollection.Where((It) => It.ContentType == ContentType.Folder).All((Item) => Item.Folder.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
                            }

                            if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                            {
                                if (FileControl.ThisPage.CurrentNode.Children.FirstOrDefault((Node) => (Node.Content as StorageFolder).Name == NewFolder.Name) is TreeViewNode ExistNode)
                                {
                                    ExistNode.HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0;
                                }
                                else
                                {
                                    FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                                    {
                                        Content = NewFolder,
                                        HasUnrealizedChildren = (await NewFolder.GetItemsAsync(0, 1)).Count > 0
                                    });
                                }
                            }
                            FileControl.ThisPage.CurrentNode.IsExpanded = true;
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
                            Content = "部分文件不存在，无法移动到指定位置",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files do not exist and cannot be moved to the specified location",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        _ = await QueueContenDialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Some files cannot be moved due to insufficient free space on the device",
                            CloseButtonText = "Confirm"
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }

            CutFiles = null;
            CopyFiles = null;
            Paste.IsEnabled = false;

            await LoadingActivation(false);
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CopyFiles != null)
            {
                CopyFiles = null;
            }

            LastNode = FileControl.ThisPage.CurrentNode;

            List<IGrouping<ContentType, FileSystemStorageItem>> GroupItem = SelectedItems.GroupBy((Item) => Item.ContentType).ToList();
            CutFiles = GroupItem.Where((Item) => Item.Key == ContentType.File).Select((It) => (IStorageItem)It.FirstOrDefault().File).Concat(GroupItem.Where((Item) => Item.Key == ContentType.Folder).Select((It) => (IStorageItem)It.FirstOrDefault().Folder)).ToArray();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            bool IsItemNotFound = false;
            bool IsUnauthorized = false;

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

                if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在删除" : "Deleting");

                    if (ItemToDelete.ContentType == ContentType.File)
                    {
                        if (!await ItemToDelete.File.CheckExist())
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
                    }
                    else
                    {
                        if (!await ItemToDelete.Folder.CheckExist())
                        {
                            IsItemNotFound = true;
                        }

                        try
                        {
                            await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                            await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            FileCollection.Remove(ItemToDelete);

                            if (FileControl.ThisPage.CurrentNode.IsExpanded)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).Name == ItemToDelete.Name).FirstOrDefault());
                            }
                            else
                            {
                                if ((await FileControl.ThisPage.CurrentFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync()) == 0)
                                {
                                    FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = false;
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            IsUnauthorized = true;
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

                if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在删除" : "Deleting");

                    foreach (FileSystemStorageItem ItemToDelete in SelectedItems)
                    {
                        if (ItemToDelete.ContentType == ContentType.File)
                        {
                            if (!await ItemToDelete.File.CheckExist())
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
                        }
                        else
                        {
                            if (!await ItemToDelete.Folder.CheckExist())
                            {
                                IsItemNotFound = true;
                            }

                            try
                            {
                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                FileCollection.Remove(ItemToDelete);

                                if (FileControl.ThisPage.CurrentNode.IsExpanded)
                                {
                                    FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).Name == ItemToDelete.Name).FirstOrDefault());
                                }
                                else
                                {
                                    if ((await FileControl.ThisPage.CurrentFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync()) == 0)
                                    {
                                        FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = false;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                IsUnauthorized = true;
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
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to delete some files/folders, the file/folders may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
            }
            else if (IsUnauthorized)
            {
                QueueContentDialog dialog;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权删除此处的文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to delete, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                }

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                }
            }

            await LoadingActivation(false);
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        /// <param name="DisableProbarIndeterminate">是否使用条状进度条替代圆形进度条</param>
        private async Task LoadingActivation(bool IsLoading, string Info = null, bool DisableProbarIndeterminate = false)
        {
            if (IsLoading)
            {
                if (HasFile.Visibility == Visibility.Visible)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                }

                if (DisableProbarIndeterminate)
                {
                    ProBar.IsIndeterminate = false;
                    ProgressInfo.Text = Info + "...0%";
                }
                else
                {
                    ProBar.IsIndeterminate = true;
                    ProgressInfo.Text = Info + "...";
                }
            }
            else
            {
                await Task.Delay(1000);
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

                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "This operation allows only one object to be renamed at a time",
                        CloseButtonText = "Got it"
                    };

                    _ = await Dialog.ShowAsync();
                }

                return;
            }

            if (SelectedItem is FileSystemStorageItem RenameItem)
            {
                if (RenameItem.ContentType == ContentType.File)
                {
                    if (!await RenameItem.File.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding file, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.File.DisplayName, RenameItem.File.FileType);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
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
                                await content.ShowAsync();
                                return;
                            }

                            try
                            {
                                await RenameItem.File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                                foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                                     where Item.Name == dialog.DesireName
                                                     select Item)
                                {
                                    await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(RenameItem.File.Path));
                                }
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
                                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                                await content.ShowAsync();
                                return;
                            }

                            try
                            {
                                await RenameItem.File.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                                foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                                     where Item.Name == dialog.DesireName
                                                     select Item)
                                {
                                    await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(RenameItem.File.Path));
                                }
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
                                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!await RenameItem.Folder.CheckExist())
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                                CloseButtonText = "刷新"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                CloseButtonText = "Refresh"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.Folder.DisplayName);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
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
                                await content.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Folder name cannot be empty, rename failed",
                                    CloseButtonText = "Confirm"
                                };
                                await content.ShowAsync();
                            }
                            return;
                        }

                        StorageFolder ReCreateFolder = null;
                        if (FileControl.ThisPage.CurrentNode.Children.Count != 0)
                        {
                            var ChildCollection = FileControl.ThisPage.CurrentNode.Children;
                            var TargetNode = FileControl.ThisPage.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).FolderRelativeId == RenameItem.Folder.FolderRelativeId).FirstOrDefault();
                            int index = FileControl.ThisPage.CurrentNode.Children.IndexOf(TargetNode);

                            try
                            {
                                await RenameItem.Folder.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
                                ReCreateFolder = await StorageFolder.GetFolderFromPathAsync(RenameItem.Folder.Path);

                                if (TargetNode.HasUnrealizedChildren)
                                {
                                    ChildCollection.Insert(index, new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = true,
                                        IsExpanded = false
                                    });
                                    ChildCollection.Remove(TargetNode);
                                }
                                else if (TargetNode.HasChildren)
                                {
                                    var NewNode = new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = true
                                    };

                                    foreach (var SubNode in TargetNode.Children)
                                    {
                                        NewNode.Children.Add(SubNode);
                                    }

                                    ChildCollection.Insert(index, NewNode);
                                    ChildCollection.Remove(TargetNode);
                                    await NewNode.UpdateAllSubNodeFolder();
                                }
                                else
                                {
                                    ChildCollection.Insert(index, new TreeViewNode()
                                    {
                                        Content = ReCreateFolder,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = false
                                    });
                                    ChildCollection.Remove(TargetNode);
                                }
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
                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                    }
                                }
                            }
                        }

                        await (SelectedItem as FileSystemStorageItem).UpdateRequested(ReCreateFolder);
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem ShareFile = SelectedItem as FileSystemStorageItem;

            if (!await ShareFile.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

            if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
            {
                BluetoothUI Bluetooth = new BluetoothUI();
                if ((await Bluetooth.ShowAsync()) == ContentDialogResult.Primary)
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                    {
                        FileToSend = ShareFile.File,
                        FileName = ShareFile.File.Name,
                        UseStorageFileRatherThanStream = true
                    };
                    await FileTransfer.ShowAsync();
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
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "Please turn on Bluetooth and try again.",
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
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
                        }
                    }
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SelectedIndex = -1;
            FileControl.ThisPage.IsSearchOrPathBoxFocused = false;
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

            if (!await Device.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.File);
            await Dialog.ShowAsync();
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            FileSystemStorageItem Item = SelectedItem as FileSystemStorageItem;

            if (!await Item.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            if (Item.Type == ".zip")
            {
                if ((await UnZipAsync(Item)) is StorageFolder NewFolder)
                {
                    TreeViewNode CurrentNode = null;
                    if (FileControl.ThisPage.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
                    {
                        FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
                        if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                        {
                            CurrentNode = new TreeViewNode
                            {
                                Content = NewFolder,
                                HasUnrealizedChildren = false
                            };
                            FileControl.ThisPage.CurrentNode.Children.Add(CurrentNode);
                        }
                        FileControl.ThisPage.CurrentNode.IsExpanded = true;
                    }
                }
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                        ? "正在压缩"
                        : "Compressing", true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password);
                    }
                    else
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level);
                    }
                }
                else
                {
                    return;
                }
            }

            await LoadingActivation(false);
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task<StorageFolder> UnZipAsync(FileSystemStorageItem ZFile)
        {
            StorageFolder NewFolder = null;
            using (var ZipFileStream = await ZFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);

                try
                {
                    if (zipFile[0].IsCrypted)
                    {
                        ZipDialog dialog = new ZipDialog(false);
                        if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                                ? "正在解压"
                                : "Extracting", true);
                            zipFile.Password = dialog.Password;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese
                            ? "正在解压"
                            : "Extracting", true);
                    }

                    NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(Path.GetFileNameWithoutExtension(ZFile.File.Name), CreationCollisionOption.OpenIfExists);

                    int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                    foreach (ZipEntry Entry in zipFile)
                    {
                        if (!Entry.IsFile)
                        {
                            continue;
                        }
                        using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                        {
                            StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                            using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                            {
                                double FileSize = Entry.Size;
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(ZipTempStream, stream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                TCounter = Convert.ToInt32((e.Processed / FileSize) * 100);
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }

                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / ((double)zipFile.Count));
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });

                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                });
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An exception occurred while extracting the file\r\rError Message：\r\r" + e.Message,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
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
        private async Task CreateZipAsync(FileSystemStorageItem ZipFile, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                var Newfile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);
                using (var NewFileStream = await Newfile.OpenStreamForWriteAsync())
                {
                    ZipOutputStream ZipStream = new ZipOutputStream(NewFileStream);
                    try
                    {
                        ZipStream.SetLevel(ZipLevel);
                        ZipStream.UseZip64 = UseZip64.Off;
                        if (EnableCryption)
                        {
                            ZipStream.Password = Password;
                            ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                            {
                                DateTime = DateTime.Now,
                                AESKeySize = (int)Size,
                                IsCrypted = true,
                                CompressionMethod = CompressionMethod.Deflated
                            };

                            ZipStream.PutNextEntry(NewEntry);
                            using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                            {
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(300), null, string.Empty);
                                });

                                ZipStream.CloseEntry();
                            }
                        }
                        else
                        {
                            ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                            {
                                DateTime = DateTime.Now
                            };

                            ZipStream.PutNextEntry(NewEntry);
                            using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                            {
                                await Task.Run(() =>
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);

                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(300), null, string.Empty);
                                });
                                ZipStream.CloseEntry();
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
                                Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "An exception occurred while compressing the file\r\rError Message：\r\r" + e.Message,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    finally
                    {
                        ZipStream.IsStreamOwner = false;
                        ZipStream.Close();
                    }
                }
                FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(Newfile, await Newfile.GetSizeDescriptionAsync(), await Newfile.GetThumbnailBitmapAsync(), await Newfile.GetModifiedTimeAsync()));
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
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                    }
                }
            }
        }

        private void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Source)
            {
                if (!await Source.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
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
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "There is already an ongoing task, please wait for it to complete",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
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

                            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    StorageFile DestinationFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.GenerateUniqueName);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source.File, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp);

                                    if (Path.GetDirectoryName(DestinationFile.Path) == FileControl.ThisPage.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string Status && Status == "Success")
                                    {
                                        FileCollection.Add(new FileSystemStorageItem(DestinationFile, await DestinationFile.GetSizeDescriptionAsync(), await DestinationFile.GetThumbnailBitmapAsync(), await DestinationFile.GetModifiedTimeAsync()));
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
                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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

                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                await LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在转码" : "Transcoding");
                                await GeneralTransformer.TranscodeFromImageAsync(Source.File, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);
                                await LoadingActivation(false);
                                FileCollection.Add(new FileSystemStorageItem(Dialog.TargetFile, await Dialog.TargetFile.GetSizeDescriptionAsync(), await Dialog.TargetFile.GetThumbnailBitmapAsync(), await Dialog.TargetFile.GetModifiedTimeAsync()));
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
            if (!await Device.Folder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            AttributeDialog Dialog = new AttributeDialog(Device.Folder);
            _ = await Dialog.ShowAsync();
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (QRTeachTip.IsOpen)
            {
                QRTeachTip.IsOpen = false;
            }

            FileSystemStorageItem Item = SelectedItem as FileSystemStorageItem;

            if (!await Item.File.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件，该文件可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding file, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            WiFiProvider = new WiFiShareProvider();
            WiFiProvider.ThreadExitedUnexpectly -= WiFiProvider_ThreadExitedUnexpectly;
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

            WiFiProvider.StartToListenRequest();
        }

        public string ComputeMD5Hash(string Data)
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
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "WIFI transmission has an unexpected error：\r" + e.Message,
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
                }
            });
        }

        private void QRTeachTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
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

            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
        }

        private async void ParentAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            if (FileControl.ThisPage.CurrentNode == FileControl.ThisPage.FolderTree.RootNodes.FirstOrDefault())
            {
                if (ThisPC.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == FileControl.ThisPage.CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    AttributeDialog Dialog = new AttributeDialog(FileControl.ThisPage.CurrentFolder);
                    _ = await Dialog.ShowAsync();
                }
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog(FileControl.ThisPage.CurrentFolder);
                _ = await Dialog.ShowAsync();
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

            if (!await folder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog1 = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog1.ShowAsync();
                }
                await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                return;
            }

            if (ThisPC.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "This folder has been added to the home page, can not be added repeatedly",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync();
                ThisPC.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserCustom));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path);
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            try
            {
                var NewFolder = Globalization.Language == LanguageEnum.Chinese
                    ? await FileControl.ThisPage.CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await FileControl.ThisPage.CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

                var Size = await NewFolder.GetSizeDescriptionAsync();
                var Thumbnail = await NewFolder.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await NewFolder.GetModifiedTimeAsync();

                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, Size, Thumbnail, ModifiedTime));

                if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                {
                    FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                    {
                        Content = NewFolder,
                        HasUnrealizedChildren = false
                    });
                }
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
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

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
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
                if (!await ShareItem.File.CheckExist())
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到对应的文件，该文件可能已被移动或删除",
                            CloseButtonText = "刷新"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Could not find the corresponding file, it may have been moved or deleted",
                            CloseButtonText = "Refresh"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
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

            if (!await FileControl.ThisPage.CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
        }

        private void GridViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            FileControl.ThisPage.IsSearchOrPathBoxFocused = false;

            if (!SettingPage.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void EnterSelectedItem(FileSystemStorageItem ReFile)
        {
            try
            {
                if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
                {
                    if (TabTarget.ContentType == ContentType.File)
                    {
                        if (!await TabTarget.File.CheckExist())
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到对应的文件，该文件可能已被移动或删除",
                                    CloseButtonText = "刷新"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Could not find the corresponding file, it may have been moved or deleted",
                                    CloseButtonText = "Refresh"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        switch (TabTarget.File.FileType)
                        {
                            case ".zip":
                                {
                                    Nav.Navigate(typeof(ZipExplorer), TabTarget, new DrillInNavigationTransitionInfo());
                                    break;
                                }
                            case ".jpg":
                            case ".png":
                            case ".bmp":
                            case ".heic":
                            case ".gif":
                            case ".tiff":
                                {
                                    Nav.Navigate(typeof(PhotoViewer), TabTarget.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
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
                                    Nav.Navigate(typeof(MediaPlayer), TabTarget.File, new DrillInNavigationTransitionInfo());
                                    break;
                                }
                            case ".txt":
                                {
                                    Nav.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                    break;
                                }
                            case ".pdf":
                                {
                                    Nav.Navigate(typeof(PdfReader), TabTarget.File, new DrillInNavigationTransitionInfo());
                                    break;
                                }
                            case ".exe":
                                {
                                    ApplicationData.Current.LocalSettings.Values["ExcutePath"] = TabTarget.Path;
                                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                                    break;
                                }
                            default:
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
                                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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
                                        if (!await Launcher.LaunchFileAsync(TabTarget.File))
                                        {
                                            LauncherOptions options = new LauncherOptions
                                            {
                                                DisplayApplicationPicker = true
                                            };
                                            _ = await Launcher.LaunchFileAsync(TabTarget.File, options);
                                        }
                                    }
                                    break;
                                }
                        }
                    }
                    else
                    {
                        if (!await ReFile.Folder.CheckExist())
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到对应的文件夹，该文件可能已被移动或删除",
                                    CloseButtonText = "刷新"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Could not find the corresponding folder, it may have been moved or deleted",
                                    CloseButtonText = "Refresh"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            await FileControl.ThisPage.DisplayItemsInFolder(FileControl.ThisPage.CurrentNode, true);
                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        if (FileControl.ThisPage.CurrentNode.HasUnrealizedChildren && !FileControl.ThisPage.CurrentNode.IsExpanded)
                        {
                            FileControl.ThisPage.CurrentNode.IsExpanded = true;
                        }

                        while (true)
                        {
                            TreeViewNode TargetNode = FileControl.ThisPage.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).Name == TabTarget.Name).FirstOrDefault();
                            if (TargetNode != null)
                            {
                                await FileControl.ThisPage.FolderTree.SelectNode(TargetNode);
                                await FileControl.ThisPage.DisplayItemsInFolder(TargetNode);
                                break;
                            }
                            else if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name != "FileControl")
                            {
                                break;
                            }
                            else
                            {
                                await Task.Delay(200);
                            }
                        }
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
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "There is already an ongoing task, please wait for it to complete",
                        CloseButtonText = "Got it"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            if (SelectedItem is FileSystemStorageItem Item)
            {
                VideoEditDialog Dialog = new VideoEditDialog(Item.File);
                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {(Globalization.Language == LanguageEnum.Chinese ? "裁剪" : "Cropped")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControl.ThisPage.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeDescriptionAsync(), await ExportFile.GetThumbnailBitmapAsync(), await ExportFile.GetModifiedTimeAsync()));
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
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "There is already an ongoing task, please wait for it to complete",
                        CloseButtonText = "Got it"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            if (SelectedItem is FileSystemStorageItem Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item.File);
                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {(Globalization.Language == LanguageEnum.Chinese ? "合并" : "Merged")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControl.ThisPage.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeDescriptionAsync(), await ExportFile.GetThumbnailBitmapAsync(), await ExportFile.GetModifiedTimeAsync()));
                    }
                }
            }
        }
    }
}

