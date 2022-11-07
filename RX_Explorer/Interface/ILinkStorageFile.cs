using RX_Explorer.Class;
using SharedLibrary;
using System.Collections.Generic;

namespace RX_Explorer.Interface
{
    public interface ILinkStorageFile : IUnsupportedStorageItem<LinkFileData>, IIndirectLaunchStorageItem, IStorageItemBaseProperties, IStorageItemOperation
    {
        public string LinkTargetPath { get; }

        public IEnumerable<string> Arguments { get; }

        public string WorkDirectory { get; }

        public byte HotKey { get; }

        public bool NeedRunAsAdmin { get; }

        public string Comment { get; }

        public ShellLinkType LinkType { get; }

        public WindowState WindowState { get; }

    }
}
