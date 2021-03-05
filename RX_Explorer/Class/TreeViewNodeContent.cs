using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent : INotifyPropertyChanged
    {
        private StorageFolder InnerFolder;
        private string InnerPath;

        public string DisplayName
        {
            get
            {
                return InnerFolder != null ? InnerFolder.DisplayName : System.IO.Path.GetFileName(Path);
            }
        }

        public string Path
        {
            get
            {
                return InnerFolder != null ? InnerFolder.Path : InnerPath;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string Name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
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

        public TreeViewNodeContent(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Argument could not be null or empty");
            }

            InnerPath = Path;
        }
    }
}
