using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class SortCollectionGenerator
    {
        public static event EventHandler<SortWayChangedEventArgs> SortWayChanged;

        public static async Task SavePathSortWayAsync(string Path, SortTarget Target, SortDirection Direction)
        {
            if (Target == SortTarget.OriginPath || Target == SortTarget.Path)
            {
                throw new NotSupportedException("SortTarget.Path and SortTarget.OriginPath is not allowed in this method");
            }

            PathConfiguration CurrentConfiguration = await SQLite.Current.GetPathConfigurationAsync(Path);

            if (CurrentConfiguration.SortTarget != Target || CurrentConfiguration.SortDirection != Direction)
            {
                await SQLite.Current.SetPathConfigurationAsync(new PathConfiguration(Path, Target, Direction));
                SortWayChanged?.Invoke(null, new SortWayChangedEventArgs(Path, Target, Direction));
            }
        }

        public static IEnumerable<T> GetSortedCollection<T>(IEnumerable<T> InputCollection, SortTarget Target, SortDirection Direction) where T : IStorageItemPropertiesBase
        {
            IEnumerable<T> FolderList = InputCollection.Where((It) => It is FileSystemStorageFolder);
            IEnumerable<T> FileList = InputCollection.Where((It) => It is FileSystemStorageFile);

            switch (Target)
            {
                case SortTarget.Name:
                    {
                        return Direction == SortDirection.Ascending
                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Name, Direction).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Name, Direction))
                            : FileList.OrderByLikeFileSystem((Item) => Item.Name, Direction).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Name, Direction));
                    }
                case SortTarget.Type:
                    {
                        return Direction == SortDirection.Ascending
                            ? FolderList.OrderByLikeFileSystem((Item) => Item.Type, Direction).Concat(FileList.OrderByLikeFileSystem((Item) => Item.Type, Direction))
                            : FileList.OrderByLikeFileSystem((Item) => Item.Type, Direction).Concat(FolderList.OrderByLikeFileSystem((Item) => Item.Type, Direction));
                    }
                case SortTarget.ModifiedTime:
                    {
                        return Direction == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.ModifiedTimeRaw).Concat(FileList.OrderBy((Item) => Item.ModifiedTimeRaw))
                            : FileList.OrderByDescending((Item) => Item.ModifiedTimeRaw).Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTimeRaw));
                    }
                case SortTarget.Size:
                    {
                        return Direction == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.SizeRaw).Concat(FileList.OrderBy((Item) => Item.SizeRaw))
                            : FileList.OrderByDescending((Item) => Item.SizeRaw).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw));
                    }
                case SortTarget.Path:
                    {
                        return Direction == SortDirection.Ascending
                            ? FolderList.OrderBy((Item) => Item.Path).Concat(FileList.OrderBy((Item) => Item.SizeRaw))
                            : FileList.OrderByDescending((Item) => Item.Path).Concat(FolderList.OrderByDescending((Item) => Item.SizeRaw));
                    }
                default:
                    {
                        if (typeof(T) == typeof(IRecycleStorageItem))
                        {
                            return Direction == SortDirection.Ascending
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

        public static int SearchInsertLocation<T>(ICollection<T> InputCollection, T SearchTarget, SortTarget Target, SortDirection Direction) where T : IStorageItemPropertiesBase
        {
            if (InputCollection == null)
            {
                throw new ArgumentNullException(nameof(InputCollection), "Argument could not be null");
            }

            if (SearchTarget == null)
            {
                throw new ArgumentNullException(nameof(SearchTarget), "Argument could not be null");
            }

            IEnumerable<T> FilteredCollection = null;

            if (SearchTarget is FileSystemStorageFile)
            {
                FilteredCollection = InputCollection.Where((Item) => Item is FileSystemStorageFile);
            }
            else if (SearchTarget is FileSystemStorageFolder)
            {
                FilteredCollection = InputCollection.Where((Item) => Item is FileSystemStorageFolder);
            }
            else
            {
                return -1;
            }

            switch (Target)
            {
                case SortTarget.Name:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.OrdinalIgnoreCase) > 0);

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
                            int Index = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Name, SearchTarget.Name, StringComparison.OrdinalIgnoreCase) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Type:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.OrdinalIgnoreCase) > 0);

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
                            int Index = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => string.Compare(Value.Item.Type, SearchTarget.Type, StringComparison.OrdinalIgnoreCase) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) > 0);

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
                            int Index = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTimeRaw, SearchTarget.ModifiedTimeRaw) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Size:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) > 0);

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
                            int Index = FilteredCollection.Select((Item, Index) => (Index, Item)).FirstOrDefault((Value) => Value.Item.SizeRaw.CompareTo(SearchTarget.SizeRaw) < 0).Index;

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
    }

    public sealed class SortWayChangedEventArgs
    {
        public SortTarget Target { get; }

        public SortDirection Direction { get; }

        public string Path { get; }

        public SortWayChangedEventArgs(string Path, SortTarget Target, SortDirection Direction)
        {
            this.Path = Path;
            this.Target = Target;
            this.Direction = Direction;
        }
    }
}
