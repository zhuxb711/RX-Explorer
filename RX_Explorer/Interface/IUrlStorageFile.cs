using ShareClassLibrary;
using System;

namespace RX_Explorer.Interface
{
    public interface IUrlStorageFile : IUnsupportedStorageItem<UrlDataPackage>, IIndirectLaunchStorageItem
    {
        public string UrlTargetPath { get; }
    }
}
