using SharedLibrary;
using System;

namespace RX_Explorer.Interface
{
    public interface IUrlStorageFile : IUnsupportedStorageItem<UrlFileData>, IIndirectLaunchStorageItem
    {
        public string UrlTargetPath { get; }
    }
}
