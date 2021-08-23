using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IRecycleStorageItem : IStorageItemPropertiesBase, INotifyPropertyChanged
    {
        public string OriginPath { get; }
        public string SizeDescription { get; }
        public string ModifiedTimeDescription { get; }
        public Task<bool> DeleteAsync();
        public Task<bool> RestoreAsync();
    }
}
