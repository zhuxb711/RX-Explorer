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
            switch (SortTarget)
            {
                case SortTarget.Name:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.Name).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.Name).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.Name).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.Name).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }

                case SortTarget.Type:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.Type).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.Type).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.Type).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.Type).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.ModifiedTimeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.ModifiedTimeRaw).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.ModifiedTimeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.ModifiedTimeRaw).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                case SortTarget.Size:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.SizeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.SizeRaw).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.SizeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.SizeRaw).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                case SortTarget.OriginPath:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.RecycleItemOriginPath).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.RecycleItemOriginPath).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.RecycleItemOriginPath).ToList();
                            List<FileSystemStorageItem> FileSortList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.RecycleItemOriginPath).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
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
