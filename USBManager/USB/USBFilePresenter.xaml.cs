using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Radios;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace USBManager
{
    public sealed partial class USBFilePresenter : Page
    {
        public ObservableCollection<RemovableDeviceFile> FileCollection = new ObservableCollection<RemovableDeviceFile>();
        public static USBFilePresenter ThisPage { get; private set; }
        public List<GridViewItem> ZipCollection = new List<GridViewItem>();
        Queue<StorageFile> CopyedQueue;
        Queue<StorageFile> CutQueue;
        AutoResetEvent AESControl;
        DispatcherTimer Ticker;
        Frame Nav;
        Queue<StorageFile> AddToZipQueue;
        const int AESCacheSize = 1048576;
        byte[] EncryptByteBuffer;
        byte[] DecryptByteBuffer;

        public USBFilePresenter()
        {
            InitializeComponent();
            ThisPage = this;
            GridViewControl.ItemsSource = FileCollection;
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            //必须注册这个东西才能使用中文解码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Nav = e.Parameter as Frame;
            CopyedQueue = new Queue<StorageFile>();
            CutQueue = new Queue<StorageFile>();
            AddToZipQueue = new Queue<StorageFile>();
            AESControl = new AutoResetEvent(false);
            Ticker = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            Ticker.Tick += (s, v) =>
            {
                ProgressInfo.Text = ProgressInfo.Text + "\r文件较大，请耐心等待...";
                Ticker.Stop();
            };
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CopyedQueue = null;
            CutQueue = null;
            AddToZipQueue = null;
            AESControl?.Dispose();
            Ticker?.Stop();
            Ticker = null;
        }

        /// <summary>
        /// 从StorageFile获取该文件的大小，并转换为易读的描述
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns></returns>
        public async Task<string> GetSizeAsync(StorageFile file)
        {
            BasicProperties Properties = await file.GetBasicPropertiesAsync();
            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
            (Properties.Size / 1048576f >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB");
        }

        /// <summary>
        /// 关闭右键菜单并将GridView从多选模式恢复到单选模式
        /// </summary>
        private void Restore()
        {
            CommandsFlyout.Hide();
            if (GridViewControl.SelectionMode != ListViewSelectionMode.Single)
            {
                GridViewControl.SelectionMode = ListViewSelectionMode.Single;
            }
        }
        private void MulSelection_Click(object sender, RoutedEventArgs e)
        {
            CommandsFlyout.Hide();
            GridViewControl.SelectionMode = GridViewControl.SelectionMode != ListViewSelectionMode.Multiple
                ? ListViewSelectionMode.Multiple
                : ListViewSelectionMode.Single;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (CopyedQueue.Count != 0)
            {
                CopyedQueue.Clear();
            }
            foreach (RemovableDeviceFile item in GridViewControl.SelectedItems)
            {
                CopyedQueue.Enqueue(item.File);
            }
            Paste.IsEnabled = true;
            if (CutQueue.Count != 0)
            {
                CutQueue.Clear();
            }
            Restore();
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();
            if (CutQueue.Count != 0)
            {
                LoadingActivation(true, "正在剪切");
                Queue<string> ErrorCollection = new Queue<string>();

                while (CutQueue.Count != 0)
                {
                    var CutFile = CutQueue.Dequeue();
                    try
                    {
                        await CutFile.MoveAsync(USBControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        //收集但不立刻报告错误
                        ErrorCollection.Enqueue(CutFile.Name);
                    }
                }

                if (ErrorCollection.Count != 0)
                {
                    string ErrorFileList = "";
                    while (ErrorCollection.Count != 0)
                    {
                        ErrorFileList = ErrorFileList + ErrorCollection.Dequeue() + "\r";
                    }
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "因设备剩余空间大小不足\r以下文件无法剪切：\r" + ErrorFileList,
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    LoadingActivation(false);
                    await contentDialog.ShowAsync();
                }

                await RefreshFileDisplay();
                await Task.Delay(500);
                LoadingActivation(false);
                Paste.IsEnabled = false;
            }
            else if (CopyedQueue.Count != 0)
            {

                LoadingActivation(true, "正在复制");
                Queue<string> ErrorCollection = new Queue<string>();
                while (CopyedQueue.Count != 0)
                {
                    var CopyedFile = CopyedQueue.Dequeue();
                    try
                    {
                        await CopyedFile.CopyAsync(USBControl.ThisPage.CurrentFolder, CopyedFile.Name, NameCollisionOption.GenerateUniqueName);
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        ErrorCollection.Enqueue(CopyedFile.Name);
                    }
                }
                if (ErrorCollection.Count != 0)
                {
                    string ErrorFileList = "";
                    while (ErrorCollection.Count != 0)
                    {
                        ErrorFileList = ErrorFileList + ErrorCollection.Dequeue() + "\r";
                    }
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "因设备剩余空间大小不足\r以下文件无法复制：\r\r" + ErrorFileList,
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    LoadingActivation(false);
                    await contentDialog.ShowAsync();
                }
                await RefreshFileDisplay();
                await Task.Delay(500);
                LoadingActivation(false);
            }
            Paste.IsEnabled = false;
        }

        /// <summary>
        /// 异步刷新并检查是否有新文件出现
        /// </summary>
        public async Task RefreshFileDisplay()
        {
            QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null)
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };

            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);
            Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

            StorageFileQueryResult QueryResult = USBControl.ThisPage.CurrentFolder.CreateFileQueryWithOptions(Options);

            var FileList = await QueryResult.GetFilesAsync();

            foreach (var file in FileList)
            {
                int i = 0;
                for (; i < FileCollection.Count; i++)
                {
                    if (FileCollection[i].Name == file.Name)
                    {
                        break;
                    }
                }
                if (i == FileCollection.Count)
                {
                    IDictionary<string, object> PropertyResults = await file.Properties.RetrievePropertiesAsync(new string[] { "System.Size" });
                    ulong PropertiesSize = (ulong)PropertyResults["System.Size"];
                    string Size = GetSizeDescription(PropertiesSize);

                    var Thumbnail = await GetThumbnailAsync(file);
                    if (Thumbnail != null)
                    {
                        FileCollection.Add(new RemovableDeviceFile(Size, file, Thumbnail));
                    }
                    else
                    {
                        FileCollection.Add(new RemovableDeviceFile(Size, file, new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"))));
                    }
                }
            }
        }

        /// <summary>
        /// 从文件大小获取标准描述
        /// </summary>
        /// <param name="PropertiesSize">文件大小</param>
        /// <returns></returns>
        private string GetSizeDescription(ulong PropertiesSize)
        {
            return PropertiesSize / 1024f < 1024 ? Math.Round(PropertiesSize / 1024f, 2).ToString() + " KB" : (PropertiesSize / 1048576f >= 1024 ? Math.Round(PropertiesSize / 1073741824f, 2).ToString() + " GB" : Math.Round(PropertiesSize / 1048576f, 2).ToString() + " MB");
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (CutQueue.Count != 0)
            {
                CutQueue.Clear();
            }
            foreach (RemovableDeviceFile item in GridViewControl.SelectedItems)
            {
                CutQueue.Enqueue(item.File);
            }
            Paste.IsEnabled = true;
            if (CopyedQueue.Count != 0)
            {
                CopyedQueue.Clear();
            }
            Restore();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "警告",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
            };

            contentDialog.Content = FileList.Count == 1
                ? "此操作将永久删除 \"" + (FileList[0] as RemovableDeviceFile).Name + " \"\r\r是否继续?"
                : "此操作将永久删除 \"" + (FileList[0] as RemovableDeviceFile).Name + "\" 等" + FileList.Count + "个文件\r\r是否继续?";

            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                LoadingActivation(true, "正在删除");
                foreach (var item in FileList)
                {
                    var file = (item as RemovableDeviceFile).File;
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    for (int i = 0; i < FileCollection.Count; i++)
                    {
                        if (FileCollection[i].RelativeId == file.FolderRelativeId)
                        {
                            FileCollection.RemoveAt(i);
                            i--;
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
                    Ticker.Start();
                }
            }
            else
            {
                if (!EnableProgressDisplay)
                {
                    Ticker.Stop();
                }
            }
            LoadingControl.IsLoading = IsLoading;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count > 1)
            {
                Restore();
                ContentDialog content = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法同时重命名多个文件",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await content.ShowAsync();
                return;
            }

            var file = (GridViewControl.SelectedItem as RemovableDeviceFile).File;
            RenameDialog dialog = new RenameDialog(file.DisplayName, file.FileType);
            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (dialog.DesireName == file.FileType)
                {
                    ContentDialog content = new ContentDialog
                    {
                        Title = "错误",
                        Content = "文件名不能为空，重命名失败",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await content.ShowAsync();
                    return;
                }
                await file.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
            }
            else
            {
                return;
            }
            for (int i = 0; i < FileCollection.Count; i++)
            {
                if (FileCollection[i].Name == dialog.DesireName)
                {
                    FileCollection[i].NameUpdateRequested();
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
            var FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            string CheckSame = ".sle";
            int CheckCount = 0;
            for (int i = 0; i < FileList.Count; i++)
            {
                if (((RemovableDeviceFile)FileList[i]).File.FileType == CheckSame)
                {
                    CheckCount++;
                }
            }
            if (CheckCount != FileList.Count && CheckCount != 0)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "  同时加密或解密多个文件时，.sle文件不能与其他文件混杂\r\r  允许的组合如下：\r\r      • 全部为.sle文件\r\r      • 全部为非.sln文件",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }

            foreach (var SelectedFile in from RemovableDeviceFile AESFile in FileList select AESFile.File)
            {
                int KeySizeRequest;
                string KeyRequest;
                bool IsDeleteRequest;
                if (SelectedFile.FileType != ".sle")
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
                    LoadingActivation(true, "正在加密");
                    await Task.Run(async () =>
                    {
                        using (var FileStream = await SelectedFile.OpenStreamForReadAsync())
                        {
                            StorageFile file = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + ".sle", CreationCollisionOption.GenerateUniqueName);
                            using (var TargetFileStream = await file.OpenStreamForWriteAsync())
                            {
                                byte[] Tail = Encoding.UTF8.GetBytes("$" + KeySizeRequest + "|" + SelectedFile.FileType + "$");
                                byte[] PasswordFlag = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");

                                if (FileStream.Length < AESCacheSize)
                                {
                                    EncryptByteBuffer = new byte[FileStream.Length];
                                    FileStream.Read(EncryptByteBuffer, 0, EncryptByteBuffer.Length);
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.EncryptForUSB(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);
                                    var EncryptedBytes = AESProvider.EncryptForUSB(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                    await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                }
                                else
                                {
                                    EncryptByteBuffer = new byte[Tail.Length];
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.EncryptForUSB(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);

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
                                        var EncryptedBytes = AESProvider.EncryptForUSB(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                        await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                    }

                                }
                            }
                        }
                    });
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

                    LoadingActivation(true, "正在解密");
                    await Task.Run(async () =>
                    {
                        using (var FileStream = await SelectedFile.OpenStreamForReadAsync())
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
                                    ContentDialog dialog = new ContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  文件格式检验错误，文件可能已损坏",
                                        CloseButtonText = "确定",
                                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                                    };
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
                            if (Encoding.UTF8.GetString(AESProvider.DecryptForUSB(PasswordConfirm, KeyRequest, EncryptKeySize)) != "PASSWORD_CORRECT")
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    ContentDialog dialog = new ContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  密码错误，无法解密\r\r  请重试...",
                                        CloseButtonText = "确定",
                                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                                    };
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
                            DecryptedBytes = AESProvider.DecryptForUSB(DecryptByteBuffer, KeyRequest, EncryptKeySize);

                            StorageFile file = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + FileType, CreationCollisionOption.GenerateUniqueName);
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
                                        DecryptedBytes = AESProvider.DecryptForUSB(DecryptByteBuffer, KeyRequest, EncryptKeySize);
                                        await TargetFileStream.WriteAsync(DecryptedBytes, 0, DecryptedBytes.Length);
                                    }

                                }
                            }
                        }
                    });
                }

                if (IsDeleteRequest)
                {
                    await SelectedFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    for (int i = 0; i < FileCollection.Count; i++)
                    {
                        if (FileCollection[i].RelativeId == SelectedFile.FolderRelativeId)
                        {
                            FileCollection.RemoveAt(i);
                            break;
                        }
                    }
                }

                DecryptByteBuffer = null;
                EncryptByteBuffer = null;
            }

            await Task.Delay(500);
            LoadingActivation(false);
            await RefreshFileDisplay();
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
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "提示",
                        Content = "请开启蓝牙开关后再试",
                        CloseButtonText = "确定"
                    };
                    _ = await dialog.ShowAsync();
                    return;
                }
            }

            List<object> FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                foreach (RemovableDeviceFile file in FileList)
                {
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
                }
            });
        }

        private void GridViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (GridViewControl.SelectedIndex == -1)
                {
                    Copy.IsEnabled = false;
                    Cut.IsEnabled = false;
                    Rename.IsEnabled = false;
                    AES.IsEnabled = false;
                    Delete.IsEnabled = false;
                    Transcode.IsEnabled = false;
                    return;
                }
                else
                {
                    Copy.IsEnabled = true;
                    Cut.IsEnabled = true;
                    Rename.IsEnabled = true;
                    Delete.IsEnabled = true;
                    AES.IsEnabled = true;
                    AES.Label = "AES加密";
                    foreach (var _ in from RemovableDeviceFile item in e.AddedItems
                                      where item.File.FileType == ".sle"
                                      select new { })
                    {
                        AES.Label = "AES解密";
                        break;
                    }

                    if (e.AddedItems.Count == 1)
                    {
                        switch ((e.AddedItems.FirstOrDefault() as RemovableDeviceFile).Type)
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
                                Transcode.IsEnabled = true;
                                break;
                            default:
                                Transcode.IsEnabled = false;
                                break;
                        }
                    }
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count <= 1)
            {
                var Context = (e.OriginalSource as FrameworkElement)?.DataContext as RemovableDeviceFile;
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 获得指定文件的缩略图图像
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns>缩略图图像</returns>
        public async Task<BitmapImage> GetThumbnailAsync(StorageFile file)
        {
            var Thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }
            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 60,
                DecodePixelWidth = 60
            };
            await bitmapImage.SetSourceAsync(Thumbnail);
            return bitmapImage;
        }

        private void GridViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count <= 1)
            {
                var Context = (e.OriginalSource as FrameworkElement)?.DataContext as RemovableDeviceFile;
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);
                e.Handled = true;
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            var SelectedGroup = GridViewControl.SelectedItems;
            if (SelectedGroup.Count != 1)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "仅允许查看单个文件属性，请重试",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog((SelectedGroup[0] as RemovableDeviceFile).File);
                await Dialog.ShowAsync();
            }
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            List<object> FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();

            int CheckCount = 0;
            for (int i = 0; i < FileList.Count; i++)
            {
                if (((RemovableDeviceFile)FileList[i]).File.FileType == ".zip")
                {
                    CheckCount++;
                }
            }
            if (CheckCount == FileList.Count)
            {
                await UnZipAsync(FileList);
            }
            else
            {
                ZipDialog dialog = FileList.Count == 1 ? new ZipDialog(true, (FileList[0] as RemovableDeviceFile).DisplayName) : new ZipDialog(true);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    LoadingActivation(true, "正在压缩", true);
                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(FileList, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password);
                    }
                    else
                    {
                        await CreateZipAsync(FileList, dialog.FileName, (int)dialog.Level);
                    }
                    await RefreshFileDisplay();
                }
                else
                {
                    return;
                }
            }
            LoadingActivation(false);
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task UnZipAsync(List<object> ZFileList)
        {
            foreach (RemovableDeviceFile ZFile in ZFileList)
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
                                LoadingActivation(true, "正在解压", true);
                                zipFile.Password = dialog.Password;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            LoadingActivation(true, "正在解压", true);
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
                                    NewFolder = await USBControl.ThisPage.CurrentFolder.CreateFolderAsync(ZFile.File.DisplayName, CreationCollisionOption.OpenIfExists);
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
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "错误",
                            Content = "解压文件时发生异常\r\r错误信息：\r\r" + e.Message,
                            Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush,
                            CloseButtonText = "确定"
                        };
                        await dialog.ShowAsync();
                        break;
                    }
                    finally
                    {
                        zipFile.IsStreamOwner = false;
                        zipFile.Close();
                    }
                }
                string RelativeId = (USBControl.ThisPage.CurrentNode.Content as StorageFolder).FolderRelativeId;

                foreach (var _ in from item in USBControl.ThisPage.CurrentNode.Children
                                  where (item.Content as StorageFolder).FolderRelativeId == NewFolder.FolderRelativeId
                                  select new { })
                {
                    goto JUMP;
                }

                USBControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                {
                    Content = await USBControl.ThisPage.CurrentFolder.GetFolderAsync(NewFolder.Name),
                    HasUnrealizedChildren = false
                });
            JUMP: continue;
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
        private async Task CreateZipAsync(List<object> FileList, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            var Newfile = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);
            using (var NewFileStream = await Newfile.OpenStreamForWriteAsync())
            {
                ZipOutputStream ZipStream = new ZipOutputStream(NewFileStream);
                try
                {
                    ZipStream.SetLevel(ZipLevel);
                    ZipStream.UseZip64 = UseZip64.Off;
                    int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                    if (EnableCryption)
                    {
                        ZipStream.Password = Password;
                        await Task.Run(async () =>
                        {
                            foreach (var (ZipFile, NewEntry) in from RemovableDeviceFile ZipFile in FileList
                                                                let NewEntry = new ZipEntry(ZipFile.File.Name)
                                                                {
                                                                    DateTime = DateTime.Now,
                                                                    AESKeySize = (int)Size,
                                                                    IsCrypted = true,
                                                                    CompressionMethod = CompressionMethod.Deflated
                                                                }
                                                                select (ZipFile, NewEntry))
                            {
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
                                                TCounter = (int)e.PercentComplete;
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }
                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / (float)FileList.Count);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            }
                        });
                    }
                    else
                    {
                        await Task.Run(async () =>
                        {
                            foreach (var (ZipFile, NewEntry) in from RemovableDeviceFile ZipFile in FileList
                                                                let NewEntry = new ZipEntry(ZipFile.File.Name)
                                                                {
                                                                    DateTime = DateTime.Now
                                                                }
                                                                select (ZipFile, NewEntry))
                            {
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
                                                TCounter = (int)e.PercentComplete;
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }

                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / (float)FileList.Count);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush,
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    ZipStream.IsStreamOwner = false;
                    ZipStream.Close();
                }

            }
        }

        private async void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is RemovableDeviceFile ReFile)
            {
                switch (ReFile.File.FileType)
                {
                    case ".zip":
                        Nav.Navigate(typeof(ZipExplorer), ReFile, new DrillInNavigationTransitionInfo());
                        break;
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                        Nav.Navigate(typeof(USBPhotoViewer), ReFile.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
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
                        Nav.Navigate(typeof(USBMediaPlayer), ReFile.File, new DrillInNavigationTransitionInfo());
                        break;
                    case ".txt":
                        Nav.Navigate(typeof(USBTextViewer), ReFile, new DrillInNavigationTransitionInfo());
                        break;
                    case ".pdf":
                        Nav.Navigate(typeof(USBPdfReader), ReFile.File, new DrillInNavigationTransitionInfo());
                        break;
                    default:
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "提示",
                            Content = "  USB文件管理器无法打开此文件\r\r  但可以使用其他应用程序打开",
                            PrimaryButtonText = "默认应用打开",
                            CloseButtonText = "取消"
                        };
                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            await Launcher.LaunchFileAsync(ReFile.File);
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
            foreach (RemovableDeviceFile item in e.Items)
            {
                AddToZipQueue?.Enqueue(item.File);
            }
        }

        private void GridItem_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添加至Zip文件";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void GridItem_Drop(object sender, DragEventArgs e)
        {
            if ((e.OriginalSource as GridViewItem).Content is RemovableDeviceFile file)
            {
                await AddFileToZipAsync(file);
            }
        }

        /// <summary>
        /// 向ZIP文件添加新文件
        /// </summary>
        /// <param name="file">待添加的文件</param>
        /// <returns>无</returns>
        public async Task AddFileToZipAsync(RemovableDeviceFile file)
        {
            LoadingActivation(true, "正在执行添加操作");
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

            file.SizeUpdateRequested(await GetSizeAsync(file.File));

            await Task.Delay(500);
            LoadingActivation(false);
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            var SelectedItems = GridViewControl.SelectedItems;
            if (SelectedItems.Count == 1)
            {
                StorageFile file = (SelectedItems[0] as RemovableDeviceFile).File;
                TranscodeDialog dialog = new TranscodeDialog
                {
                    SourceFile = file
                };
                await dialog.ShowAsync();
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "一次仅支持转码一个媒体文件",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();
                Restore();
            }
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
    }
}
