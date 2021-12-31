using RX_Explorer.Class;
using Windows.Storage;

namespace RX_Explorer.Interface
{
    public interface ICoreStorageItem<T> where T : IStorageItem
    {
        public T StorageItem { get; }
    }
}
