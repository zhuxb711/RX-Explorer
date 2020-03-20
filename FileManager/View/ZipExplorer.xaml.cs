using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class ZipExplorer : Page
    {
        ObservableCollection<ZipFileDisplay> FileCollection;
        FileSystemStorageItem OriginFile;
        FileControl FileControlInstance;

        public ZipExplorer()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, FileSystemStorageItem> Parameters)
            {
                FileControlInstance = Parameters.Item1;
                OriginFile = Parameters.Item2;
                FileCollection = new ObservableCollection<ZipFileDisplay>();
                FileCollection.CollectionChanged += FileCollection_CollectionChanged;
                GridControl.ItemsSource = FileCollection;

                await Initialize().ConfigureAwait(false);
            }
        }

        private async Task Initialize()
        {
            try
            {
                await GetFileItemInZip().ConfigureAwait(true);

                if (FileCollection.Count == 0)
                {
                    EmptyTip.Visibility = Visibility.Visible;
                }
            }
            catch (ZipException)
            {
                EmptyTip.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "此Zip文件无法被正确解析",
                        CloseButtonText = "返回"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "This Zip file cannot be parsed correctly",
                        CloseButtonText = "Back"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                FileControlInstance.Nav.GoBack();
            }
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (FileCollection.Count == 0)
            {
                EmptyTip.Visibility = Visibility.Visible;
                DecompressAll.IsEnabled = false;
                Crctest.IsEnabled = false;
            }
            else
            {
                DecompressAll.IsEnabled = true;
                Crctest.IsEnabled = true;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            FileCollection.Clear();
            FileCollection = null;
            OriginFile = null;
        }

        public async Task GetFileItemInZip()
        {
            using (Stream ZipFileStream = await OriginFile.File.OpenStreamForReadAsync().ConfigureAwait(true))
            using (ZipInputStream InputStream = new ZipInputStream(ZipFileStream))
            {
                while (InputStream.GetNextEntry() is ZipEntry Entry)
                {
                    if (Entry.IsFile && FileCollection.All((Item) => Item.FullName != Entry.Name))
                    {
                        FileCollection.Add(new ZipFileDisplay(Entry));
                    }
                }
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
            LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在执行删除操作" : "Deleting");

            ZipFileDisplay file = GridControl.SelectedItem as ZipFileDisplay;
            using (var ZipFileStream = (await OriginFile.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    if (zipFile.GetEntry(file.FullName) is ZipEntry Entry)
                    {
                        await Task.Run(() =>
                        {
                            zipFile.BeginUpdate();
                            zipFile.Delete(Entry);
                            zipFile.CommitUpdate();
                        }).ConfigureAwait(true);
                    }
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            await OriginFile.SizeUpdateRequested().ConfigureAwait(true);
            await Task.Delay(500).ConfigureAwait(true);

            FileCollection.Remove(file);
            LoadingActivation(false);
        }

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在检验文件" : "Verifying");

            var file = GridControl.SelectedItem as ZipFileDisplay;
            using (var ZipFileStream = await OriginFile.File.OpenStreamForReadAsync().ConfigureAwait(true))
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    bool IsCorrect = await Task.Run(() =>
                    {
                        return zipFile.TestArchive(true);
                    }).ConfigureAwait(true);

                    QueueContentDialog QueueContenDialog;
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContenDialog = new QueueContentDialog
                        {
                            Title = "测试结果",
                            Content = IsCorrect ? "CRC校验通过，Zip文件完整" : "未能通过CRC校验，Zip文件存在问题",
                            CloseButtonText = "确定"
                        };
                    }
                    else
                    {
                        QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Test Result",
                            Content = IsCorrect ? "The CRC is verified" : "Failed to pass CRC check",
                            CloseButtonText = "Confirm"
                        };
                    }
                    LoadingActivation(false);
                    await Task.Delay(500).ConfigureAwait(true);
                    await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
            }
            else
            {
                Delete.IsEnabled = true;
            }
        }

        private void LoadingActivation(bool IsLoading, string Info = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = Info + "...";
            }
            LoadingControl.IsLoading = IsLoading;
            MainPage.ThisPage.IsAnyTaskRunning = IsLoading;
        }

        private async void Decompression_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在解压" : "Extracting");

            ZipFileDisplay file = GridControl.SelectedItem as ZipFileDisplay;
            using (Stream ZipFileStream = await OriginFile.File.OpenStreamForReadAsync().ConfigureAwait(true))
            using (ZipInputStream InputStream = new ZipInputStream(ZipFileStream))
            {
                while (true)
                {
                    if (InputStream.GetNextEntry() is ZipEntry Entry && Entry.Name == file.FullName)
                    {
                        StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(OriginFile.DisplayName, CreationCollisionOption.OpenIfExists);

                        if (TabViewContainer.ThisPage.InstanceContainer[FileControlInstance].FileCollection.All((Item) => Item.Name != NewFolder.Name))
                        {
                            TabViewContainer.ThisPage.InstanceContainer[FileControlInstance].FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
                        }

                        if (FileControlInstance.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
                        {
                            if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                            {
                                Microsoft.UI.Xaml.Controls.TreeViewNode CurrentNode = new Microsoft.UI.Xaml.Controls.TreeViewNode
                                {
                                    Content = await FileControlInstance.CurrentFolder.GetFolderAsync(NewFolder.Name),
                                    HasUnrealizedChildren = false
                                };
                                FileControlInstance.CurrentNode.Children.Add(CurrentNode);
                            }
                            FileControlInstance.CurrentNode.IsExpanded = true;
                        }

                        StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.GenerateUniqueName);

                        using (Stream NewFileStream = await NewFile.OpenStreamForWriteAsync().ConfigureAwait(true))
                        {
                            await InputStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                        }

                        break;
                    }
                }
            }

            await Task.Delay(500).ConfigureAwait(true);

            LoadingActivation(false);
        }

        private async void DecompressAll_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在解压" : "Extracting");

            using (Stream ZipFileStream = await OriginFile.File.OpenStreamForReadAsync().ConfigureAwait(true))
            using (ZipInputStream InputStream = new ZipInputStream(ZipFileStream))
            {
                StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(OriginFile.DisplayName, CreationCollisionOption.OpenIfExists);

                if (TabViewContainer.ThisPage.InstanceContainer[FileControlInstance].FileCollection.All((Item) => Item.Name != NewFolder.Name))
                {
                    TabViewContainer.ThisPage.InstanceContainer[FileControlInstance].FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetSizeDescriptionAsync(), await NewFolder.GetThumbnailBitmapAsync(), await NewFolder.GetModifiedTimeAsync()));
                }

                if (FileControlInstance.CurrentNode.Children.All((Node) => (Node.Content as StorageFolder).Name != NewFolder.Name))
                {
                    if (FileControlInstance.CurrentNode.IsExpanded || !FileControlInstance.CurrentNode.HasChildren)
                    {
                        Microsoft.UI.Xaml.Controls.TreeViewNode CurrentNode = new Microsoft.UI.Xaml.Controls.TreeViewNode
                        {
                            Content = await FileControlInstance.CurrentFolder.GetFolderAsync(NewFolder.Name),
                            HasUnrealizedChildren = false
                        };
                        FileControlInstance.CurrentNode.Children.Add(CurrentNode);
                    }
                    FileControlInstance.CurrentNode.IsExpanded = true;
                }

                while (InputStream.GetNextEntry() is ZipEntry Entry)
                {
                    StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.GenerateUniqueName);

                    using (Stream NewFileStream = await NewFile.OpenStreamForWriteAsync().ConfigureAwait(true))
                    {
                        await InputStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                    }
                }
            }

            await Task.Delay(500).ConfigureAwait(true);

            LoadingActivation(false);
        }

        private async void AddNewFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            IReadOnlyList<StorageFile> AddList = await Picker.PickMultipleFilesAsync();

            if (AddList.Count != 0)
            {
                if (EmptyTip.Visibility == Visibility.Visible)
                {
                    EmptyTip.Visibility = Visibility.Collapsed;
                }

                LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在执行添加操作" : "Adding");

                using (Stream ZipFileStream = (await OriginFile.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                using (ZipOutputStream OutputStream = new ZipOutputStream(ZipFileStream))
                {
                    foreach (StorageFile ToAddFile in AddList)
                    {
                        using (Stream FileStream = await ToAddFile.OpenStreamForReadAsync().ConfigureAwait(true))
                        {
                            ZipEntry Entry = new ZipEntry(ToAddFile.Name)
                            {
                                DateTime = DateTime.Now,
                                Size = FileStream.Length
                            };

                            OutputStream.PutNextEntry(Entry);

                            await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                        }
                    }

                    await OutputStream.FlushAsync().ConfigureAwait(true);
                    OutputStream.Finish();
                }

                await GetFileItemInZip().ConfigureAwait(true);
                await OriginFile.SizeUpdateRequested().ConfigureAwait(true);

                await Task.Delay(500).ConfigureAwait(true);
                LoadingActivation(false);
            }
        }
    }
}

