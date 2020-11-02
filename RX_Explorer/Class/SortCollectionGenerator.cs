using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class SortCollectionGenerator
    {
        private static readonly object Locker = new object();

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
            if (SortTarget == Class.SortTarget.OriginPath || SortTarget == Class.SortTarget.Path)
            {
                throw new NotSupportedException("SortTarget.Path and SortTarget.OriginPath is not allow in this method");
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

            SortIndicatorController.SetIndicatorStatus(this.SortTarget, this.SortDirection);
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
                case SortTarget.Path:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? new List<T>(FolderList.OrderBy((Item) => Item.Path).Concat(FileList.OrderBy((Item) => Item.SizeRaw)))
                            : new List<T>(FileList.OrderByDescending((Item) => Item.Path).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw)));
                    }
                default:
                    {
                        if (typeof(T) == typeof(RecycleStorageItem))
                        {
                            return TempDirection == SortDirection.Ascending
                                ? new List<T>(FolderList.Select((Item) => Item as RecycleStorageItem).OrderBy((Item) => Item.OriginPath).Concat(FileList.Select((Item) => Item as RecycleStorageItem).OrderBy((Item) => Item.OriginPath)).Select((Item) => Item as T))
                                : new List<T>(FolderList.Select((Item) => Item as RecycleStorageItem).OrderByDescending((Item) => Item.OriginPath).Concat(FileList.Select((Item) => Item as RecycleStorageItem).OrderByDescending((Item) => Item.OriginPath)).Select((Item) => Item as T));
                        }
                        else
                        {
                            return null;
                        }
                    }
            }
        }

        public int SearchInsertLocation<T>(ICollection<T> InputCollection, T SearchTarget) where T : FileSystemStorageItemBase
        {
            if (InputCollection == null)
            {
                throw new ArgumentNullException(nameof(InputCollection), "Argument could not be null");
            }

            if (SearchTarget == null)
            {
                throw new ArgumentNullException(nameof(SearchTarget), "Argument could not be null");
            }

            switch (SortTarget)
            {
                case SortTarget.Name:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.Ordinal) > 0);

                            if (SearchResult == default)
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                            }
                            else
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                                else
                                {
                                    return SearchResult.Index;
                                }
                            }
                        }
                        else
                        {
                            //未找到任何匹配的项目时，FirstOrDefault返回元组的默认值，而int的默认值刚好契合此处需要返回0的要求，因此无需像SortDirection.Ascending一样进行额外处理
                            int Index = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.Ordinal) < 0).Index;

                            if (SearchTarget.StorageType == StorageItemTypes.Folder)
                            {
                                Index += InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.File);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Type:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.Ordinal) > 0);

                            if (SearchResult == default)
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                            }
                            else
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                                else
                                {
                                    return SearchResult.Index;
                                }
                            }
                        }
                        else
                        {
                            //未找到任何匹配的项目时，FirstOrDefault返回元组的默认值，而int的默认值刚好契合此处需要返回0的要求，因此无需像SortDirection.Ascending一样进行额外处理
                            int Index = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.Ordinal) < 0).Index;

                            if (SearchTarget.StorageType == StorageItemTypes.Folder)
                            {
                                Index += InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.File);
                            }

                            return Index;
                        }
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) > 0);

                            if (SearchResult == default)
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                            }
                            else
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                                else
                                {
                                    return SearchResult.Index;
                                }
                            }
                        }
                        else
                        {
                            //未找到任何匹配的项目时，FirstOrDefault返回元组的默认值，而int的默认值刚好契合此处需要返回0的要求，因此无需像SortDirection.Ascending一样进行额外处理
                            int Index = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) < 0).Index;

                            if (SearchTarget.StorageType == StorageItemTypes.Folder)
                            {
                                Index += InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.File);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Size:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) > 0);

                            if (SearchResult == default)
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                            }
                            else
                            {
                                if (SearchTarget.StorageType == StorageItemTypes.File)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.Folder);
                                }
                                else
                                {
                                    return SearchResult.Index;
                                }
                            }
                        }
                        else
                        {
                            //未找到任何匹配的项目时，FirstOrDefault返回元组的默认值，而int的默认值刚好契合此处需要返回0的要求，因此无需像SortDirection.Ascending一样进行额外处理
                            int Index = InputCollection.Where((Item) => Item.StorageType == SearchTarget.StorageType).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) < 0).Index;

                            if (SearchTarget.StorageType == StorageItemTypes.Folder)
                            {
                                Index += InputCollection.Count((Item) => Item.StorageType == StorageItemTypes.File);
                            }

                            return Index;
                        }
                    }
                default:
                    {
                        return -1;
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
