using System.Collections.Generic;
using System.ComponentModel;

namespace RX_Explorer.Class
{
    public class FileSystemStorageGroupItem : GroupItemBase<string, FileSystemStorageItemBase>
    {
        public override string Description
        {
            get
            {
                return $"({Count} {Globalization.GetString("Items_Description")})";
            }
        }

        public FileSystemStorageGroupItem(string Key, IEnumerable<FileSystemStorageItemBase> Items) : base(Key, Items)
        {

        }
    }
}
