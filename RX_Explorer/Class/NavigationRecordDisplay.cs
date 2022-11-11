using PropertyChanged;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class NavigationRecordDisplay
    {
        public string Path { get; }

        public string DisplayName { get; private set; }

        public BitmapImage Thumbnail { get; private set; }

        public Task LoadAsync()
        {
            return Execution.ExecuteOnceAsync(this, async () =>
            {
                try
                {
                    if (RootVirtualFolder.Current.DisplayName.Equals(DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/ThisPC.png"));
                    }
                    else
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFolder Folder)
                        {
                            if (await Folder.GetStorageItemAsync() is StorageFolder InnerFolder)
                            {
                                DisplayName = InnerFolder.DisplayName;
                            }

                            Thumbnail = await Folder.GetThumbnailAsync(ThumbnailMode.ListView);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the NavigationRecordDisplay on path: {Path}");
                }
                finally
                {
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayName));
                }
            });
        }

        public NavigationRecordDisplay(string Path)
        {
            this.Path = Path;

            if (RootVirtualFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
            {
                DisplayName = RootVirtualFolder.Current.DisplayName;
            }
            else
            {
                DisplayName = System.IO.Path.GetFileName(Path);

                if (string.IsNullOrEmpty(DisplayName))
                {
                    DisplayName = Path;
                }
            }
        }
    }
}
