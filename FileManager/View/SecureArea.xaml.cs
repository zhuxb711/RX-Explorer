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
        }

        private async void SecureArea_Loading(FrameworkElement sender, object args)
        {
            Loading -= SecureArea_Loading;

            SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);

            QueryOptions Options = new QueryOptions
            {
                FolderDepth = FolderDepth.Deep,
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
            EmptyTips.Visibility = SecureCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

                }
            }
        }

        private async void SecureGridView_Drop(object sender, DragEventArgs e)
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

                    }
                }

                if (Items.Count((Item) => Item.IsOfType(StorageItemTypes.Folder)) != 0)
                {

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
                    catch(PasswordErrorException)
                    {

                    }
                    catch (FileDamagedException)
                    {

                    }
                }
            }
        }
    }
}
