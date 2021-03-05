using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class SortCollectionGenerator
    {
        private static readonly object Locker = new object();

        private static SortCollectionGenerator Instance;

        public event EventHandler<string> SortWayChanged;

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

        public async Task ModifySortWayAsync(string Path, SortTarget? SortTarget = null, SortDirection? SortDirection = null, bool BypassSaveAndNotification = false)
        {
            if (SortTarget == Class.SortTarget.OriginPath || SortTarget == Class.SortTarget.Path)
            {
                throw new NotSupportedException("SortTarget.Path and SortTarget.OriginPath is not allow in this method");
            }

            bool IsModified = false;

            if (SortTarget.HasValue && this.SortTarget != SortTarget)
            {
                this.SortTarget = SortTarget.Value;
                IsModified = true;
            }

            if (SortDirection.HasValue && this.SortDirection != SortDirection)
            {
                this.SortDirection = SortDirection.Value;
                IsModified = true;
            }

            if (IsModified && !BypassSaveAndNotification)
            {
                await SQLite.Current.SetPathConfiguration(new PathConfiguration(Path, this.SortTarget, this.SortDirection)).ConfigureAwait(true);

                SortWayChanged?.Invoke(this, Path);
            }
        }

        public IEnumerable<T> GetSortedCollection<T>(IEnumerable<T> InputCollection) where T : IStorageItemPropertyBase
        {
            return GetSortedCollection(InputCollection, null, null);
        }

        public IEnumerable<T> GetSortedCollection<T>(IEnumerable<T> InputCollection, SortTarget? Target, SortDirection? Direction) where T : IStorageItemPropertyBase
        {
            SortTarget TempTarget = Target ?? SortTarget;
            SortDirection TempDirection = Direction ?? SortDirection;

            IEnumerable<T> FolderList = InputCollection.Where((It) => It is FileSystemStorageFolder);
            IEnumerable<T> FileList = InputCollection.Where((It) => It is FileSystemStorageFile);

            switch (TempTarget)
            {
                case SortTarget.Name:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection))
                            : FileList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, TempDirection));
                    }
                case SortTarget.Type:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection))
                            : FileList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Type, TempDirection));
                    }
                case SortTarget.ModifiedTime:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.ModifiedTimeRaw).Concat(FileList.OrderBy((Item) => Item.ModifiedTimeRaw))
                            : FileList.OrderByDescending((Item) => Item.ModifiedTimeRaw).Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTimeRaw));
                    }
                case SortTarget.Size:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.SizeRaw).Concat(FileList.OrderBy((Item) => Item.SizeRaw))
                            : FileList.OrderByDescending((Item) => Item.SizeRaw).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw));
                    }
                case SortTarget.Path:
                    {
                        return TempDirection == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.Path).Concat(FileList.OrderBy((Item) => Item.SizeRaw))
                            : FileList.OrderByDescending((Item) => Item.Path).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw));
                    }
                default:
                    {
                        if (typeof(T) == typeof(IRecycleStorageItem))
                        {
                            return TempDirection == SortDirection.Ascending
                                ? FolderList.OfType<IRecycleStorageItem>().OrderBy((Item) => Item.OriginPath).Concat(FileList.OfType<IRecycleStorageItem>().OrderBy((Item) => Item.OriginPath)).OfType<T>()
                                : FolderList.OfType<IRecycleStorageItem>().OrderByDescending((Item) => Item.OriginPath).Concat(FileList.OfType<IRecycleStorageItem>().OrderByDescending((Item) => Item.OriginPath)).OfType<T>();
                        }
                        else
                        {
                            return null;
                        }
                    }
            }
        }

        public int SearchInsertLocation<T>(ICollection<T> InputCollection, T SearchTarget) where T : IStorageItemPropertyBase
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
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.OrdinalIgnoreCase) > 0);

                            if (SearchResult.Item == null)
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                }
                            }
                            else
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
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
                            int Index = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.OrdinalIgnoreCase) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Type:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.OrdinalIgnoreCase) > 0);

                            if (SearchResult.Item == null)
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                }
                            }
                            else
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
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
                            int Index = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.OrdinalIgnoreCase) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) > 0);

                            if (SearchResult.Item == null)
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                }
                            }
                            else
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
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
                            int Index = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Size:
                    {
                        if (SortDirection == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) > 0);

                            if (SearchResult.Item == null)
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                }
                            }
                            else
                            {
                                if (SearchTarget is FileSystemStorageFile)
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
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
                            int Index = InputCollection.Where((Item) => Item.GetType() == SearchTarget.GetType()).Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
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

        private SortCollectionGenerator()
        {
            SortTarget = SortTarget.Name;
            SortDirection = SortDirection.Ascending;
        }
    }
}
