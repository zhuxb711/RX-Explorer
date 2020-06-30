using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher
    {
        private StorageItemQueryResult ItemQuery;

        public event EventHandler<List<FileSystemStorageItem>> AddContent;

        public event EventHandler<List<FileSystemStorageItem>> RemoveContent;

        private IEnumerable<FileSystemStorageItem> CurrentCollection;

        public void SetCurrentLocation(StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            if (CurrentCollection == null)
            {
                throw new InvalidOperationException("Excute Initialize() first");
            }

            QueryOptions Options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow
            };

            ItemQuery = Folder.CreateItemQueryWithOptions(Options);
            ItemQuery.ContentsChanged += ItemQuery_ContentsChanged;
        }

        private void ItemQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            List<FileSystemStorageItem> NewItems = WIN_Native_API.GetStorageItems(ItemQuery.Folder, ItemFilters.File | ItemFilters.Folder);

            List<FileSystemStorageItem> AddItems = NewItems.Except(CurrentCollection).ToList();
            if (AddItems.Count > 0)
            {
                AddContent?.Invoke(this, AddItems);
            }

            List<FileSystemStorageItem> RemoveItems = CurrentCollection.Except(NewItems).ToList();
            if (RemoveItems.Count > 0)
            {
                RemoveContent?.Invoke(this, RemoveItems);
            }
        }

        public StorageAreaWatcher(IEnumerable<FileSystemStorageItem> InitList)
        {
            CurrentCollection = InitList ?? throw new ArgumentNullException(nameof(InitList), "Parameter could not be null");
        }
    }
}
