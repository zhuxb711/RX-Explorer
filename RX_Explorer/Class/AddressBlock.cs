using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class AddressBlock : INotifyPropertyChanged
    {
        public string Path { get; }

        public string DisplayName { get; private set; }

        public AddressBlockType BlockType { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private int IsContentLoaded;

        public void SetBlockType(AddressBlockType BlockType)
        {
            this.BlockType = BlockType;
            OnPropertyChanged(nameof(this.BlockType));
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    if (!RootStorageFolder.Instance.DisplayName.Equals(DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFolder Folder)
                        {
                            if (await Folder.GetStorageItemAsync() is StorageFolder InnerFolder)
                            {
                                DisplayName = InnerFolder.DisplayName;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the AddressBlock on path: {Path}");
                }
                finally
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;
            this.DisplayName = DisplayName ?? System.IO.Path.GetFileName(Path);

            if (string.IsNullOrEmpty(this.DisplayName))
            {
                this.DisplayName = Path;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
