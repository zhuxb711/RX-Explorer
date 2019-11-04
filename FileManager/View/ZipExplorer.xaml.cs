using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
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
            await GetFileItemInZip();
        }

        public async Task GetFileItemInZip()
        {
            using (var ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    foreach (ZipEntry Entry in zipFile)
                    {
                        if (!Entry.IsFile || FileCollection.Any((Item) => Item.FullName == Entry.Name))
                        {
                            continue;
                        }

                        FileCollection.Add(new ZipFileDisplay(Entry));
                    }
                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
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
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在执行删除操作"
                : "Deleting");
            var file = GridControl.SelectedItem as ZipFileDisplay;
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
                        });
                        FileCollection.Remove(file);
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
                    bool IsCorrect = await Task.Run(() =>
                    {
                        return zipFile.TestArchive(true);
                    });

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
        }

        private async Task SetSelectedNodeInTreeAsync(TreeViewNode Node)
        {
            if(!FileControl.ThisPage.CurrentNode.IsExpanded)
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


        private async void Decompression_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在解压"
                : "Extracting");

            var file = GridControl.SelectedItem as ZipFileDisplay;
            using (Stream ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    ZipEntry Entry = zipFile.GetEntry(file.FullName);
                    if (Entry != null)
                    {
                        TreeViewNode CurrentNode = null;

                        StorageFolder NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(OriginFile.DisplayName, CreationCollisionOption.OpenIfExists);
                        StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);

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

                        using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                        using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                        {
                            await Task.Run(() =>
                            {
                                StreamUtils.Copy(ZipTempStream, stream, new byte[4096]);
                            });
                        }

                        await Task.Delay(1000);

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

                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            LoadingActivation(false);
        }

        private async void DecompressAll_Click(object sender, RoutedEventArgs e)
        {
            LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
            ? "正在解压"
            : "Extracting");

            using (Stream ZipFileStream = await OriginFile.File.OpenStreamForReadAsync())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    TreeViewNode CurrentNode = null;
                    StorageFolder NewFolder = await FileControl.ThisPage.CurrentFolder.CreateFolderAsync(OriginFile.DisplayName, CreationCollisionOption.OpenIfExists);

                    foreach (ZipEntry Entry in zipFile)
                    {
                        StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);

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

                        using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                        using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                        {
                            await Task.Run(() =>
                            {
                                StreamUtils.Copy(ZipTempStream, stream, new byte[4096]);
                            });
                        }
                    }

                    await Task.Delay(1000);

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
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

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
            var AddList = await Picker.PickMultipleFilesAsync();

            if (AddList.Count != 0)
            {
                LoadingActivation(true, MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "正在执行添加操作"
                : "Adding");

                using (var ZipFileStream = (await OriginFile.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
                {
                    ZipFile zipFile = new ZipFile(ZipFileStream);
                    try
                    {
                        zipFile.BeginUpdate();

                        foreach (var ToAddFile in AddList)
                        {
                            using (var filestream = await ToAddFile.OpenStreamForReadAsync())
                            {
                                await Task.Run(() =>
                                {
                                    CustomStaticDataSource CSD = new CustomStaticDataSource();
                                    CSD.SetStream(filestream);
                                    zipFile.Add(CSD, ToAddFile.Name);
                                });
                            }
                        }

                        zipFile.CommitUpdate();
                    }
                    finally
                    {
                        zipFile.IsStreamOwner = false;
                        zipFile.Close();
                    }
                }

                await GetFileItemInZip();
                await OriginFile.SizeUpdateRequested();

                await Task.Delay(500);
                LoadingActivation(false);
            }
        }
    }
}

