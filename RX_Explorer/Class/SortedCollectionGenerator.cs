using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class SortedCollectionGenerator
    {
        public static event EventHandler<SortStateChangedEventArgs> SortConfigChanged;

        public static void SaveSortConfigOnPath(string Path, SortTarget? Target = null, SortDirection? Direction = null)
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
                SortConfigChanged?.Invoke(null, new SortStateChangedEventArgs(Path, LocalTarget, LocalDirection));
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
                                    return await InputCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);
                                }
                            case SortTarget.Type:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    foreach (IGrouping<string, T> Group in await InputCollection.GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
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
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    foreach (IGrouping<string, T> Group in await InputCollection.GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult;
                                }
                            case SortTarget.OriginPath when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    IEnumerable<IRecycleStorageItem> SortResult = Enumerable.Empty<IRecycleStorageItem>();

                                    foreach (IGrouping<string, IRecycleStorageItem> Group in await InputCollection.Cast<IRecycleStorageItem>().GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Cast<T>();
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
                            case SortTarget.CompressedSize when InputCollection.All((Item) => Item is ICompressionItem):
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<ICompressionItem>()
                                                                         .OrderBy((Item) => Item.CompressedSize)
                                                                         .Cast<T>()
                                                        : InputCollection.Cast<ICompressionItem>()
                                                                         .OrderByDescending((Item) => Item.CompressedSize)
                                                                         .Cast<T>();
                                }
                            case SortTarget.CompressionRate when InputCollection.All((Item) => Item is ICompressionItem):
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<ICompressionItem>()
                                                                         .OrderBy((Item) => Item.CompressionRate)
                                                                         .Cast<T>()
                                                        : InputCollection.Cast<ICompressionItem>()
                                                                         .OrderByDescending((Item) => Item.CompressionRate)
                                                                         .Cast<T>();
                                }
                        }

                        break;
                    }
                case SortStyle.UseFileSystemStyle:
                    {
                        IEnumerable<T> FolderList = InputCollection.Where((Item) => Item.IsDirectory);
                        IEnumerable<T> FileList = InputCollection.Where((Item) => !Item.IsDirectory);

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

                                    foreach (IGrouping<string, T> Group in (await FolderList.GroupBy((Item) => Item.DisplayType)
                                                                                            .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                            .Concat(await FileList.GroupBy((Item) => Item.DisplayType)
                                                                                                                  .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))

                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
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
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    foreach (IGrouping<string, T> Group in (await FolderList.GroupBy((Item) => Path.GetDirectoryName(Item.Path))
                                                                                            .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                            .Concat(await FileList.GroupBy((Item) => Path.GetDirectoryName(Item.Path))
                                                                                                                  .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult;
                                }
                            case SortTarget.OriginPath when InputCollection.All((Item) => Item is IRecycleStorageItem):
                                {
                                    IEnumerable<IRecycleStorageItem> SortResult = Enumerable.Empty<IRecycleStorageItem>();

                                    foreach (IGrouping<string, IRecycleStorageItem> Group in (await FolderList.Cast<IRecycleStorageItem>()
                                                                                                              .GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath))
                                                                                                              .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                                              .Concat(await FileList.Cast<IRecycleStorageItem>()
                                                                                                                                    .GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath))
                                                                                                                                    .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Cast<T>();
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
                            case SortTarget.CompressedSize when InputCollection.All((Item) => Item is ICompressionItem):
                                {
                                    IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                                    return Direction == SortDirection.Ascending
                                                        ? SortedFolderList.Concat(FileList.Cast<ICompressionItem>()
                                                                                          .OrderBy((Item) => Item.CompressedSize)
                                                                                          .Cast<T>())
                                                        : FileList.Cast<ICompressionItem>()
                                                                  .OrderByDescending((Item) => Item.CompressedSize)
                                                                  .Cast<T>()
                                                                  .Concat(SortedFolderList);
                                }
                            case SortTarget.CompressionRate when InputCollection.All((Item) => Item is ICompressionItem):
                                {
                                    IEnumerable<T> SortedFolderList = await FolderList.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                                    return Direction == SortDirection.Ascending
                                                        ? SortedFolderList.Concat(FileList.Cast<ICompressionItem>()
                                                                                          .OrderBy((Item) => Item.CompressionRate)
                                                                                          .Cast<T>())
                                                        : FileList.Cast<ICompressionItem>()
                                                                  .OrderByDescending((Item) => Item.CompressionRate)
                                                                  .Cast<T>()
                                                                  .Concat(SortedFolderList);
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
                                    IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);

                                    return SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Path:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    foreach (IGrouping<string, T> Group in await InputCollection.Append(SearchTarget).GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Type:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();

                                    foreach (IGrouping<string, T> Group in await InputCollection.Append(SearchTarget).GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
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
                                    IEnumerable<IRecycleStorageItem> SortResult = Enumerable.Empty<IRecycleStorageItem>();

                                    foreach (IGrouping<string, IRecycleStorageItem> Group in await InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
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
                            case SortTarget.CompressedSize when InputCollection.Append(SearchTarget).All((Item) => Item is ICompressionItem):
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderBy((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                    else
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                }
                            case SortTarget.CompressionRate when InputCollection.Append(SearchTarget).All((Item) => Item is ICompressionItem):
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderBy((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                    else
                                    {
                                        return InputCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                    }
                                }
                        }

                        break;
                    }
                case SortStyle.UseFileSystemStyle:
                    {
                        IEnumerable<T> FilteredCollection = null;

                        if (SearchTarget.IsDirectory)
                        {
                            FilteredCollection = InputCollection.Where((Item) => Item.IsDirectory);
                        }
                        else
                        {
                            FilteredCollection = InputCollection.Where((Item) => !Item.IsDirectory);
                        }

                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                    IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction);

                                    int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                    if (Direction == SortDirection.Ascending)
                                    {
                                        if (!SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                    }
                                    else
                                    {
                                        if (SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }
                                    }

                                    return Index;
                                }
                            case SortTarget.Path:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();
                                    IEnumerable<T> InputCollectionCopy = InputCollection.Append(SearchTarget);

                                    foreach (IGrouping<string, T> Group in (await InputCollectionCopy.Where((Item) => Item.IsDirectory)
                                                                                                     .GroupBy((Item) => Path.GetDirectoryName(Item.Path))
                                                                                                     .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                                     .Concat(await InputCollectionCopy.Where((Item) => !Item.IsDirectory)
                                                                                                                                      .GroupBy((Item) => Path.GetDirectoryName(Item.Path))
                                                                                                                                      .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Type:
                                {
                                    IEnumerable<T> SortResult = Enumerable.Empty<T>();
                                    IEnumerable<T> InputCollectionCopy = InputCollection.Append(SearchTarget);

                                    foreach (IGrouping<string, T> Group in (await InputCollectionCopy.Where((Item) => Item.IsDirectory)
                                                                                                     .GroupBy((Item) => Item.DisplayType)
                                                                                                     .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                                     .Concat(await InputCollectionCopy.Where((Item) => !Item.IsDirectory)
                                                                                                                                      .GroupBy((Item) => Item.DisplayType)
                                                                                                                                      .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction));
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.Append(SearchTarget).OrderBy((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (!SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.Append(SearchTarget).OrderByDescending((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                }
                            case SortTarget.Size:
                                {
                                    if (SearchTarget.IsDirectory)
                                    {
                                        IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                        IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                                        int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (Direction == SortDirection.Descending)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        if (Direction == SortDirection.Ascending)
                                        {
                                            return FilteredCollection.Append(SearchTarget).OrderBy((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Append(SearchTarget).OrderByDescending((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
                                    }
                                }
                            case SortTarget.OriginPath when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    IEnumerable<IRecycleStorageItem> SortResult = Enumerable.Empty<IRecycleStorageItem>();
                                    IEnumerable<T> InputCollectionCopy = InputCollection.Append(SearchTarget);

                                    foreach (IGrouping<string, IRecycleStorageItem> Group in (await InputCollectionCopy.Where((Item) => Item.IsDirectory)
                                                                                                                       .Cast<IRecycleStorageItem>()
                                                                                                                       .GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath))
                                                                                                                       .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction))
                                                                                                                       .Concat(await InputCollectionCopy.Where((Item) => !Item.IsDirectory)
                                                                                                                                                        .Cast<IRecycleStorageItem>()
                                                                                                                                                        .GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath))
                                                                                                                                                        .OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))
                                    {
                                        SortResult = SortResult.Concat(await Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending));
                                    }

                                    return SortResult.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.RecycleDate when InputCollection.Append(SearchTarget).All((Item) => Item is IRecycleStorageItem):
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().Append((IRecycleStorageItem)SearchTarget).OrderBy((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (!SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().Append((IRecycleStorageItem)SearchTarget).OrderByDescending((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                }
                            case SortTarget.CompressedSize when InputCollection.Append(SearchTarget).All((Item) => Item is ICompressionItem):
                                {
                                    if (SearchTarget.IsDirectory)
                                    {
                                        IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                        IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                                        int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (Direction == SortDirection.Descending)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        if (Direction == SortDirection.Ascending)
                                        {
                                            return FilteredCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderBy((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
                                    }
                                }
                            case SortTarget.CompressionRate when InputCollection.Append(SearchTarget).All((Item) => Item is ICompressionItem):
                                {
                                    if (SearchTarget.IsDirectory)
                                    {
                                        IEnumerable<T> Collection = FilteredCollection.Append(SearchTarget);
                                        IEnumerable<T> SortedFilteredCollection = await Collection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending);

                                        int Index = SortedFilteredCollection.Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (Direction == SortDirection.Descending)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        if (Direction == SortDirection.Ascending)
                                        {
                                            return FilteredCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderBy((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Append(SearchTarget).Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
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
