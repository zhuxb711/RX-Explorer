using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IRecycleStorageItem : IStorageItemPropertyBase, INotifyPropertyChanged
    {
        public string OriginPath { get; }
        public string Size { get; }
        public string ModifiedTime { get; }
        public Task<bool> DeleteAsync();
        public Task<bool> RestoreAsync();
    }
}
