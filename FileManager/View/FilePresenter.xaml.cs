using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using OpenCV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI;
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
        StorageFile CopyFile;
        StorageFile CutFile;
        AutoResetEvent AESControl;
        Frame Nav;
        Queue<StorageFile> AddToZipQueue;
        WiFiShareProvider WiFiProvider;
        const int AESCacheSize = 1048576;
        byte[] EncryptByteBuffer;
        byte[] DecryptByteBuffer;

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
            AddToZipQueue = new Queue<StorageFile>();
            AESControl = new AutoResetEvent(false);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            AddToZipQueue = null;
            AESControl?.Dispose();
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
            if (CopyFile != null)
            {
                CopyFile = null;
            }

            CopyFile = (GridViewControl.SelectedItem as FileSystemStorageItem).File;

            Paste.IsEnabled = true;

            if (CutFile != null)
            {
                CutFile = null;
            }

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
                            FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }
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
                            FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(CutFile, await CutFile.GetSizeDescriptionAsync(), await CutFile.GetThumbnailBitmapAsync(), await CutFile.GetModifiedTimeAsync()));
                        }
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
                            FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }
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
                            FileCollection.Insert(FileCollection.IndexOf(FileCollection.First((Item) => Item.ContentType == ContentType.File)), new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }
                        else
                        {
                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeDescriptionAsync(), await NewFile.GetThumbnailBitmapAsync(), await NewFile.GetModifiedTimeAsync()));
                        }
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
            if (CutFile != null)
            {
                CutFile = null;
            }

            CutFile = (GridViewControl.SelectedItem as FileSystemStorageItem).File;
            Paste.IsEnabled = true;

            if (CopyFile != null)
            {
                CopyFile = null;
            }
            Restore();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var FileToDelete = GridViewControl.SelectedItem as FileSystemStorageItem;
            Restore();

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                QueueContentDialog QueueContenDialog = new QueueContentDialog
                {
                    Title = "警告",
                    PrimaryButtonText = "是",
                    Content = "此操作将永久删除 \" " + FileToDelete.Name + " \"\r\r是否继续?",
                    CloseButtonText = "否",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    LoadingActivation(true, "正在删除");

                    try
                    {
                        await FileToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                        for (int i = 0; i < FileCollection.Count; i++)
                        {
                            if (FileCollection[i].RelativeId == FileToDelete.File.FolderRelativeId)
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
                    Content = "This action will permanently delete \" " + FileToDelete.Name + " \"\r\rWhether to continue?",
                    CloseButtonText = "Cancel",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    LoadingActivation(true, "Deleting");

                    try
                    {
                        await FileToDelete.File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                        for (int i = 0; i < FileCollection.Count; i++)
                        {
                            if (FileCollection[i].RelativeId == FileToDelete.File.FolderRelativeId)
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

            await Task.Delay(500);
            LoadingActivation(false);
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
            var file = (GridViewControl.SelectedItem as FileSystemStorageItem).File;
            RenameDialog dialog = new RenameDialog(file.DisplayName, file.FileType);
            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    if (dialog.DesireName == file.FileType)
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
                        await file.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                        foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                             where Item.Name == dialog.DesireName
                                             select Item)
                        {
                            await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(file.Path));
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
                    if (dialog.DesireName == file.FileType)
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
                        await file.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                        foreach (var Item in from FileSystemStorageItem Item in FileCollection
                                             where Item.Name == dialog.DesireName
                                             select Item)
                        {
                            await Item.UpdateRequested(await StorageFile.GetFileFromPathAsync(file.Path));
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
                if (e.AddedItems.Count == 0)
                {
                    return;
                }

                if ((GridViewControl.SelectedItem as FileSystemStorageItem).ContentType == ContentType.File)
                {
                    Transcode.IsEnabled = false;

                    Zip.Label = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                        ? "Zip压缩"
                        : "Zip Compression";
                    switch ((e.AddedItems.FirstOrDefault() as FileSystemStorageItem).Type)
                    {
                        case ".zip":
                            Zip.Label = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                ? "Zip解压"
                                : "Zip Decompression";
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
                            Transcode.IsEnabled = true;
                            break;
                    }

                    AES.Label = (e.AddedItems.FirstOrDefault() as FileSystemStorageItem).Type == ".sle"
                        ? (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "AES解密" : "AES Decryption")
                        : (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "AES加密" : "AES Encryption");
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var Context = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemStorageItem;
            GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);
            e.Handled = true;
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

            if (FileControl.ThisPage.CurrentNode.IsExpanded)
            {
                FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                {
                    Content = await FileControl.ThisPage.CurrentFolder.GetFolderAsync(NewFolder.Name),
                    HasUnrealizedChildren = false
                });
            }
            else if (!FileControl.ThisPage.CurrentNode.HasChildren)
            {
                FileControl.ThisPage.CurrentNode.HasUnrealizedChildren = true;
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }
            else
            {
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }

            while (true)
            {
                TreeViewNode Node = FileControl.ThisPage.CurrentNode?.Children.Where((Folder) => NewFolder.Name == (Folder.Content as StorageFolder).Name).FirstOrDefault();
                if (Node != null)
                {
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
                    await Task.Delay(500);
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

        private async void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem ReFile && ReFile.ContentType == ContentType.File)
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

        private void GridViewControl_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            foreach (var GridItem in from File in FileCollection
                                     where File.Type == ".zip"
                                     let GridItem = GridViewControl.ContainerFromItem(File) as GridViewItem
                                     select GridItem)
            {
                GridItem.AllowDrop = true;
                GridItem.DragEnter += GridItem_DragEnter;
                GridItem.Drop += GridItem_Drop;

                ZipCollection.Add(GridItem);
            }
            AddToZipQueue?.Clear();
            foreach (FileSystemStorageItem item in e.Items)
            {
                AddToZipQueue?.Enqueue(item.File);
            }
        }

        private void GridItem_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "添加至Zip文件"
                : "Add To Zip File";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void GridItem_Drop(object sender, DragEventArgs e)
        {
            if ((e.OriginalSource as GridViewItem).Content is FileSystemStorageItem file)
            {
                await AddFileToZipAsync(file);
            }
        }

        /// <summary>
        /// 向ZIP文件添加新文件
        /// </summary>
        /// <param name="file">待添加的文件</param>
        /// <returns>无</returns>
        public async Task AddFileToZipAsync(FileSystemStorageItem file)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在执行添加操作"
                : "Adding");

            using (var ZipFileStream = (await file.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    await Task.Run(async () =>
                    {
                        while (AddToZipQueue.Count > 0)
                        {
                            zipFile.BeginUpdate();

                            StorageFile ToAddFile = AddToZipQueue.Dequeue();
                            using (var filestream = await ToAddFile.OpenStreamForReadAsync())
                            {
                                CustomStaticDataSource CSD = new CustomStaticDataSource();
                                CSD.SetStream(filestream);
                                zipFile.Add(CSD, ToAddFile.Name);
                                zipFile.CommitUpdate();
                            }
                        }
                    });

                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            await file.SizeUpdateRequested();

            await Task.Delay(500);
            LoadingActivation(false);
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            StorageFile file = (GridViewControl.SelectedItem as FileSystemStorageItem).File;
            TranscodeDialog dialog = new TranscodeDialog
            {
                SourceFile = file
            };
            await dialog.ShowAsync();
        }

        private void GridViewControl_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            foreach (var GridItem in ZipCollection)
            {
                GridItem.AllowDrop = false;
                GridItem.DragEnter -= GridItem_DragEnter;
                GridItem.Drop -= GridItem_Drop;
            }
            ZipCollection.Clear();
        }

        private async void FolderOpen_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl.ThisPage.CurrentNode.HasUnrealizedChildren && !FileControl.ThisPage.CurrentNode.IsExpanded)
            {
                FileControl.ThisPage.CurrentNode.IsExpanded = true;
            }

            while (true)
            {
                var TargetNode = FileControl.ThisPage.CurrentNode?.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == (GridViewControl.SelectedItem as FileSystemStorageItem).RelativeId).FirstOrDefault();
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
                    await Task.Delay(500);
                }
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            var Folder = (GridViewControl.SelectedItem as FileSystemStorageItem).Folder;
            RenameDialog dialog = new RenameDialog(Folder.DisplayName);
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
                    var TargetNode = FileControl.ThisPage.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).FolderRelativeId == Folder.FolderRelativeId).FirstOrDefault();
                    int index = FileControl.ThisPage.CurrentNode.Children.IndexOf(TargetNode);

                    try
                    {
                        await Folder.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
                        ReCreateFolder = await StorageFolder.GetFolderFromPathAsync(Folder.Path);

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

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem Device = GridViewControl.SelectedItem as FileSystemStorageItem;
            AttributeDialog Dialog = new AttributeDialog(Device.Folder);
            _ = await Dialog.ShowAsync();
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            var SelectedItem = GridViewControl.SelectedItem as FileSystemStorageItem;
            Restore();

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                QueueContentDialog QueueContenDialog = new QueueContentDialog
                {
                    Title = "警告",
                    PrimaryButtonText = "是",
                    CloseButtonText = "否",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush,
                    Content = "此操作将永久删除 \"" + SelectedItem.DisplayName + " \"\r\r是否继续?"
                };

                if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    foreach (FileSystemStorageItem Item in GridViewControl.SelectedItems)
                    {
                        try
                        {
                            await Item.Folder.DeleteAllSubFilesAndFolders();
                            await Item.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
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

                        FileCollection.Remove(Item);
                        if (FileControl.ThisPage.CurrentNode.IsExpanded)
                        {
                            FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == Item.RelativeId).FirstOrDefault());
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
                    Content = "This action will permanently delete \" " + SelectedItem.DisplayName + " \"\r\rWhether to continue ?"
                };

                if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    foreach (FileSystemStorageItem Item in GridViewControl.SelectedItems)
                    {
                        try
                        {
                            await Item.Folder.DeleteAllSubFilesAndFolders();
                            await Item.Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
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

                        FileCollection.Remove(Item);
                        if (FileControl.ThisPage.CurrentNode.IsExpanded)
                        {
                            FileControl.ThisPage.CurrentNode.Children.Remove(FileControl.ThisPage.CurrentNode.Children.Where((Node) => (Node.Content as StorageFolder).FolderRelativeId == Item.RelativeId).FirstOrDefault());
                        }
                    }
                }
            }
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
            AttributeDialog Dialog = new AttributeDialog(FileControl.ThisPage.CurrentFolder);
            _ = await Dialog.ShowAsync();
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
    }
}

