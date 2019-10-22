using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class ZipExplorer : Page
    {
        ObservableCollection<ZipFileDisplay> FileCollection;
        FileSystemStorageItem OriginFile;

        public ZipExplorer()
        {
            InitializeComponent();
            Loaded += ZipExplorer_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            OriginFile = e.Parameter as FileSystemStorageItem;
            FileCollection = new ObservableCollection<ZipFileDisplay>();
            GridControl.ItemsSource = FileCollection;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            FileCollection.Clear();
            FileCollection = null;
            OriginFile = null;
        }

        private async void ZipExplorer_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                ZIPFileName.Text = "Zip文件查看器 - " + OriginFile.File.Name;
                using (var ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
                {
                    ZipFile zipFile = new ZipFile(ZipFileStream);
                    try
                    {
                        foreach (ZipEntry Entry in zipFile)
                        {
                            if (!Entry.IsFile)
                            {
                                continue;
                            }
                            string DisplayName, Type;
                            int index = Entry.Name.LastIndexOf(".");
                            if (index != -1)
                            {
                                DisplayName = Entry.Name.Substring(0, index);
                                Type = Entry.Name.Substring(index + 1).ToUpper() + "文件";
                            }
                            else
                            {
                                DisplayName = Entry.Name;
                                Type = "未知文件类型";
                            }
                            FileCollection.Add(new ZipFileDisplay(DisplayName, Type, "压缩大小：" + GetSize(Entry.CompressedSize), "解压大小：" + GetSize(Entry.Size), GetDate(Entry.DateTime), Entry.IsCrypted));
                        }
                    }
                    finally
                    {
                        zipFile.IsStreamOwner = false;
                        zipFile.Close();
                    }
                }
            }
            else
            {
                ZIPFileName.Text = "Zip Viewer - " + OriginFile.File.Name;
                using (var ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
                {
                    ZipFile zipFile = new ZipFile(ZipFileStream);
                    try
                    {
                        foreach (ZipEntry Entry in zipFile)
                        {
                            if (!Entry.IsFile)
                            {
                                continue;
                            }
                            string DisplayName, Type;
                            int index = Entry.Name.LastIndexOf(".");
                            if (index != -1)
                            {
                                DisplayName = Entry.Name.Substring(0, index);
                                Type = Entry.Name.Substring(index + 1).ToUpper() + "File";
                            }
                            else
                            {
                                DisplayName = Entry.Name;
                                Type = "UnknownType";
                            }
                            FileCollection.Add(new ZipFileDisplay(DisplayName, Type, "Compressed：" + GetSize(Entry.CompressedSize), "Decompressed：" + GetSize(Entry.Size), GetDate(Entry.DateTime), Entry.IsCrypted));
                        }
                    }
                    finally
                    {
                        zipFile.IsStreamOwner = false;
                        zipFile.Close();
                    }
                }
            }
        }

        /// <summary>
        /// 获取文件大小的描述
        /// </summary>
        /// <param name="Size">大小</param>
        /// <returns>大小描述</returns>
        private string GetSize(long Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString() + " KB" :
            (Size / 1048576f >= 1024 ? Math.Round(Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Size / 1048576f, 2).ToString() + " MB");
        }

        /// <summary>
        /// 获取创建时间的描述
        /// </summary>
        /// <param name="time">时间</param>
        /// <returns></returns>
        private string GetDate(DateTime time)
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                return "创建时间：" + time.ToString("F");
            }
            else
            {
                return "Created：" + time.ToString("F");
            }
        }

        private void GridControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var Context = (e.OriginalSource as FrameworkElement)?.DataContext as ZipFileDisplay;
            GridControl.SelectedIndex = FileCollection.IndexOf(Context);
            e.Handled = true;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在执行删除操作"
                : "Deleting");
            var file = GridControl.SelectedItem as ZipFileDisplay;
            using (var ZipFileStream = (await OriginFile.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    foreach (ZipEntry Entry in zipFile)
                    {
                        string FullName;
                        int index = Entry.Name.LastIndexOf(".");
                        if (index != -1)
                        {
                            FullName = file.Name + "." + file.Type.Substring(0, file.Type.Length - 2).ToLower();
                        }
                        else
                        {
                            FullName = file.Name;
                        }
                        if (Entry.Name == FullName)
                        {
                            await Task.Run(() =>
                            {
                                zipFile.BeginUpdate();
                                zipFile.Delete(Entry);
                                zipFile.CommitUpdate();
                            });
                            FileCollection.Remove(file);
                            break;
                        }
                    }

                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            await OriginFile.SizeUpdateRequested();
            await Task.Delay(500);
            LoadingActivation(false);
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在检验文件"
                : "Verifying");
            var file = GridControl.SelectedItem as ZipFileDisplay;
            using (var ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    bool Mode = default;
                    switch ((sender as MenuFlyoutItem).Name)
                    {
                        case "Simple": Mode = false; break;
                        case "Full": Mode = true; break;
                    }
                    bool IsCorrect = await Task.Run(() =>
                    {
                        return zipFile.TestArchive(Mode);
                    });
                    if (!Mode)
                    {
                        await Task.Delay(1000);
                    }

                    QueueContentDialog QueueContenDialog;
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContenDialog = new QueueContentDialog
                        {
                            Title = "测试结果",
                            Content = IsCorrect ? "CRC校验通过，Zip文件完整" : "未能通过CRC校验，Zip文件存在问题",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }
                    else
                    {
                        QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Test Result",
                            Content = IsCorrect ? "The CRC is verified" : "Failed to pass CRC check",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }
                    LoadingActivation(false);
                    await Task.Delay(500);
                    await QueueContenDialog.ShowAsync();
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }
        }

        private void GridControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridControl.SelectedIndex == -1)
            {
                Delete.IsEnabled = false;
                Test.IsEnabled = true;
            }
            else
            {
                Delete.IsEnabled = true;
                Test.IsEnabled = false;
            }
        }

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
                }
            }
            LoadingControl.IsLoading = IsLoading;
        }


        private async void Decompression_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在解压"
                : "Extracting", true);

            var file = GridControl.SelectedItem as ZipFileDisplay;
            using (var ZipFileStream = (await OriginFile.File.OpenStreamForReadAsync()))
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    foreach (ZipEntry Entry in zipFile)
                    {
                        string FullName;
                        StorageFolder NewFolder = null;
                        int index = Entry.Name.LastIndexOf(".");
                        if (index != -1)
                        {
                            FullName = file.Name + "." + file.Type.Substring(0, file.Type.Length - 2).ToLower();
                        }
                        else
                        {
                            FullName = file.Name;
                        }
                        if (Entry.Name == FullName)
                        {
                            await Task.Run(async () =>
                            {
                                using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                                {
                                    NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(OriginFile.DisplayName, CreationCollisionOption.OpenIfExists);
                                    StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                                    using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                                    {
                                        double FileSize = Entry.Size;
                                        int RepeatFilter = -1;
                                        StreamUtils.Copy(ZipTempStream, stream, new byte[4096], async (s, m) =>
                                        {
                                            await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                            {
                                                lock (SyncRootProvider.SyncRoot)
                                                {
                                                    string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                    int TCounter = Convert.ToInt32((m.Processed / FileSize) * 100);
                                                    if (RepeatFilter == TCounter)
                                                    {
                                                        return;
                                                    }
                                                    else
                                                    {
                                                        RepeatFilter = TCounter;
                                                    }

                                                    ProgressInfo.Text = temp + TCounter + "%";
                                                    ProBar.Value = TCounter;
                                                }
                                            });

                                        }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    }
                                }

                            });
                            string RelativeId = FileControl.ThisPage.CurrentFolder.FolderRelativeId;

                            foreach (var _ in from Node in FileControl.ThisPage.CurrentNode.Children
                                              where (Node.Content as StorageFolder).FolderRelativeId == NewFolder.FolderRelativeId
                                              select new { })
                            {
                                goto JUMP;
                            }

                            if (FileControl.ThisPage.CurrentNode.IsExpanded || !FileControl.ThisPage.CurrentNode.HasChildren)
                            {
                                FileControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                                {
                                    Content = await FileControl.ThisPage.CurrentFolder.GetFolderAsync(NewFolder.Name),
                                    HasUnrealizedChildren = false
                                });
                            }
                            FileControl.ThisPage.CurrentNode.IsExpanded = true;

                        JUMP: break;
                        }
                    }

                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            await Task.Delay(1000);
            LoadingActivation(false);
        }
    }
}

