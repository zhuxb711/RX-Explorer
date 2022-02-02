using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class AddressBlock : INotifyPropertyChanged
    {
        public string Path { get; }

        public string DisplayName { get; }

        public AddressBlockType BlockType { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetBlockType(AddressBlockType BlockType)
        {
            this.BlockType = BlockType;
            OnPropertyChanged(nameof(this.BlockType));
        }

        public static async Task<AddressBlock> CreateAsync(FileSystemStorageFolder Folder)
        {
            if (await Folder.GetStorageItemAsync() is StorageFolder InnerFolder)
            {
                return new AddressBlock(Folder.Path, InnerFolder.DisplayName);
            }

            return new AddressBlock(Folder.Path);
        }

        public static async Task<AddressBlock> CreateAsync(string Path, string OverrideDisplayName = null)
        {
            if (string.IsNullOrEmpty(OverrideDisplayName))
            {
                if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFolder Folder)
                {
                    if (await Folder.GetStorageItemAsync() is StorageFolder InnerFolder)
                    {
                        return new AddressBlock(Path, InnerFolder.DisplayName);
                    }
                }
            }

            return new AddressBlock(Path, OverrideDisplayName);
        }

        private AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;

            if (string.IsNullOrEmpty(DisplayName))
            {
                string FileName = System.IO.Path.GetFileName(Path);

                if (string.IsNullOrEmpty(FileName))
                {
                    this.DisplayName = Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                }
                else
                {
                    this.DisplayName = FileName;
                }
            }
            else
            {
                this.DisplayName = DisplayName;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
