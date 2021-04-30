using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public class FileSystemStorageGroupItem : GroupItemBase<string, FileSystemStorageItemBase>
    {
        public FileSystemStorageGroupItem(string Key, IEnumerable<FileSystemStorageItemBase> Items) : base(Key, Items)
        {

        }
    }
}
