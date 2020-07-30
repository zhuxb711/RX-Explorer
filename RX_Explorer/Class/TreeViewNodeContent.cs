using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent : INotifyPropertyChanged
    {
        private StorageFolder InnerFolder;

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

        private string InnerPath;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string Name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
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

        public void Update(StorageFolder Folder)
        {
            InnerFolder = Folder ?? throw new ArgumentNullException(nameof(Folder), "Argument could not be null");

            OnPropertyChanged(nameof(DisplayName));
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
