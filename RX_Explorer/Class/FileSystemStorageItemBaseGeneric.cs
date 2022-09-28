using MorseCode.ITask;
using RX_Explorer.Interface;
using SharedLibrary;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public abstract class FileSystemStorageItemBase<T> : FileSystemStorageItemBase, ICoreFileSystemStorageItem<T> where T : IStorageItem
    {
        protected T StorageItem { get; private set; }

        protected abstract Task<T> GetStorageItemCoreAsync();

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            await GetStorageItemAsync(ForceUpdate);
        }

        public async ITask<T> GetStorageItemAsync(bool ForceUpdate = false)
        {
            if (!IsHiddenItem && !IsSystemItem)
            {
                if (StorageItem == null || ForceUpdate)
                {
                    StorageItem = await GetStorageItemCoreAsync();
                }
            }

            return StorageItem;
        }

        protected FileSystemStorageItemBase(NativeFileData Data) : this(Data?.Path)
        {
            if ((Data?.IsDataValid).GetValueOrDefault())
            {
                Size = Data.Size;
                StorageItem = (T)Data.StorageItem;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = Data.LastAccessTime;
            }
        }

        protected FileSystemStorageItemBase(MTPFileData Data) : this(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }

        protected FileSystemStorageItemBase(FtpFileData Data) : this(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }

        protected FileSystemStorageItemBase(string Path) : base(Path)
        {
            this.Path = Path;
        }
    }
}
