using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent
    {
        public static TreeViewNodeContent QuickAccessNode { get; } = new TreeViewNodeContent("QuickAccessPath", Globalization.GetString("QuickAccessDisplayName"));

        public BitmapImage Thumbnail { get; }

        public string Name { get; }

        public string Path { get; }

        public bool HasChildren { get; }

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

        public static Task<TreeViewNodeContent> CreateAsync(StorageFolder Folder)
        {
            return CreateAsync(new FileSystemStorageFolder(Folder));
        }

        public static async Task<TreeViewNodeContent> CreateAsync(FileSystemStorageFolder Folder)
        {
            BitmapImage Thubnail = null;

            if (SettingPage.ContentLoadMode == LoadMode.All || (System.IO.Path.GetPathRoot(Folder.Path)?.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault())
            {
                Thubnail = await Folder.GetThumbnailAsync(ThumbnailMode.ListView);
            }

            return new TreeViewNodeContent(Folder, Thubnail, await Folder.CheckContainsAnyItemAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, BasicFilters.Folder));
        }

        private TreeViewNodeContent(FileSystemStorageFolder InnerFolder, BitmapImage Thumbnail, bool HasChildren)
        {
            Path = InnerFolder.Path;
            Name = System.IO.Path.GetPathRoot(InnerFolder.Path) == InnerFolder.Path ? InnerFolder.DisplayName : InnerFolder.Name;

            this.HasChildren = HasChildren;

            if (Thumbnail != null)
            {
                this.Thumbnail = Thumbnail;
            }
            else
            {
                this.Thumbnail = new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
            }
        }

        private TreeViewNodeContent(string OverridePath, string OverrideDisplayName)
        {
            Path = OverridePath;
            Name = OverrideDisplayName;
            HasChildren = true;
            Thumbnail = new BitmapImage(OverridePath == "QuickAccessPath"
                                                        ? new Uri("ms-appx:///Assets/Favourite.png")
                                                        : WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
        }
    }
}
