using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IIndirectLaunchStorageItem
    {
        public Task<bool> LaunchAsync();
    }
}
