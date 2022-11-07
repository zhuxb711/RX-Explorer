using SharedLibrary;

namespace RX_Explorer.Interface
{
    public interface IUrlStorageFile : IUnsupportedStorageItem<UrlFileData>, IIndirectLaunchStorageItem, IStorageItemBaseProperties, IStorageItemOperation
    {
        public string UrlTargetPath { get; }
    }
}
