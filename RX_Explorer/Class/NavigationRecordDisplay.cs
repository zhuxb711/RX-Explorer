using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class NavigationRecordDisplay : INotifyPropertyChanged
    {
        public string Path { get; }

        public string DisplayName { get; private set; }

        public BitmapImage Thumbnail { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private int IsContentLoaded;

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    if (RootStorageFolder.Instance.DisplayName.Equals(DisplayName, StringComparison.OrdinalIgnoreCase))
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

                            if (Thumbnail == null)
                            {
                                Thumbnail = new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
                            }
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
            }
        }

        public NavigationRecordDisplay(string Path)
        {
            this.Path = Path;

            if (RootStorageFolder.Instance.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
            {
                DisplayName = RootStorageFolder.Instance.DisplayName;
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

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
