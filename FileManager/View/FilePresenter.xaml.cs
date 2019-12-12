using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Toolkit.Uwp.Notifications;
using OpenCV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace FileManager
{
    public sealed partial class FilePresenter : Page
    {
        public IncrementalLoadingCollection<FileSystemStorageItem> FileCollection;
        public static FilePresenter ThisPage { get; private set; }
        public List<GridViewItem> ZipCollection = new List<GridViewItem>();
        public TreeViewNode DisplayNode;
        private static StorageFile CopyFile;
        private static StorageFile CutFile;
        AutoResetEvent AESControl;
        Frame Nav;
        WiFiShareProvider WiFiProvider;
        const int AESCacheSize = 1048576;
        byte[] EncryptByteBuffer;
        byte[] DecryptByteBuffer;
        FileSystemStorageItem DoubleTabTarget = null;

        CancellationTokenSource TranscodeCancellation;
        bool IsTranscoding = false;

        public FilePresenter()
        {
            InitializeComponent();
            ThisPage = this;

            FileCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            GridViewControl.ItemsSource = FileCollection;
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
                    case VirtualKey.Right when GridViewControl.SelectedIndex == -1:
                        {
                            GridViewControl.Focus(FocusState.Programmatic);
                            GridViewControl.SelectedIndex = 0;
                            break;
                        }
                    case VirtualKey.Enter when !QueueContentDialog.IsRunningOrWaiting && GridViewControl.SelectedItem is FileSystemStorageItem Item:
                        {
                            GridViewControl.Focus(FocusState.Programmatic);
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
                            if (CutFile != null)
                            {
                                CutFile = null;
                            }

                            CopyFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;
                            break;
                        }
                    case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            if (CopyFile != null)
                            {
                                CopyFile = null;
                            }

                            CutFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;
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
            AESControl = new AutoResetEvent(false);
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            AESControl?.Dispose();
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
        }

        /// <summary>
        /// 关闭右键菜单并将GridView从多选模式恢复到单选模式
        /// </summary>
        private void Restore()
        {
            CommandsFlyout.Hide();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (CutFile != null)
            {
                CutFile = null;
            }

            CopyFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;

            Restore();
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();
            if (CutFile != null)
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    LoadingActivation(true, "正在剪切");

                    try
                    {
                        await CutFile.MoveAsync(FileControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因源文件已删除，无法剪切到指定位置",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，文件无法剪切",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
                else
                {
                    LoadingActivation(true, "Cutting");

                    try
                    {
                        await CutFile.MoveAsync(FileControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                        if (FileCollection.Count > 0)
                        {
                            int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.ContentType == ContentType.File));
                            if (Index == -1)
                            {
                                FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                            else
                            {
                                FileCollection.Insert(Index, new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                            }
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to cut to the specified location because the source file has been deleted",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "The device has insufficient free space and the file cannot be cut.",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }

                await Task.Delay(500);
                LoadingActivation(false);
            }
            else if (CopyFile != null)
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    LoadingActivation(true, "正在复制");

                    try
                    {
                        StorageFile NewFile = await CopyFile.CopyAsync(FileControl.ThisPage.CurrentFolder, CopyFile.Name, NameCollisionOption.GenerateUniqueName);
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

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因源文件已删除，无法复制到指定位置",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因设备剩余空间大小不足，文件无法复制",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
                else
                {
                    LoadingActivation(true, "Copying");

                    try
                    {
                        StorageFile NewFile = await CopyFile.CopyAsync(FileControl.ThisPage.CurrentFolder, CopyFile.Name, NameCollisionOption.GenerateUniqueName);
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

                        CutFile = null;
                        CopyFile = null;
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to copy to the specified location because the source file has been deleted",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "The device has insufficient free space and the file cannot be copy",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }

                await Task.Delay(500);
                LoadingActivation(false);
            }

            Paste.IsEnabled = false;
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (CopyFile != null)
            {
                CopyFile = null;
            }

            CutFile = (GridViewControl.SelectedItem as FileSystemStorageItem)?.File;

            Restore();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (GridViewControl.SelectedItem is FileSystemStorageItem ItemToDelete)
            {
                Restore();

                if (ItemToDelete.ContentType == ContentType.File)
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "警告",
                            PrimaryButtonText = "是",
                            Content = "此操作将永久删除 \" " + ItemToDelete.Name + " \"\r\r是否继续?",
                            CloseButtonText = "否",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            LoadingActivation(true, "正在删除");

                            try
                            {
                                await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                for (int i = 0; i < FileCollection.Count; i++)
                                {
                                    if (FileCollection[i].RelativeId == ItemToDelete.File.FolderRelativeId)
                                    {
                                        FileCollection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权删除此处的文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            PrimaryButtonText = "Continue",
                            Content = "This action will permanently delete \" " + ItemToDelete.Name + " \"\r\rWhether to continue?",
                            CloseButtonText = "Cancel",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            LoadingActivation(true, "Deleting");

                            try
                            {
                                await ItemToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                for (int i = 0; i < FileCollection.Count; i++)
                                {
                                    if (FileCollection[i].RelativeId == ItemToDelete.File.FolderRelativeId)
                                    {
                                        FileCollection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to delete, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "警告",
                            PrimaryButtonText = "是",
                            CloseButtonText = "否",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
                            Content = "此操作将永久删除 \"" + ItemToDelete.DisplayName + " \"\r\r是否继续?"
                        };

                        if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            try
                            {
                                LoadingActivation(true, "正在删除");

                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权删除此文件夹，可能是您无权访问此文件夹\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                                return;
                            }

                            FileCollection.Remove(ItemToDelete);
                            if (FileControl.ThisPage.CurrentNode.IsExpanded)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == ItemToDelete.RelativeId).FirstOrDefault());
                            }
                            else
                            {
                                if ((await FileControl.ThisPage.CurrentFolder.GetFoldersAsync()).Count == 0)
                                {
                                    FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            PrimaryButtonText = "Continue",
                            CloseButtonText = "Cancel",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
                            Content = "This action will permanently delete \" " + ItemToDelete.DisplayName + " \"\r\rWhether to continue ?"
                        };

                        if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                        {
                            LoadingActivation(true, "Deleting");

                            try
                            {
                                await ItemToDelete.Folder.DeleteAllSubFilesAndFolders();
                                await ItemToDelete.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "RX does not have permission to delete, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                                return;
                            }

                            FileCollection.Remove(ItemToDelete);
                            if (FileControl.ThisPage.CurrentNode.IsExpanded)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == ItemToDelete.RelativeId).FirstOrDefault());
                            }
                        }
                    }
                }

                await Task.Delay(500);
                LoadingActivation(false);
            }
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        /// <param name="EnableProgressDisplay">是否使用条状进度条替代圆形进度条</param>
        private void LoadingActivation(bool IsLoading, string Info = null, bool EnableProgressDisplay = false)
        {
            if (IsLoading)
            {
                if (HasFile.Visibility == Visibility.Visible)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                }

                if (EnableProgressDisplay)
                {
                    ProRing.Visibility = Visibility.Collapsed;
                    ProBar.Visibility = Visibility.Visible;
                    ProgressInfo.Text = Info + "...0%";
                }
                else
                {
                    ProRing.Visibility = Visibility.Visible;
                    ProBar.Visibility = Visibility.Collapsed;
                    ProgressInfo.Text = Info + "...";
                }
            }

            LoadingControl.IsLoading = IsLoading;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (GridViewControl.SelectedItem is FileSystemStorageItem RenameItem)
            {
                if (RenameItem.ContentType == ContentType.File)
                {
                    RenameDialog dialog = new RenameDialog(RenameItem.File.DisplayName, RenameItem.File.FileType);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                        {
                            if (dialog.DesireName == RenameItem.File.FileType)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "文件名不能为空，重命名失败",
                                    CloseButtonText = "确定",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                    CloseButtonText = "稍后",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                    CloseButtonText = "Confirm",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                    CloseButtonText = "Later",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                    RenameDialog dialog = new RenameDialog(RenameItem.Folder.DisplayName);
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (string.IsNullOrWhiteSpace(dialog.DesireName))
                        {
                            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "文件夹名不能为空，重命名失败",
                                    CloseButtonText = "确定",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                await content.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog content = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Folder name cannot be empty, rename failed",
                                    CloseButtonText = "Confirm",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r是否立即进入系统文件管理器进行相应操作？",
                                        PrimaryButtonText = "立刻",
                                        CloseButtonText = "稍后",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                        CloseButtonText = "Later",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                    };
                                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                    }
                                }
                            }
                        }

                        await (GridViewControl.SelectedItem as FileSystemStorageItem).UpdateRequested(ReCreateFolder);
                    }
                }
            }
        }

        /*
         * AES模块采用了分段文件流读取，内存占用得到有效控制
         * 可处理数据量极大的各种文件的加密和解密
         * 加密完成后生成.sle格式的文件
         * 其中文件名和类型以及AES加密的密钥长度以明文存储在.sle文件的开头
         * 
         * AES本身并不能判断解密时的密码是否正确，无论对错均可解密，因此这里取巧：
         * 将一段字符标志“PASSWORD_CORRECT”与源文件一起加密，由于知道标志具体位置和标志原始明文内容
         * 因此解密的时候，利用用户提供的密码对该标识符的位置进行解密，若解密出来的明文与PASSWORD_CORRECT相符
         * 则证明该密码正确，否则密码错误。此方法可确保既不需要存储用户原始密码亦可判断密码正误
         */
        private async void AES_Click(object sender, RoutedEventArgs e)
        {
            var SelectedFile = GridViewControl.SelectedItem as FileSystemStorageItem;
            Restore();

            int KeySizeRequest;
            string KeyRequest;
            bool IsDeleteRequest;
            if (SelectedFile.Type != ".sle")
            {
                AESDialog Dialog = new AESDialog(true, SelectedFile.Name);
                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    KeyRequest = Dialog.Key;
                    IsDeleteRequest = Dialog.IsDeleteChecked;
                    KeySizeRequest = Dialog.KeySize;
                }
                else
                {
                    LoadingActivation(false);
                    DecryptByteBuffer = null;
                    EncryptByteBuffer = null;
                    return;
                }

                LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? "正在加密"
                    : "Encrypting");

                try
                {
                    StorageFile file = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + ".sle", CreationCollisionOption.GenerateUniqueName);

                    await Task.Run(async () =>
                    {
                        using (var FileStream = await SelectedFile.File.OpenStreamForReadAsync())
                        {
                            using (var TargetFileStream = await file.OpenStreamForWriteAsync())
                            {
                                byte[] Tail = Encoding.UTF8.GetBytes("$" + KeySizeRequest + "|" + SelectedFile.Type + "$");
                                byte[] PasswordFlag = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");

                                if (FileStream.Length < AESCacheSize)
                                {
                                    EncryptByteBuffer = new byte[FileStream.Length];
                                    FileStream.Read(EncryptByteBuffer, 0, EncryptByteBuffer.Length);
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.ECBEncrypt(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);
                                    var EncryptedBytes = AESProvider.ECBEncrypt(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                    await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                }
                                else
                                {
                                    EncryptByteBuffer = new byte[Tail.Length];
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.ECBEncrypt(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);

                                    long BytesWrite = 0;
                                    EncryptByteBuffer = new byte[AESCacheSize];
                                    while (BytesWrite < FileStream.Length)
                                    {
                                        if (FileStream.Length - BytesWrite < AESCacheSize)
                                        {
                                            if (FileStream.Length - BytesWrite == 0)
                                            {
                                                break;
                                            }
                                            EncryptByteBuffer = new byte[FileStream.Length - BytesWrite];
                                        }

                                        BytesWrite += FileStream.Read(EncryptByteBuffer, 0, EncryptByteBuffer.Length);
                                        var EncryptedBytes = AESProvider.ECBEncrypt(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                        await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                    }

                                }
                            }
                        }
                    });
                    FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(file, await file.GetSizeDescriptionAsync(), await file.GetThumbnailBitmapAsync(), await file.GetModifiedTimeAsync()));
                }
                catch (UnauthorizedAccessException)
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权在此处创建加密文件，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                            Content = "RX does not have permission to create an encrypted file here, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                        }
                    }
                }
            }
            else
            {
                AESDialog Dialog = new AESDialog(false, SelectedFile.Name);
                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    KeyRequest = Dialog.Key;
                    IsDeleteRequest = Dialog.IsDeleteChecked;
                }
                else
                {
                    LoadingActivation(false);
                    DecryptByteBuffer = null;
                    EncryptByteBuffer = null;
                    return;
                }

                LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? "正在解密"
                    : "Decrypting");

                await Task.Run(async () =>
                {
                    using (var FileStream = await SelectedFile.File.OpenStreamForReadAsync())
                    {
                        string FileType;
                        byte[] DecryptedBytes;
                        int SignalLength = 0;
                        int EncryptKeySize = 0;

                        DecryptByteBuffer = new byte[20];
                        FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                        try
                        {
                            if (Encoding.UTF8.GetString(DecryptByteBuffer, 0, 1) != "$")
                            {
                                throw new Exception("文件格式错误");
                            }
                        }
                        catch (Exception)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                QueueContentDialog dialog;
                                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                                {
                                    dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  文件格式检验错误，文件可能已损坏",
                                        CloseButtonText = "确定",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                    };
                                }
                                else
                                {
                                    dialog = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "  File format validation error, file may be corrupt",
                                        CloseButtonText = "Confirm",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                    };
                                }

                                LoadingActivation(false);
                                DecryptByteBuffer = null;
                                EncryptByteBuffer = null;
                                await dialog.ShowAsync();
                            });

                            return;
                        }
                        StringBuilder builder = new StringBuilder();
                        for (int i = 1; ; i++)
                        {
                            string Char = Encoding.UTF8.GetString(DecryptByteBuffer, i, 1);
                            if (Char == "|")
                            {
                                EncryptKeySize = int.Parse(builder.ToString());
                                KeyRequest = KeyRequest.PadRight(EncryptKeySize / 8, '0');
                                builder.Clear();
                                continue;
                            }
                            if (Char != "$")
                            {
                                builder.Append(Char);
                            }
                            else
                            {
                                SignalLength = i + 1;
                                break;
                            }
                        }
                        FileType = builder.ToString();
                        FileStream.Seek(SignalLength, SeekOrigin.Begin);

                        byte[] PasswordConfirm = new byte[16];
                        await FileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length);
                        if (Encoding.UTF8.GetString(AESProvider.ECBDecrypt(PasswordConfirm, KeyRequest, EncryptKeySize)) != "PASSWORD_CORRECT")
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                QueueContentDialog dialog;
                                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                                {
                                    dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  密码错误，无法解密\r\r  请重试...",
                                        CloseButtonText = "确定",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                    };
                                }
                                else
                                {
                                    dialog = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "  The password is incorrect and cannot be decrypted\r\r  Please try again...",
                                        CloseButtonText = "Confirm",
                                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                    };
                                }

                                LoadingActivation(false);
                                DecryptByteBuffer = null;
                                EncryptByteBuffer = null;
                                await dialog.ShowAsync();
                                AESControl.Set();
                            });
                            AESControl.WaitOne();
                            return;
                        }
                        else
                        {
                            SignalLength += 16;
                        }

                        if (FileStream.Length - SignalLength < AESCacheSize)
                        {
                            DecryptByteBuffer = new byte[FileStream.Length - SignalLength];
                        }
                        else
                        {
                            DecryptByteBuffer = new byte[AESCacheSize];
                        }
                        FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                        DecryptedBytes = AESProvider.ECBDecrypt(DecryptByteBuffer, KeyRequest, EncryptKeySize);

                        StorageFolder CurrentFolder = null;
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            CurrentFolder = FileControl.ThisPage.CurrentFolder;
                        });

                        try
                        {
                            StorageFile file = await CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + FileType, CreationCollisionOption.GenerateUniqueName);

                            using (var TargetFileStream = await file.OpenStreamForWriteAsync())
                            {
                                await TargetFileStream.WriteAsync(DecryptedBytes, 0, DecryptedBytes.Length);

                                if (FileStream.Length - SignalLength >= AESCacheSize)
                                {
                                    long BytesRead = DecryptByteBuffer.Length + SignalLength;
                                    while (BytesRead < FileStream.Length)
                                    {
                                        if (FileStream.Length - BytesRead < AESCacheSize)
                                        {
                                            if (FileStream.Length - BytesRead == 0)
                                            {
                                                break;
                                            }
                                            DecryptByteBuffer = new byte[FileStream.Length - BytesRead];
                                        }
                                        BytesRead += FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                                        DecryptedBytes = AESProvider.ECBDecrypt(DecryptByteBuffer, KeyRequest, EncryptKeySize);
                                        await TargetFileStream.WriteAsync(DecryptedBytes, 0, DecryptedBytes.Length);
                                    }
                                }
                            }

                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                            {
                                FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(file, await file.GetSizeDescriptionAsync(), await file.GetThumbnailBitmapAsync(), await file.GetModifiedTimeAsync()));
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "RX无权在此处创建解密文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                    PrimaryButtonText = "立刻",
                                    CloseButtonText = "稍后",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                    Content = "RX does not have permission to create an decrypted file here, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                    PrimaryButtonText = "Enter",
                                    CloseButtonText = "Later",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                }
                            }
                        }
                    }
                });
            }

            if (IsDeleteRequest)
            {
                await SelectedFile.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                for (int i = 0; i < FileCollection.Count; i++)
                {
                    if (FileCollection[i].RelativeId == SelectedFile.RelativeId)
                    {
                        FileCollection.RemoveAt(i);
                        break;
                    }
                }
            }

            DecryptByteBuffer = null;
            EncryptByteBuffer = null;

            await Task.Delay(500);
            LoadingActivation(false);
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            var RadioDevice = await Radio.GetRadiosAsync();

            foreach (var Device in from Device in RadioDevice
                                   where Device.Kind == RadioKind.Bluetooth
                                   select Device)
            {
                if (Device.State != RadioState.On)
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "请开启蓝牙开关后再试",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "Please turn on Bluetooth and try again.",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await dialog.ShowAsync();
                    }
                    return;
                }
            }

            Restore();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                FileSystemStorageItem file = GridViewControl.SelectedItem as FileSystemStorageItem;

                BluetoothUI Bluetooth = new BluetoothUI();
                var result = await Bluetooth.ShowAsync();
                if (result == ContentDialogResult.Secondary)
                {
                    return;
                }
                else if (result == ContentDialogResult.Primary)
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                    {
                        FileToSend = file.File,
                        FileName = file.File.Name,
                        UseStorageFileRatherThanStream = true
                    };
                    await FileTransfer.ShowAsync();
                }
            });
        }

        private void GridViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (GridViewControl.SelectedItem is FileSystemStorageItem Item)
                {
                    if (Item.ContentType == ContentType.File)
                    {
                        Transcode.IsEnabled = false;

                        Zip.Label = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                    ? "Zip压缩"
                                    : "Zip Compression";
                        switch (Item.Type)
                        {
                            case ".zip":
                                {
                                    Zip.Label = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                                ? "Zip解压"
                                                : "Zip Decompression";
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
                                    Transcode.IsEnabled = true;
                                    break;
                                }
                        }

                        AES.Label = Item.Type == ".sle"
                                    ? (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "AES解密" : "AES Decryption")
                                    : (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "AES加密" : "AES Encryption");
                    }
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            GridViewControl.SelectedIndex = -1;
        }

        private void GridViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
            {
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);

                if (Context.ContentType == ContentType.Folder)
                {
                    GridViewControl.ContextFlyout = FolderFlyout;
                }
                else
                {
                    GridViewControl.ContextFlyout = CommandsFlyout;
                }
            }
            else
            {
                GridViewControl.ContextFlyout = EmptyFlyout;
            }

            e.Handled = true;
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem Device = GridViewControl.SelectedItem as FileSystemStorageItem;
            if (Device.File != null)
            {
                AttributeDialog Dialog = new AttributeDialog(Device.File);
                await Dialog.ShowAsync();
            }
            else if (Device.Folder != null)
            {
                AttributeDialog Dialog = new AttributeDialog(Device.Folder);
                await Dialog.ShowAsync();
            }
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem SelectedItem = GridViewControl.SelectedItem as FileSystemStorageItem;
            Restore();

            if (SelectedItem.Type == ".zip")
            {
                await UnZipAsync(SelectedItem);
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, SelectedItem.DisplayName);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                        ? "正在压缩"
                        : "Compressing", true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(SelectedItem, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password);
                    }
                    else
                    {
                        await CreateZipAsync(SelectedItem, dialog.FileName, (int)dialog.Level);
                    }
                }
                else
                {
                    return;
                }
            }

            await Task.Delay(500);
            LoadingActivation(false);
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task UnZipAsync(FileSystemStorageItem ZFile)
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
                            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                ? "正在解压"
                                : "Extracting", true);
                            zipFile.Password = dialog.Password;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                            ? "正在解压"
                            : "Extracting", true);
                    }
                    await Task.Run(async () =>
                    {
                        int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                        foreach (ZipEntry Entry in zipFile)
                        {
                            if (!Entry.IsFile)
                            {
                                continue;
                            }
                            using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                            {
                                StorageFolder CurrentFolder = null;
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                {
                                    CurrentFolder = FileControl.ThisPage.CurrentFolder;
                                });

                                try
                                {
                                    NewFolder = await CurrentFolder.CreateFolderAsync(ZFile.File.DisplayName, CreationCollisionOption.OpenIfExists);
                                    StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                                    using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                                    {
                                        double FileSize = Entry.Size;
                                        StreamUtils.Copy(ZipTempStream, stream, new byte[4096], async (s, e) =>
                                        {
                                            await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
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
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                    {
                                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = "错误",
                                                Content = "RX无权在此处解压Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                                PrimaryButtonText = "立刻",
                                                CloseButtonText = "稍后",
                                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                                CloseButtonText = "Later",
                                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                            };
                                            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                            {
                                                _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                                            }
                                        }
                                    });
                                }
                            }
                        }
                    });
                }
                catch (Exception e)
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "解压文件时发生异常\r\r错误信息：\r\r" + e.Message,
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
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
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    return;
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            TreeViewNode CurrentNode = null;
            if (FileControl.ThisPage.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
            {
                if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                {
                    CurrentNode = new TreeViewNode
                    {
                        Content = await FileControl.ThisPage.CurrentFolder.GetFolderAsync(NewFolder.Name),
                        HasUnrealizedChildren = false
                    };
                    FileControl.ThisPage.CurrentNode.Children.Add(CurrentNode);
                }
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }

            if (CurrentNode == null)
            {
                while (true)
                {
                    if (FileControl.ThisPage.CurrentNode.Children.Where((Item) => (Item.Content as StorageFolder).Name == NewFolder.Name).FirstOrDefault() is TreeViewNode TargetNode)
                    {
                        await SetSelectedNodeInTreeAsync(TargetNode);
                        break;
                    }
                    else
                    {
                        await Task.Delay(200);
                    }
                }
            }
            else
            {
                await SetSelectedNodeInTreeAsync(CurrentNode);
            }
        }

        private async Task SetSelectedNodeInTreeAsync(TreeViewNode Node)
        {
            if (!FileControl.ThisPage.CurrentNode.IsExpanded)
            {
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }

            while (true)
            {
                if (FileControl.ThisPage.FolderTree.ContainerFromNode(Node) is TreeViewItem Item)
                {
                    Item.IsSelected = true;
                    Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
                    await FileControl.ThisPage.DisplayItemsInFolder(Node);
                    break;
                }
                else
                {
                    await Task.Delay(200);
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
                            await Task.Run(async () =>
                            {
                                var NewEntry = new ZipEntry(ZipFile.File.Name)
                                {
                                    DateTime = DateTime.Now,
                                    AESKeySize = (int)Size,
                                    IsCrypted = true,
                                    CompressionMethod = CompressionMethod.Deflated
                                };

                                ZipStream.PutNextEntry(NewEntry);
                                using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            });
                        }
                        else
                        {
                            await Task.Run(async () =>
                            {
                                var NewEntry = new ZipEntry(ZipFile.File.Name)
                                {
                                    DateTime = DateTime.Now
                                };

                                ZipStream.PutNextEntry(NewEntry);
                                using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (SyncRootProvider.SyncRoot)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);

                                                int CurrentProgress = (int)Math.Ceiling(e.PercentComplete);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
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
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
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
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此处创建Zip文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                        CloseButtonText = "Later",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
            if (GridViewControl.SelectedItem is FileSystemStorageItem Source)
            {
                if (IsTranscoding)
                {
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "已存在正在进行的转码任务，请等待其完成",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "There is already an ongoing transcoding task, please wait for it to complete",
                            CloseButtonText = "Got it",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    return;
                }

                TranscodeDialog dialog = new TranscodeDialog(Source.File);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    try
                    {
                        IsTranscoding = true;

                        StorageFile DestinationFile = await FileControl.ThisPage.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.ReplaceExisting);

                        await TranscodeMediaAsync(dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp, Source.File, DestinationFile);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "RX无权在此处创建转码文件，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                PrimaryButtonText = "立刻",
                                CloseButtonText = "稍后",
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                                CloseButtonText = "Later",
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                            };
                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
                            }
                        }
                    }
                    finally
                    {
                        IsTranscoding = false;
                    }
                }
            }
        }

        private async Task TranscodeMediaAsync(string MediaTranscodeEncodingProfile, string MediaTranscodeQuality, bool SpeedUp, StorageFile SourceFile, StorageFile DestinationFile)
        {
            await Task.Factory.StartNew(() =>
            {
                TranscodeCancellation = new CancellationTokenSource();

                MediaTranscoder Transcoder = new MediaTranscoder
                {
                    HardwareAccelerationEnabled = true,
                    VideoProcessingAlgorithm = SpeedUp ? MediaVideoProcessingAlgorithm.Default : MediaVideoProcessingAlgorithm.MrfCrf444
                };

                try
                {
                    MediaEncodingProfile Profile = null;
                    VideoEncodingQuality VideoQuality = default;
                    AudioEncodingQuality AudioQuality = default;

                    switch (MediaTranscodeQuality)
                    {
                        case "UHD2160p":
                            VideoQuality = VideoEncodingQuality.Uhd2160p;
                            break;
                        case "QVGA":
                            VideoQuality = VideoEncodingQuality.Qvga;
                            break;
                        case "HD1080p":
                            VideoQuality = VideoEncodingQuality.HD1080p;
                            break;
                        case "HD720p":
                            VideoQuality = VideoEncodingQuality.HD720p;
                            break;
                        case "WVGA":
                            VideoQuality = VideoEncodingQuality.Wvga;
                            break;
                        case "VGA":
                            VideoQuality = VideoEncodingQuality.Vga;
                            break;
                        case "High":
                            AudioQuality = AudioEncodingQuality.High;
                            break;
                        case "Medium":
                            AudioQuality = AudioEncodingQuality.Medium;
                            break;
                        case "Low":
                            AudioQuality = AudioEncodingQuality.Low;
                            break;
                    }

                    switch (MediaTranscodeEncodingProfile)
                    {
                        case "MKV":
                            Profile = MediaEncodingProfile.CreateHevc(VideoQuality);
                            break;
                        case "MP4":
                            Profile = MediaEncodingProfile.CreateMp4(VideoQuality);
                            break;
                        case "WMV":
                            Profile = MediaEncodingProfile.CreateWmv(VideoQuality);
                            break;
                        case "AVI":
                            Profile = MediaEncodingProfile.CreateAvi(VideoQuality);
                            break;
                        case "MP3":
                            Profile = MediaEncodingProfile.CreateMp3(AudioQuality);
                            break;
                        case "ALAC":
                            Profile = MediaEncodingProfile.CreateAlac(AudioQuality);
                            break;
                        case "WMA":
                            Profile = MediaEncodingProfile.CreateWma(AudioQuality);
                            break;
                        case "M4A":
                            Profile = MediaEncodingProfile.CreateM4a(AudioQuality);
                            break;
                    }

                    PrepareTranscodeResult Result = Transcoder.PrepareFileTranscodeAsync(SourceFile, DestinationFile, Profile).AsTask().Result;
                    if (Result.CanTranscode)
                    {
                        SendUpdatableToastWithProgress(SourceFile, DestinationFile);
                        Progress<double> TranscodeProgress = new Progress<double>(UpdateToastNotification);

                        Result.TranscodeAsync().AsTask(TranscodeCancellation.Token, TranscodeProgress).Wait();

                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Success";
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "NotSupport";
                        DestinationFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                    }
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Cancel";
                    DestinationFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = e.Message;
                    DestinationFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
            }, TaskCreationOptions.LongRunning).ContinueWith((task) =>
            {
                TranscodeCancellation.Dispose();
                TranscodeCancellation = null;

                if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string ExcuteStatus)
                {
                    Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                        {
                            switch (ExcuteStatus)
                            {
                                case "Success":
                                    FileControl.ThisPage.Notification.Show("转码已成功完成", 10000);
                                    ShowCompleteNotification(SourceFile, DestinationFile);
                                    break;
                                case "Cancel":
                                    FileControl.ThisPage.Notification.Show("转码任务被取消", 10000);
                                    ShowUserCancelNotification();
                                    break;
                                case "NotSupport":
                                    FileControl.ThisPage.Notification.Show("转码格式不支持", 10000);
                                    break;
                                default:
                                    FileControl.ThisPage.Notification.Show("转码失败:" + ExcuteStatus, 10000);
                                    break;
                            }
                        }
                        else
                        {
                            switch (ExcuteStatus)
                            {
                                case "Success":
                                    FileControl.ThisPage.Notification.Show("Transcoding has been successfully completed", 10000);
                                    break;
                                case "Cancel":
                                    FileControl.ThisPage.Notification.Show("Transcoding task is cancelled", 10000);
                                    ShowUserCancelNotification();
                                    break;
                                case "NotSupport":
                                    FileControl.ThisPage.Notification.Show("Transcoding format is not supported", 10000);
                                    break;
                                default:
                                    FileControl.ThisPage.Notification.Show("Transcoding failed:" + ExcuteStatus, 10000);
                                    break;
                            }
                        }
                    }).AsTask().Wait();
                }
            });
        }

        private void ShowCompleteNotification(StorageFile SourceFile, StorageFile DestinationFile)
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "转换已完成！"
                                },

                                new AdaptiveText()
                                {
                                   Text = SourceFile.Name + " 已成功转换为 " + DestinationFile.Name
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Transcoding has been completed！"
                                },

                                new AdaptiveText()
                                {
                                   Text = SourceFile.Name + " has been successfully transcoded to " + DestinationFile.Name
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private void ShowUserCancelNotification()
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "格式转换已被取消"
                                },

                                new AdaptiveText()
                                {
                                   Text = "您可以尝试重新启动转换"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Transcode has been cancelled"
                                },

                                new AdaptiveText()
                                {
                                   Text = "You can try restarting the transcode"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private void UpdateToastNotification(double CurrentValue)
        {
            string Tag = "TranscodeNotification";

            var data = new NotificationData
            {
                SequenceNumber = 0
            };
            data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
            data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

            ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
        }

        public void SendUpdatableToastWithProgress(StorageFile SourceFile, StorageFile DestinationFile)
        {
            string Tag = "TranscodeNotification";

            var content = new ToastContent()
            {
                Launch = "Transcode",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = MainPage.ThisPage.CurrentLanguage==LanguageEnum.Chinese
                                ? ("正在转换:"+SourceFile.DisplayName)
                                : ("Transcoding:"+SourceFile.DisplayName)
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = SourceFile.FileType.Substring(1).ToUpper()+" ⋙⋙⋙⋙ "+DestinationFile.FileType.Substring(1).ToUpper(),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressValueString"] = "0%";
            Toast.Data.Values["ProgressStatus"] = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "点击该提示以取消转码"
                : "Click the prompt to cancel transcoding";
            Toast.Data.SequenceNumber = 0;

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "TranscodeNotification")
                {
                    TranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }


        private async void FolderOpen_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl.ThisPage.CurrentNode.HasUnrealizedChildren && !FileControl.ThisPage.CurrentNode.IsExpanded)
            {
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }

            while (true)
            {
                var TargetNode = FileControl.ThisPage.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).Name == (GridViewControl.SelectedItem as FileSystemStorageItem).Name).FirstOrDefault();
                if (TargetNode != null)
                {
                    while (true)
                    {
                        if (FileControl.ThisPage.FolderTree.ContainerFromNode(TargetNode) is TreeViewItem Container)
                        {
                            Container.IsSelected = true;
                            Container.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
                            await FileControl.ThisPage.DisplayItemsInFolder(TargetNode);
                            break;
                        }
                        else
                        {
                            await Task.Delay(300);
                        }
                    }
                    break;
                }
                else if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name != "FileControl")
                {
                    break;
                }
                else
                {
                    await Task.Delay(300);
                }
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem Device = GridViewControl.SelectedItem as FileSystemStorageItem;
            AttributeDialog Dialog = new AttributeDialog(Device.Folder);
            _ = await Dialog.ShowAsync();
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            if (QRTeachTip.IsOpen)
            {
                QRTeachTip.IsOpen = false;
            }

            FileSystemStorageItem Item = GridViewControl.SelectedItem as FileSystemStorageItem;
            Restore();

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

            QRTeachTip.Target = GridViewControl.ContainerFromItem(Item) as GridViewItem;
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
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
            _ = await Launcher.LaunchFolderAsync(FileControl.ThisPage.CurrentFolder);
        }

        private async void ParentAttribute_Click(object sender, RoutedEventArgs e)
        {
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

        private async void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            if (GridViewControl.SelectedItem is FileSystemStorageItem ReFile && ReFile.ContentType == ContentType.File)
            {
                switch (ReFile.File.FileType)
                {
                    case ".zip":
                        Nav.Navigate(typeof(ZipExplorer), ReFile, new DrillInNavigationTransitionInfo());
                        break;
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                        Nav.Navigate(typeof(PhotoViewer), ReFile.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
                        break;
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".alac":
                        Nav.Navigate(typeof(MediaPlayer), ReFile.File, new DrillInNavigationTransitionInfo());
                        break;
                    case ".txt":
                        Nav.Navigate(typeof(TextViewer), ReFile, new DrillInNavigationTransitionInfo());
                        break;
                    case ".pdf":
                        Nav.Navigate(typeof(PdfReader), ReFile.File, new DrillInNavigationTransitionInfo());
                        break;
                    default:
                        if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "提示",
                                Content = "  RX文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                                PrimaryButtonText = "默认应用打开",
                                CloseButtonText = "取消",
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                            };
                            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFileAsync(ReFile.File);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Tips",
                                Content = "  RX FileManager could not open this file\r\r  But it can be opened with other applications",
                                PrimaryButtonText = "Open with default app",
                                CloseButtonText = "Cancel",
                                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                            };
                            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFileAsync(ReFile.File);
                            }
                        }
                        break;
                }
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void AddToLibray_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = (GridViewControl.SelectedItem as FileSystemStorageItem).Folder;
            if (ThisPC.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "提示",
                        Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                        CloseButtonText = "知道了",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Tips",
                        Content = "This folder has been added to the home page, can not be added repeatedly",
                        CloseButtonText = "知道了",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    _ = await dialog.ShowAsync();
                }
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync();
                ThisPC.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserAdded));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path);
            }
        }

        private async void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var NewFolder = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
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
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
            if (CutFile != null || CopyFile != null)
            {
                Paste.IsEnabled = true;
            }
        }

        private void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            DataTransferManager Manager = DataTransferManager.GetForCurrentView();
            if (GridViewControl.SelectedItem is FileSystemStorageItem ShareItem)
            {
                Manager.DataRequested += (s, args) =>
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
            await FileControl.ThisPage.DisplayItemsInFolder(DisplayNode, true);
        }

        private void GridViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingPage.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItem ReFile)
            {
                EnterSelectedItem(ReFile);
            }
        }

        private async void EnterSelectedItem(FileSystemStorageItem ReFile)
        {
            if (Interlocked.Exchange(ref DoubleTabTarget, ReFile) == null)
            {
                if (DoubleTabTarget.ContentType == ContentType.File)
                {
                    switch (DoubleTabTarget.File.FileType)
                    {
                        case ".zip":
                            Nav.Navigate(typeof(ZipExplorer), DoubleTabTarget, new DrillInNavigationTransitionInfo());
                            break;
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                            Nav.Navigate(typeof(PhotoViewer), DoubleTabTarget.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
                            break;
                        case ".mkv":
                        case ".mp4":
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".wmv":
                        case ".m4a":
                        case ".mov":
                        case ".alac":
                            Nav.Navigate(typeof(MediaPlayer), DoubleTabTarget.File, new DrillInNavigationTransitionInfo());
                            break;
                        case ".txt":
                            Nav.Navigate(typeof(TextViewer), DoubleTabTarget, new DrillInNavigationTransitionInfo());
                            break;
                        case ".pdf":
                            Nav.Navigate(typeof(PdfReader), DoubleTabTarget.File, new DrillInNavigationTransitionInfo());
                            break;
                        default:
                            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "  RX文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                                    PrimaryButtonText = "默认应用打开",
                                    CloseButtonText = "取消",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    if (!await Launcher.LaunchFileAsync(DoubleTabTarget.File))
                                    {
                                        LauncherOptions options = new LauncherOptions
                                        {
                                            DisplayApplicationPicker = true
                                        };
                                        _ = await Launcher.LaunchFileAsync(DoubleTabTarget.File, options);
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
                                    CloseButtonText = "Cancel",
                                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                };
                                if (!await Launcher.LaunchFileAsync(DoubleTabTarget.File))
                                {
                                    LauncherOptions options = new LauncherOptions
                                    {
                                        DisplayApplicationPicker = true
                                    };
                                    _ = await Launcher.LaunchFileAsync(DoubleTabTarget.File, options);
                                }
                            }
                            break;
                    }
                }
                else
                {
                    if (FileControl.ThisPage.CurrentNode.HasUnrealizedChildren && !FileControl.ThisPage.CurrentNode.IsExpanded)
                    {
                        FileControl.ThisPage.CurrentNode.IsExpanded = true;
                    }

                    while (true)
                    {
                        TreeViewNode TargetNode = FileControl.ThisPage.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).Name == DoubleTabTarget.Name).FirstOrDefault();
                        if (TargetNode != null)
                        {
                            while (true)
                            {
                                if (FileControl.ThisPage.FolderTree.ContainerFromNode(TargetNode) is TreeViewItem Container)
                                {
                                    Container.IsSelected = true;
                                    Container.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
                                    _ = FileControl.ThisPage.DisplayItemsInFolder(TargetNode);
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(300);
                                }
                            }
                            break;
                        }
                        else if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name != "FileControl")
                        {
                            break;
                        }
                        else
                        {
                            await Task.Delay(300);
                        }
                    }
                }
                Interlocked.Exchange(ref DoubleTabTarget, null);
            }
        }
    }
}

