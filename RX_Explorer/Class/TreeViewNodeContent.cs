using RX_Explorer.View;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent : INotifyPropertyChanged
    {
        public static TreeViewNodeContent QuickAccessNode { get; } = new TreeViewNodeContent("QuickAccessPath", Globalization.GetString("QuickAccessDisplayName"));

        public BitmapImage Thumbnail { get; private set; }

        public string Path { get; }

        public string DisplayName { get; private set; }

        public bool HasChildren { get; }

        private readonly FileSystemStorageFolder InnerFolder;

        public event PropertyChangedEventHandler PropertyChanged;

        private int IsContentLoaded;

        public async static Task<TreeViewNodeContent> CreateAsync(string Path)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFolder Folder)
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
            TreeViewNodeContent Content = new TreeViewNodeContent(Folder, await Folder.CheckContainsAnyItemAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, BasicFilters.Folder));

            if ((System.IO.Path.GetPathRoot(Folder.Path)?.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
            {
                Content.Thumbnail = await Folder.GetThumbnailAsync(ThumbnailMode.SingleItem);
            }

            return Content;
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    if (Thumbnail == null)
                    {
                        if (InnerFolder != null
                            && SettingPage.ContentLoadMode == LoadMode.All)
                        {
                            Thumbnail = await InnerFolder.GetThumbnailAsync(ThumbnailMode.ListView);
                        }

                        if (Thumbnail == null)
                        {
                            Thumbnail = new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                            ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                            : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
                        }
                    }

                    if (InnerFolder != null
                        && await InnerFolder.GetStorageItemAsync() is StorageFolder Folder)
                    {
                        DisplayName = Folder.DisplayName;
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

        private TreeViewNodeContent(FileSystemStorageFolder InnerFolder, bool HasChildren)
        {
            Path = InnerFolder.Path;
            DisplayName = InnerFolder.DisplayName;

            this.HasChildren = HasChildren;
            this.InnerFolder = InnerFolder;
        }

        private TreeViewNodeContent(string OverridePath, string OverrideDisplayName)
        {
            Path = OverridePath;
            DisplayName = OverrideDisplayName;

            HasChildren = true;

            if (OverridePath == "QuickAccessPath")
            {
                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/Favourite.png"));
            }
        }
    }
}
