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
                                    return (await Task.WhenAll((await InputCollection.GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group);
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
                                    return (await Task.WhenAll((await InputCollection.GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)))).SelectMany((Group) => Group);
                                }
                            case SortTarget.OriginPath:
                                {
                                    return (await Task.WhenAll((await InputCollection.Cast<IRecycleStorageItem>().GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)))).SelectMany((Group) => Group).Cast<T>();
                                }
                            case SortTarget.RecycleDate:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<IRecycleStorageItem>()
                                                                         .OrderBy((Item) => Item.RecycleDate)
                                                                         .Cast<T>()
                                                        : InputCollection.Cast<IRecycleStorageItem>()
                                                                         .OrderByDescending((Item) => Item.RecycleDate)
                                                                         .Cast<T>();
                                }
                            case SortTarget.CompressedSize:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<ICompressionItem>()
                                                                         .OrderBy((Item) => Item.CompressedSize)
                                                                         .Cast<T>()
                                                        : InputCollection.Cast<ICompressionItem>()
                                                                         .OrderByDescending((Item) => Item.CompressedSize)
                                                                         .Cast<T>();
                                }
                            case SortTarget.CompressionRate:
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
                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? (await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group)
                                                        : (await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group);
                                }
                            case SortTarget.Type:
                                {
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group);
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).SelectMany((Group) => Group.OrderBy((Item) => Item.ModifiedTime))
                                                        : InputCollection.GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).SelectMany((Group) => Group.OrderByDescending((Item) => Item.ModifiedTime));
                                }
                            case SortTarget.Size:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? (await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<T>>(Group.OrderBy((Item) => Item.Size))))).SelectMany((Group) => Group)
                                                        : (await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<T>>(Group.OrderByDescending((Item) => Item.Size))))).SelectMany((Group) => Group);
                                }
                            case SortTarget.Path:
                                {
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group);
                                }
                            case SortTarget.OriginPath:
                                {
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.Cast<IRecycleStorageItem>().GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group).Cast<T>();
                                }
                            case SortTarget.RecycleDate:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? InputCollection.Cast<IRecycleStorageItem>().GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).SelectMany((Group) => Group.OrderBy((Item) => Item.RecycleDate)).Cast<T>()
                                                        : InputCollection.Cast<IRecycleStorageItem>().GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).SelectMany((Group) => Group.OrderByDescending((Item) => Item.RecycleDate)).Cast<T>();
                                }
                            case SortTarget.CompressedSize:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? (await Task.WhenAll(InputCollection.Cast<ICompressionItem>().GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<ICompressionItem>>(Group.OrderBy((Item) => Item.CompressedSize))))).SelectMany((Group) => Group).Cast<T>()
                                                        : (await Task.WhenAll(InputCollection.Cast<ICompressionItem>().GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<ICompressionItem>>(Group.OrderByDescending((Item) => Item.CompressedSize))))).SelectMany((Group) => Group).Cast<T>();
                                }
                            case SortTarget.CompressionRate:
                                {
                                    return Direction == SortDirection.Ascending
                                                        ? (await Task.WhenAll(InputCollection.Cast<ICompressionItem>().GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<ICompressionItem>>(Group.OrderBy((Item) => Item.CompressionRate))))).SelectMany((Group) => Group).Cast<T>()
                                                        : (await Task.WhenAll(InputCollection.Cast<ICompressionItem>().GroupBy((Item) => Item.IsDirectory).OrderBy((Group) => Group.Key).Select((Group) => Group.Key ? Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending) : Task.FromResult<IEnumerable<ICompressionItem>>(Group.OrderByDescending((Item) => Item.CompressionRate))))).SelectMany((Group) => Group).Cast<T>();
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
                                    return (await InputCollection.Append(SearchTarget).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Path:
                                {
                                    return (await Task.WhenAll((await InputCollection.Append(SearchTarget).GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Type:
                                {
                                    return (await Task.WhenAll((await InputCollection.Append(SearchTarget).GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
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
                            case SortTarget.OriginPath:
                                {
                                    return (await Task.WhenAll((await InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.RecycleDate:
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
                            case SortTarget.CompressedSize:
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
                            case SortTarget.CompressionRate:
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
                        IEnumerable<T> FilteredCollection = SearchTarget.IsDirectory ? InputCollection.Where((Item) => Item.IsDirectory).Append(SearchTarget) : InputCollection.Where((Item) => !Item.IsDirectory).Append(SearchTarget);

                        switch (Target)
                        {
                            case SortTarget.Name:
                                {
                                    int Index = (await FilteredCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.Append(SearchTarget).GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Path.GetDirectoryName(Item.Path)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.Type:
                                {
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.Append(SearchTarget).GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Item.DisplayType).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.ModifiedTime:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.OrderBy((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (!SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.OrderByDescending((Item) => Item.ModifiedTime).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                        int Index = (await FilteredCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                            return FilteredCollection.OrderBy((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.OrderByDescending((Item) => Item.Size).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
                                    }
                                }
                            case SortTarget.OriginPath:
                                {
                                    return (await Task.WhenAll((await Task.WhenAll(InputCollection.Append(SearchTarget).Cast<IRecycleStorageItem>().GroupBy((Item) => Item.IsDirectory).OrderByDescending((Group) => Group.Key).Select((Group) => Group.GroupBy((Item) => Path.GetDirectoryName(Item.OriginPath)).OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Key, Direction)))).SelectMany((Group) => Group).Select((Group) => Group.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, Direction)))).SelectMany((Group) => Group).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                }
                            case SortTarget.RecycleDate:
                                {
                                    if (Direction == SortDirection.Ascending)
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().OrderBy((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (!SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                    else
                                    {
                                        int Index = FilteredCollection.Cast<IRecycleStorageItem>().OrderByDescending((Item) => Item.RecycleDate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

                                        if (SearchTarget.IsDirectory)
                                        {
                                            Index += InputCollection.Count((Item) => !Item.IsDirectory);
                                        }

                                        return Index;
                                    }
                                }
                            case SortTarget.CompressedSize:
                                {
                                    if (SearchTarget.IsDirectory)
                                    {
                                        int Index = (await FilteredCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                            return FilteredCollection.Cast<ICompressionItem>().OrderBy((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressedSize).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
                                        }
                                    }
                                }
                            case SortTarget.CompressionRate:
                                {
                                    if (SearchTarget.IsDirectory)
                                    {
                                        int Index = (await FilteredCollection.OrderByNaturalStringSortAlgorithmAsync((Item) => Item.Name, SortDirection.Ascending)).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;

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
                                            return FilteredCollection.Cast<ICompressionItem>().OrderBy((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index + InputCollection.Count((Item) => Item.IsDirectory);
                                        }
                                        else
                                        {
                                            return FilteredCollection.Cast<ICompressionItem>().OrderByDescending((Item) => Item.CompressionRate).Select((Item, Index) => (Index, Item)).First((Value) => Value.Item.Equals(SearchTarget)).Index;
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
