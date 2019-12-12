using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager
{
    public sealed partial class SecureArea : Page
    {
        private IncrementalLoadingCollection<FileSystemStorageItem> SecureCollection;

        private StorageFolder SecureFolder;

        public SecureArea()
        {
            InitializeComponent();
            SecureCollection = new IncrementalLoadingCollection<FileSystemStorageItem>(GetMoreItemsFunction);
            SecureGridView.ItemsSource = SecureCollection;
            Loading += SecureArea_Loading;
            SecureCollection.CollectionChanged += SecureCollection_CollectionChanged;

            if ((bool)ApplicationData.Current.LocalSettings.Values["SecureAreaPreviousCheckResult"])
            {
                EmptyTips.Visibility = Visibility.Collapsed;
                SecureGridView.Visibility = Visibility.Visible;
            }
        }

        private async void SecureArea_Loading(FrameworkElement sender, object args)
        {
            Loading -= SecureArea_Loading;

            SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);

            QueryOptions Options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 100, ThumbnailOptions.ResizeThumbnail);
            Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

            StorageItemQueryResult ItemQuery = SecureFolder.CreateItemQueryWithOptions(Options);

            IReadOnlyList<IStorageItem> EncryptedFileList = await ItemQuery.GetItemsAsync(0, 100);

            foreach (var Item in EncryptedFileList)
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
                SecureCollection.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }

            await SecureCollection.SetStorageItemQueryAsync(ItemQuery);
        }

        private void SecureCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(SecureCollection.Count == 0)
            {
                EmptyTips.Visibility = Visibility.Visible;
            }
            else
            {
                EmptyTips.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<IEnumerable<FileSystemStorageItem>> GetMoreItemsFunction(uint Index, uint Num, StorageItemQueryResult Query)
        {
            List<FileSystemStorageItem> ItemList = new List<FileSystemStorageItem>();
            foreach (var Item in await Query.GetItemsAsync(Index, Num))
            {
                var Size = await Item.GetSizeDescriptionAsync();
                var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                var ModifiedTime = await Item.GetModifiedTimeAsync();
                ItemList.Add(new FileSystemStorageItem(Item, Size, Thumbnail, ModifiedTime));
            }
            return ItemList;
        }

        private async void AddFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            IReadOnlyList<StorageFile> FileList = await Picker.PickMultipleFilesAsync();

            foreach (var File in FileList)
            {
                if ((await File.EncryptionAsync(SecureFolder, "123456789", 128)) is StorageFile EncryptedFile)
                {
                    var Size = await EncryptedFile.GetSizeDescriptionAsync();
                    var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                    var ModifiedTime = await EncryptedFile.GetModifiedTimeAsync();
                    SecureCollection.Add(new FileSystemStorageItem(EncryptedFile, Size, Thumbnail, ModifiedTime));
                }
                else
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "加密文件时出现意外错误，导入过程已经终止",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An unexpected error occurred while encrypting the file, the import process has ended",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> Items = await e.DataView.GetStorageItemsAsync();

                foreach (StorageFile Item in Items.OfType<StorageFile>())
                {
                    if ((await Item.EncryptionAsync(SecureFolder, "123456789", 128)) is StorageFile EncryptedFile)
                    {
                        var Size = await EncryptedFile.GetSizeDescriptionAsync();
                        var Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                        var ModifiedTime = await EncryptedFile.GetModifiedTimeAsync();
                        SecureCollection.Add(new FileSystemStorageItem(EncryptedFile, Size, Thumbnail, ModifiedTime));
                    }
                    else
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "加密文件时出现意外错误，导入过程已经终止",
                                CloseButtonText = "确定"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "An unexpected error occurred while encrypting the file, the import process has ended",
                                CloseButtonText = "Got it"
                            };
                            _ = await Dialog.ShowAsync();
                        }
                    }
                }

                if (Items.Count((Item) => Item.IsOfType(StorageItemTypes.Folder)) != 0)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = "安全域不支持导入文件夹类型，所有文件夹类型均已被过滤",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "Security Area does not support importing folder, all folders have been filtered",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                }
            }
        }

        private void SecureGridView_DragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "松开即可添加文件";
        }

        private void SecureGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                DeleteFile.IsEnabled = true;
                ExportFile.IsEnabled = true;
            }
            else
            {
                DeleteFile.IsEnabled = false;
                ExportFile.IsEnabled = false;
            }
        }

        private void SecureGridView_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            SecureGridView.SelectedIndex = -1;
        }

        private async void DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                await Item.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                SecureCollection.Remove(Item);
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            if (SecureGridView.SelectedItem is FileSystemStorageItem Item)
            {
                FolderPicker Picker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    ViewMode = PickerViewMode.Thumbnail
                };
                Picker.FileTypeFilter.Add("*");

                if ((await Picker.PickSingleFolderAsync()) is StorageFolder Folder)
                {
                    try
                    {
                        _ = await Item.File.DecryptionAsync(Folder, "123456789");

                        await Item.File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        SecureCollection.Remove(Item);

                        _ = await Launcher.LaunchFolderAsync(Folder);
                    }
                    catch (Exception ex)
                    {
                        if (ex is PasswordErrorException)
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "由于解密密码错误，解密失败，导出任务已经终止\r\r这可能是由于待解密文件数据不匹配造成的",
                                    CloseButtonText = "确定"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "The decryption failed due to the wrong decryption password, the export task has been terminated \r \rThis may be caused by a mismatch in the data of the files to be decrypted",
                                    CloseButtonText = "Got it"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                        }
                        else if (ex is FileDamagedException)
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "由于待解密文件的内部结构损坏，解密失败，导出任务已经终止\r\r这可能是由于文件数据已损坏或被修改造成的",
                                    CloseButtonText = "确定"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Because the internal structure of the file to be decrypted is damaged and the decryption fails, the export task has been terminated \r \rThis may be caused by the file data being damaged or modified",
                                    CloseButtonText = "Got it"
                                };
                                _ = await Dialog.ShowAsync();
                            }
                        }
                    }
                }
            }
        }

        private void SecureGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
            {
                SecureGridView.SelectedItem = Item;
                SecureGridView.ContextFlyout = FileFlyout;
            }
            else
            {
                SecureGridView.SelectedItem = null;
                SecureGridView.ContextFlyout = EmptyFlyout;
            }
        }
    }
}
