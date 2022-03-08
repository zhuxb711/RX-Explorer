using RX_Explorer.Class;
using ShareClassLibrary;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface ILinkStorageFile : IUnsupportedStorageItem<LinkFileData>, IIndirectLaunchStorageItem
    {
        public ShellLinkType LinkType { get; }

        public string LinkTargetPath { get; }

        public string[] Arguments { get; }

        public string WorkDirectory { get; }

        public string Comment { get; }

        public WindowState WindowState { get; }
        
        public int HotKey { get; }

        public bool NeedRunAsAdmin { get; }
    }
}
