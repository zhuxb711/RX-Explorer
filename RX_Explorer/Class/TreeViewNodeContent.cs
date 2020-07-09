using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class TreeViewNodeContent
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

        public async Task<StorageFolder> GetStorageFolderAsync()
        {
            if (InnerFolder == null)
            {
                try
                {
                    return InnerFolder = await StorageFolder.GetFolderFromPathAsync(Path);
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
