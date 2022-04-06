using Microsoft.Toolkit.Deferred;
using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class SortCollectionGenerator
    {
        public static event EventHandler<SortStateChangedEventArgs> SortConfigChanged;

        public static async Task SaveSortConfigOnPathAsync(string Path, SortTarget? Target = null, SortDirection? Direction = null)
        {
            if (Target == SortTarget.OriginPath || Target == SortTarget.Path)
            {
                throw new NotSupportedException("SortTarget.Path and SortTarget.OriginPath is not allowed in this method");
            }

            PathConfiguration CurrentConfig = SQLite.Current.GetPathConfiguration(Path);

            SortTarget LocalTarget = Target ?? CurrentConfig.SortTarget.GetValueOrDefault();
            SortDirection LocalDirection = Direction ?? CurrentConfig.SortDirection.GetValueOrDefault();

            if (CurrentConfig.SortTarget != LocalTarget || CurrentConfig.SortDirection != LocalDirection)
            {
                SQLite.Current.SetPathConfiguration(new PathConfiguration(Path, LocalTarget, LocalDirection));

                if (SortConfigChanged != null)
                {
                    await SortConfigChanged.InvokeAsync(null, new SortStateChangedEventArgs(Path, LocalTarget, LocalDirection));
                }
            }
        }

        public static async Task<IEnumerable<T>> GetSortedCollectionAsync<T>(IEnumerable<T> InputCollection, SortTarget Target, SortDirection Direction) where T : IStorageItemPropertiesBase
        {
            IEnumerable<T> FolderList = InputCollection.Where((It) => It is FileSystemStorageFolder);
            IEnumerable<T> FileList = InputCollection.Where((It) => It is FileSystemStorageFile);

            switch (Target)
            {
                case SortTarget.Name:
                    {
                        IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);
                        IEnumerable<T> SortedFileList = await FileList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);

                        return Direction == SortDirection.Ascending
                                            ? SortedFolderList.Concat(SortedFileList)
                                            : SortedFileList.Concat(SortedFolderList);
                    }
                case SortTarget.Type:
                    {
                        IEnumerable<T> SortResult = Enumerable.Empty<T>();

                        if (Direction == SortDirection.Ascending)
                        {
                            foreach (IGrouping<string, T> Group in FolderList.OrderBy((Item) => Item.Type)
                                                                             .Concat(FileList.OrderBy((Item) => Item.Type))
                                                                             .GroupBy((Item) => Item.Type))
                            {
                                SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
                            }
                        }
                        else
                        {
                            foreach (IGrouping<string, T> Group in FolderList.OrderByDescending((Item) => Item.Type)
                                                                             .Concat(FileList.OrderByDescending((Item) => Item.Type))
                                                                             .GroupBy((Item) => Item.Type))
                            {
                                SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
                            }
                        }

                        return SortResult;
                    }
                case SortTarget.ModifiedTime:
                    {
                        return Direction == SortDirection.Ascending
                                            ? FolderList.OrderBy((Item) => Item.ModifiedTime)
                                                        .Concat(FileList.OrderBy((Item) => Item.ModifiedTime))
                                            : FileList.OrderByDescending((Item) => Item.ModifiedTime)
                                                      .Concat(FolderList.OrderByDescending((Item) => Item.ModifiedTime));
                    }
                case SortTarget.Size:
                    {
                        IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                        return Direction == SortDirection.Ascending
                                            ? SortedFolderList.Concat(FileList.OrderBy((Item) => Item.Size))
                                            : FileList.OrderByDescending((Item) => Item.Size).Concat(SortedFolderList);
                    }
                case SortTarget.Path:
                    {
                        IEnumerable<T> SortedFolderList = FolderList.OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction);
                        IEnumerable<T> SortedFileList = FileList.OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction);

                        return Direction == SortDirection.Ascending
                                            ? SortedFolderList.Concat(SortedFileList)
                                            : SortedFileList.Concat(SortedFolderList);
                    }
                default:
                    {
                        if (typeof(T) == typeof(IRecycleStorageItem))
                        {
                            return FolderList.OfType<IRecycleStorageItem>()
                                             .OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction)
                                             .Concat(FileList.OfType<IRecycleStorageItem>().OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction))
                                             .OfType<T>();
                        }
                        else
                        {
                            return null;
                        }
                    }
            }
        }

        public static async Task<int> SearchInsertLocationAsync<T>(ICollection<T> InputCollection, T SearchTarget, SortTarget Target, SortDirection Direction) where T : IStorageItemPropertiesBase
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
                        IEnumerable<T> SortedFilteredCollection = await FilteredCollection.Append(SearchTarget).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);

                        int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                        if (Direction == SortDirection.Ascending)
                        {
                            if (SearchTarget is FileSystemStorageFile)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                            }
                        }
                        else
                        {
                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }
                        }

                        return Index;
                    }
                case SortTarget.Path:
                    {
                        IEnumerable<T> SortedFilteredCollection = FilteredCollection.Append(SearchTarget).OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction);

                        int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                        if (Direction == SortDirection.Ascending)
                        {
                            if (SearchTarget is FileSystemStorageFile)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                            }
                        }
                        else
                        {
                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }
                        }

                        return Index;
                    }
                case SortTarget.Type:
                    {
                        IEnumerable<T> SortResult = Enumerable.Empty<T>();
                        IEnumerable<T> InputCollectionCopy = InputCollection.Append(SearchTarget);

                        if (Direction == SortDirection.Ascending)
                        {
                            foreach (IGrouping<string, T> Group in InputCollectionCopy.Where((Item) => Item is FileSystemStorageFolder)
                                                                                      .OrderBy((Item) => Item.Type)
                                                                                      .Concat(InputCollectionCopy.Where((Item) => Item is FileSystemStorageFile).OrderBy((Item) => Item.Type))
                                                                                      .GroupBy((Item) => Item.Type))
                            {
                                SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
                            }
                        }
                        else
                        {
                            foreach (IGrouping<string, T> Group in InputCollectionCopy.Where((Item) => Item is FileSystemStorageFolder)
                                                                                      .OrderByDescending((Item) => Item.Type)
                                                                                      .Concat(InputCollectionCopy.Where((Item) => Item is FileSystemStorageFile).OrderByDescending((Item) => Item.Type))
                                                                                      .GroupBy((Item) => Item.Type))
                            {
                                SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
                            }
                        }

                        return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item))
                                                                                 .FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTime, SearchTarget.ModifiedTime) > 0);

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
                            int Index = FilteredCollection.Select((Item, Index) => (Index, Item))
                                                          .FirstOrDefault((Value) => DateTimeOffset.Compare(Value.Item.ModifiedTime, SearchTarget.ModifiedTime) < 0).Index;

                            if (SearchTarget is FileSystemStorageFolder)
                            {
                                return Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                    }
                case SortTarget.Size:
                    {
                        if (SearchTarget is FileSystemStorageFolder)
                        {
                            IEnumerable<T> SortedFilteredCollection = await FilteredCollection.Append(SearchTarget).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                            int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                            if (Direction == SortDirection.Descending)
                            {
                                Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                            }

                            return Index;
                        }
                        else
                        {
                            if (Direction == SortDirection.Ascending)
                            {
                                (int Index, T Item) SearchResult = FilteredCollection.Select((Item, Index) => (Index, Item))
                                                                                     .FirstOrDefault((Value) => Value.Item.Size.CompareTo(SearchTarget.Size) > 0);

                                if (SearchResult.Item == null)
                                {
                                    return InputCollection.Count;
                                }
                                else
                                {
                                    return SearchResult.Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                }
                            }
                            else
                            {
                                return FilteredCollection.Select((Item, Index) => (Index, Item))
                                                         .FirstOrDefault((Value) => Value.Item.Size.CompareTo(SearchTarget.Size) < 0).Index;
                            }
                        }
                    }
                default:
                    {
                        return -1;
                    }
            }
        }
    }
}
