using MorseCode.ITask;
using Windows.Storage;

namespace RX_Explorer.Interface
{
    public interface ICoreFileSystemStorageItem<out T> where T : IStorageItem
    {
        public string Path { get; }

        public ITask<T> GetStorageItemAsync(bool ForceUpdate = false);
    }
}
