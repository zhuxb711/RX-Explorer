using Microsoft.Toolkit.Deferred;
using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class SortedCollectionGenerator
    {
        public static event EventHandler<SortStateChangedEventArgs> SortConfigChanged;

        public static async Task SaveSortConfigOnPathAsync(string Path, SortTarget? Target = null, SortDirection? Direction = null)
        {
            if (Target is SortTarget.OriginPath or SortTarget.Path)
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

        public static async Task<IEnumerable<T>> GetSortedCollectionAsync<T>(IEnumerable<T> InputCollection, SortTarget Target, SortDirection Direction, SortStyle Style) where T : IStorageItemBaseProperties
        {
            switch (Style)
            {
                case SortStyle.None:
                    {
                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    return await InputCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return InputCollection.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });
                                }
                            case SortTarget.Type:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    if (Direction == SortDirection.Ascending)
                                    {
                                        foreach (IGrouping<string, T> Group in InputCollection.OrderBy((Item) => Item.Type).GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        foreach (IGrouping<string, T> Group in InputCollection.OrderByDescending((Item) => Item.Type).GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }

                                    return SortResult;
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.OrderBy((Item) => Item.ModifiedTime)
                                                        : InputCollection.OrderByDescending((Item) => Item.ModifiedTime);
                                }
                            case SortTarget.Size:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.OrderBy((Item) => Item.Size)
                                                        : InputCollection.OrderByDescending((Item) => Item.Size);
                                }
                            case SortTarget.Path:
                                {
                                    return InputCollection.OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction);
                                }
                            case SortTarget.OriginPath when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    return InputCollection.Cast<IRecycleStorageItem>()
                                                          .OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction)
                                                          .Cast<T>();
                                }
                            case SortTarget.RecycleDate when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<IRecycleStorageItem>()
                                                                         .OrderBy((Item) => Item.RecycleDate)
                                                                         .Cast<T>()
                                                        : InputCollection.Cast<IRecycleStorageItem>()
                                                                         .OrderByDescending((Item) => Item.RecycleDate)
                                                                         .Cast<T>();
                                }
                        }

                        break;
                    }
                case SortStyle.UseFileSystemStyle:
                    {
                        IEnumerable<T> FolderList = InputCollection.Where((It) => It is FileSystemStorageFolder);
                        IEnumerable<T> FileList = InputCollection.Where((It) => It is FileSystemStorageFile);

                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return FolderList.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });

                                    IEnumerable<T> SortedFileList = await FileList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return FileList.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });

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
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        foreach (IGrouping<string, T> Group in FolderList.OrderByDescending((Item) => Item.Type)
                                                                                         .Concat(FileList.OrderByDescending((Item) => Item.Type))
                                                                                         .GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
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
                                    IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return FolderList.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });

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
                            case SortTarget.OriginPath when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    return FolderList.Cast<IRecycleStorageItem>()
                                                     .OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction)
                                                     .Concat(FileList.Cast<IRecycleStorageItem>().OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction))
                                                     .Cast<T>();
                                }
                            case SortTarget.RecycleDate when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? FolderList.Cast<IRecycleStorageItem>()
                                                                    .OrderBy((Item) => Item.RecycleDate)
                                                                    .Concat(FileList.Cast<IRecycleStorageItem>().OrderBy((Item) => Item.RecycleDate))
                                                                    .Cast<T>()
                                                        : FileList.Cast<IRecycleStorageItem>()
                                                                    .OrderByDescending((Item) => Item.RecycleDate)
                                                                    .Concat(FolderList.Cast<IRecycleStorageItem>().OrderByDescending((Item) => Item.RecycleDate))
                                                                    .Cast<T>();
                                }
                        }

                        break;
                    }
            }

            throw new NotSupportedException();
        }

        public static async Task<int> SearchInsertLocationAsync<T>(ICollection<T> InputCollection, T SearchTarget, SortTarget Target, SortDirection Direction, SortStyle Style) where T : IStorageItemBaseProperties
        {
            if (InputCollection == null)
            {
                throw new ArgumentNullException(nameof(InputCollection), "Argument could not be null");
            }

            if (SearchTarget == null)
            {
                throw new ArgumentNullException(nameof(SearchTarget), "Argument could not be null");
            }

            switch (Style)
            {
                case SortStyle.None:
                    {
                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    IEnumerable<T> Collection = InputCollection.Append(SearchTarget);
                                    IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return Collection.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });

                                    return SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Path:
                                {
                                    return InputCollection.Append(SearchTarget).OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Type:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    if (Direction == SortDirection.Ascending)
                                    {
                                        foreach (IGrouping<string, T> Group in InputCollection.Append(SearchTarget).OrderBy((Item) => Item.Type).GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        foreach (IGrouping<string, T> Group in InputCollection.Append(SearchTarget).OrderByDescending((Item) => Item.Type).GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        return InputCollection.Append(SearchTarget).OrderBy((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                    else
                                    {
                                        return InputCollection.Append(SearchTarget).OrderByDescending((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                }
                            case SortTarget.Size:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        return InputCollection.Append(SearchTarget).OrderBy((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                    else
                                    {
                                        return InputCollection.Append(SearchTarget).OrderByDescending((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                }
                            case SortTarget.OriginPath when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    return InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.RecycleDate when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().OrderBy((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                    else
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().OrderByDescending((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                }
                        }

                        break;
                    }
                case SortStyle.UseFileSystemStyle:
                    {
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
                                    IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                    IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                    {
                                        if (PreviousTask.Exception is not null)
                                        {
                                            return Collection.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                        }

                                        return PreviousTask.Result;
                                    });

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
                                    int Index = FilteredCollection.Append(SearchTarget).OrderByFastStringSortAlgorithm((Item) => Item.Path, Direction).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        foreach (IGrouping<string, T> Group in InputCollectionCopy.Where((Item) => Item is FileSystemStorageFolder)
                                                                                                  .OrderByDescending((Item) => Item.Type)
                                                                                                  .Concat(InputCollectionCopy.Where((Item) => Item is FileSystemStorageFile).OrderByDescending((Item) => Item.Type))
                                                                                                  .GroupBy((Item) => Item.Type))
                                        {
                                            SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction).ContinueWith((PreviousTask) =>
                                            {
                                                if (PreviousTask.Exception is not null)
                                                {
                                                    return Group.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                                }

                                                return PreviousTask.Result;
                                            }));
                                        }
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.Append(SearchTarget).OrderBy((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget is FileSystemStorageFile)
                                        {
                                            Index += InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.Append(SearchTarget).OrderByDescending((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget is FileSystemStorageFolder)
                                        {
                                            Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                                        }

                                        return Index;
                                    }
                                }
                            case SortTarget.Size:
                                {
                                    if (SearchTarget is FileSystemStorageFolder)
                                    {
                                        IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                        IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending).ContinueWith((PreviousTask) =>
                                        {
                                            if (PreviousTask.Exception is not null)
                                            {
                                                return Collection.OrderByFastStringSortAlgorithm((Item) => Item.Name, Direction);
                                            }

                                            return PreviousTask.Result;
                                        });

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
                                            return FilteredCollection.Append(SearchTarget).OrderBy((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Append(SearchTarget).OrderByDescending((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
                                    }
                                }
                            case SortTarget.OriginPath when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    int Index = FilteredCollection.Cast<IRecycleStorageItem>().Append((IRecycleStorageItem)SearchTarget).OrderByFastStringSortAlgorithm((Item) => Item.OriginPath, Direction).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                            case SortTarget.RecycleDate when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().Append((IRecycleStorageItem)SearchTarget).OrderBy((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget is FileSystemStorageFile)
                                        {
                                            Index += InputCollection.Count((Item) => Item is FileSystemStorageFolder);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().Append((IRecycleStorageItem)SearchTarget).OrderByDescending((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget is FileSystemStorageFolder)
                                        {
                                            Index += InputCollection.Count((Item) => Item is FileSystemStorageFile);
                                        }

                                        return Index;
                                    }
                                }
                        }

                        break;
                    }
            }

            throw new NotSupportedException();
        }
    }
}
