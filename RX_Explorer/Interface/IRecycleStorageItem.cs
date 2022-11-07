using System;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IRecycleStorageItem : IStorageItemBaseProperties
    {
        public string OriginPath { get; }

        public DateTimeOffset RecycleDate { get; }

        public Task<bool> DeleteAsync();

        public Task<bool> RestoreAsync();
    }
}
