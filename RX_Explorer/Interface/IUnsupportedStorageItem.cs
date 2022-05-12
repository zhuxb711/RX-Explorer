using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IUnsupportedStorageItem<T> where T : class
    {
        public Task<T> GetRawDataAsync();
    }
}
