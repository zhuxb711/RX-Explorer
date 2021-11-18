using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent : INotifyPropertyChanged
    {
        private StorageFolder InnerFolder;
        private string InnerPath;
        private readonly string DisplayNameOverride;

        public string DisplayName => DisplayNameOverride ?? InnerFolder?.DisplayName ?? System.IO.Path.GetFileName(Path);

        public string Path => InnerFolder?.Path ?? InnerPath;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public void ReplaceWithNewPath(string NewPath)
        {
            if (InnerPath != NewPath)
            {
                InnerFolder = null;
                InnerPath = NewPath;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public async Task<StorageFolder> GetStorageFolderAsync()
        {
            if (InnerFolder == null)
            {
                try
                {
                    return InnerFolder = await StorageFolder.GetFolderFromPathAsync(Path).AsTask().ConfigureAwait(false);
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return InnerFolder;
            }
        }

        public TreeViewNodeContent(StorageFolder Folder)
        {
            InnerFolder = Folder ?? throw new ArgumentNullException(nameof(Folder), "Argument could not be null");
        }

        public TreeViewNodeContent(string Path, string DisplayNameOverride = null)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Argument could not be null or empty");
            }

            InnerPath = Path;
            this.DisplayNameOverride = DisplayNameOverride;
        }
    }
}
