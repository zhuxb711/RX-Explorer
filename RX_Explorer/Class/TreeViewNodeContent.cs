using RX_Explorer.View;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent : INotifyPropertyChanged
    {
        public static TreeViewNodeContent QuickAccessNode { get; }

        public string Path { get; }

        public bool HasChildren { get; private set; }

        public string DisplayName { get; private set; }

        public BitmapImage Thumbnail
        {
            get
            {
                if (Path == "QuickAccessPath")
                {
                    return new BitmapImage(new Uri("ms-appx:///Assets/Favourite.png"));
                }
                else
                {
                    return thumbnail;
                }
            }
            private set
            {
                thumbnail = value;
            }
        }

        private int IsContentLoaded;
        private BitmapImage thumbnail;
        private readonly FileSystemStorageFolder InnerFolder;

        public event PropertyChangedEventHandler PropertyChanged;


        public async static Task<TreeViewNodeContent> CreateAsync(string Path)
        {
            if (LabelCollectionVirtualFolder.TryGetFolderFromPath(Path, out LabelCollectionVirtualFolder LabelFolder))
            {
                return await CreateAsync(LabelFolder);
            }
            else if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFolder Folder)
            {
                return await CreateAsync(Folder);
            }
            else
            {
                throw new DirectoryNotFoundException();
            }
        }

        public static async Task<TreeViewNodeContent> CreateAsync(FileSystemStorageFolder Folder)
        {
            return new TreeViewNodeContent(Folder)
            {
                HasChildren = Folder is not LabelCollectionVirtualFolder && await Folder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled, false, Filter: BasicFilters.Folder).AnyAsync()
            };
        }

        public async Task LoadAsync(bool ForceUpdate = false)
        {
            if (ForceUpdate || Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    if (InnerFolder != null)
                    {
                        if ((System.IO.Path.GetPathRoot(InnerFolder.Path)?.Equals(InnerFolder.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
                        {
                            if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.DriveFolder == InnerFolder) is DriveDataBase Drive)
                            {
                                Thumbnail = await Drive.GetThumbnailAsync();
                            }
                            else
                            {
                                Thumbnail = await InnerFolder.GetThumbnailAsync(ThumbnailMode.SingleItem);
                            }
                        }
                        else
                        {
                            Thumbnail = await InnerFolder.GetThumbnailAsync(ThumbnailMode.ListView);
                        }

                        if (InnerFolder is MTPStorageFolder MTPFolder)
                        {
                            if (MTPFolder.Path.TrimEnd('\\').Equals(MTPFolder.DeviceId, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    if (await Task.Run(() => StorageDevice.FromId(InnerFolder.Path)) is StorageFolder Item)
                                    {
                                        DisplayName = Item.DisplayName;
                                    }
                                }
                                catch (Exception)
                                {
                                    //No need to handle this exception
                                }
                            }
                        }
                        else if (await InnerFolder.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            DisplayName = Folder.DisplayName;
                        }
                        else
                        {
                            DisplayName = InnerFolder.DisplayName;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the TreeViewNodeContent on path: {Path}");
                }
                finally
                {
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private TreeViewNodeContent(FileSystemStorageFolder InnerFolder) : this(InnerFolder.Path, InnerFolder.DisplayName)
        {
            this.InnerFolder = InnerFolder;
        }

        private TreeViewNodeContent(string Path, string DisplayName)
        {
            this.Path = Path;
            this.DisplayName = DisplayName;
        }

        static TreeViewNodeContent()
        {
            QuickAccessNode = new TreeViewNodeContent("QuickAccessPath", Globalization.GetString("QuickAccessDisplayName")) { HasChildren = true };
        }
    }
}
