using RX_Explorer.Class;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IUnsupportedStorageItem<T> : IUnsupportedStorageItem where T : class
    {
        public Task<T> GetRawDataAsync();

        //Use this overload to avoid create another FullTrustProcessController
        public Task<T> GetRawDataAsync(FullTrustProcessController Controller);
    }

    public interface IUnsupportedStorageItem : IStorageItemPropertiesBase, INotifyPropertyChanged
    {

    }
}
