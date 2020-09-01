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

        public List<T> GetSortedCollection<T>(ICollection<T> InputCollection, SortTarget? Target, SortDirection? Direction) where T : FileSystemStorageItemBase
        {
            SortTarget TempTarget = Target ?? SortTarget;
            SortDirection TempDirection = Direction ?? SortDirection;

            IEnumerable<T> FolderList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.Folder);
            IEnumerable<T> FileList = InputCollection.Where((It) => It.StorageType == StorageItemTypes.File);

            switch (TempTarget)
            {
                case SortTarget.Name:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? new List<T>(FolderList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection)))
                            : new List<T>(FileList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection)));
                    }
                case SortTarget.Type:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? new List<T>(FolderList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection)))
                            : new List<T>(FileList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection)));
                    }
                case SortTarget.ModifiedTime:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? new List<T>(FolderList.OrderBy((Item) => Item.ModifiedTimeRaw).Concat(FileList.OrderBy((Item) => Item.ModifiedTimeRaw)))
                            : new List<T>(FileList.OrderByDescending((Item) => Item.ModifiedTimeRaw).Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTimeRaw)));
                    }
                case SortTarget.Size:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? new List<T>(FolderList.OrderBy((Item) => Item.SizeRaw).Concat(FileList.OrderBy((Item) => Item.SizeRaw)))
                            : new List<T>(FileList.OrderByDescending((Item) => Item.SizeRaw).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw)));
                    }
                default:
                    {
                        if(typeof(T) == typeof(RecycleStorageItem))
                        {
                            return TempDirection == SortDirection.Ascending
                                ? new List<T>(FolderList.Select((Item)=>Item as RecycleStorageItem).OrderBy((Item) => Item.OriginPath).Concat(FileList.Select((Item) => Item as RecycleStorageItem).OrderBy((Item) => Item.OriginPath)).Select((Item)=>Item as T))
                                : new List<T>(FolderList.Select((Item) => Item as RecycleStorageItem).OrderByDescending((Item) => Item.OriginPath).Concat(FileList.Select((Item) => Item as RecycleStorageItem).OrderByDescending((Item) => Item.OriginPath)).Select((Item) => Item as T));
                        }
                        else
                        {
                            return null;
                        }
                    }
            }
        }

        public List<T> GetSortedCollection<T>(ICollection<T> InputCollection) where T : FileSystemStorageItemBase
        {
            return GetSortedCollection(InputCollection, null, null);
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
