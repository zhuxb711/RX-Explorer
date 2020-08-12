using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class SortCollectionGenerator
    {
        private readonly static object Locker = new object();

        private static SortCollectionGenerator Instance;

        public static SortCollectionGenerator Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new SortCollectionGenerator();
                }
            }
        }

        public SortTarget SortTarget { get; private set; }

        public SortDirection SortDirection { get; private set; }

        public void ModifySortWay(SortTarget? SortTarget = null, SortDirection? SortDirection = null)
        {
            if (SortTarget == Class.SortTarget.OriginPath)
            {
                throw new NotSupportedException("OriginPath is not allow in this class");
            }

            if (SortTarget.HasValue)
            {
                this.SortTarget = SortTarget.Value;
                ApplicationData.Current.LocalSettings.Values["CollectionSortTarget"] = Enum.GetName(typeof(SortTarget), SortTarget);
            }

            if (SortDirection.HasValue)
            {
                this.SortDirection = SortDirection.Value;
                ApplicationData.Current.LocalSettings.Values["CollectionSortDirection"] = Enum.GetName(typeof(SortDirection), SortDirection);
            }
        }

        public List<FileSystemStorageItem> GetSortedCollection(ICollection<FileSystemStorageItem> InputCollection)
        {
            IEnumerable<FileSystemStorageItem> FolderList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder);
            IEnumerable<FileSystemStorageItem> FileList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File);

            switch (SortTarget)
            {
                case SortTarget.Name:
                    {
                        return SortDirection == SortDirection.Ascending
                            ? new List<FileSystemStorageItem>(FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection)))
                            : new List<FileSystemStorageItem>(FileList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, SortDirection)));
                    }
                case SortTarget.Type:
                    {
                        return SortDirection == SortDirection.Ascending
                            ? new List<FileSystemStorageItem>(FolderList.OrderByLikeFileSystem((Item) => Item.Type, SortDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Type, SortDirection)))
                            : new List<FileSystemStorageItem>(FileList.OrderByLikeFileSystem((Item) => Item.Type, SortDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Type, SortDirection)));
                    }
                case SortTarget.ModifiedTime:
                    {
                        return SortDirection == SortDirection.Ascending
                            ? new List<FileSystemStorageItem>(FolderList.OrderBy((Item) => Item.ModifiedTimeRaw).Concat(FileList.OrderBy((Item) => Item.ModifiedTimeRaw)))
                            : new List<FileSystemStorageItem>(FileList.OrderByDescending((Item) => Item.ModifiedTimeRaw).Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTimeRaw)));
                    }
                case SortTarget.Size:
                    {
                        return SortDirection == SortDirection.Ascending
                            ? new List<FileSystemStorageItem>(FolderList.OrderBy((Item) => Item.SizeRaw).Concat(FileList.OrderBy((Item) => Item.SizeRaw)))
                            : new List<FileSystemStorageItem>(FileList.OrderByDescending((Item) => Item.SizeRaw).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw)));
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private SortCollectionGenerator()
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("CollectionSortTarget", out object Target))
            {
                SortTarget = (SortTarget)Enum.Parse(typeof(SortTarget), Convert.ToString(Target));
            }
            else
            {
                SortTarget = SortTarget.Name;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("CollectionSortDirection", out object Direction))
            {
                SortDirection = (SortDirection)Enum.Parse(typeof(SortDirection), Convert.ToString(Direction));
            }
            else
            {
                SortDirection = SortDirection.Ascending;
            }
        }
    }
}
