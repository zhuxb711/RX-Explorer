using ShareClassLibrary;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IHiddenStorageItem: IUnsupportedStorageItem
    {
        public Task<HiddenDataPackage> GetHiddenDataAsync();
    }
}
