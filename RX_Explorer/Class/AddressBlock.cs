using PropertyChanged;
using RX_Explorer.Interface;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class AddressBlock
    {
        public string Path { get; }

        public string DisplayName { get; private set; }

        public AddressBlockType BlockType { get; set; }

        public Task LoadAsync()
        {
            return Execution.ExecuteOnceAsync(this, async () =>
            {
                try
                {
                    if (!RootVirtualFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
                    {
                        switch (await FileSystemStorageItemBase.OpenAsync(Path))
                        {
                            case FileSystemStorageFolder Folder when Folder is not INotWin32StorageFolder:
                                {
                                    if (await Folder.GetStorageItemAsync() is StorageFolder InnerFolder)
                                    {
                                        DisplayName = InnerFolder.DisplayName;
                                    }

                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the AddressBlock on path: {Path}");
                }
            });
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
    }
}
