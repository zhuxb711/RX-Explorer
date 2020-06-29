
using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    public sealed class StorageAreaWatcher
    {
        private static StorageAreaWatcher Instance;

        private StorageItemQueryResult ItemQuery;

        public event EventHandler<List<FileSystemStorageItem>> AddContent;

        public event EventHandler<List<FileSystemStorageItem>> RemoveContent;

        private static readonly object Locker = new object();

        public static StorageAreaWatcher Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new StorageAreaWatcher();
                }
            }
        }

        public void SetCurrentLocation(StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
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
            
        }

        private StorageAreaWatcher()
        {

        }
    }
}
