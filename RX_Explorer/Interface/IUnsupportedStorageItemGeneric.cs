using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IUnsupportedStorageItem<T> : IUnsupportedStorageItem where T : class
    {
        public Task<T> GetRawDataAsync();
    }
}
