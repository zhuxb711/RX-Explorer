using RX_Explorer.Class;
using ShareClassLibrary;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface ILinkStorageFile : IUnsupportedStorageItem
    {
        public ShellLinkType LinkType { get; }

        public string LinkTargetPath { get; }

        public string[] Arguments { get; }

        public bool NeedRunAsAdmin { get; }

        public Task<LinkDataPackage> GetLinkDataAsync();

        public Task LaunchAsync();
    }
}
