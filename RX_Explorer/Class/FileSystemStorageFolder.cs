using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFolder : FileSystemStorageItemBase
    {
        public override string Name
        {
            get
            {
                return StorageItem?.Name ?? System.IO.Path.GetFileName(Path);
            }
        }

        public override string DisplayName
        {
            get
            {
                return StorageItem?.DisplayName ?? Name;
            }
        }

        public override string Size
        {
            get
            {
                return string.Empty;
            }
        }

        public override string DisplayType
        {
            get
            {
                return Type;
            }
        }

        public override string Type
        {
            get
            {
                return Globalization.GetString("Folder_Admin_DisplayType");
            }
        }

        public override BitmapImage Thumbnail { get; protected set; } = Const_Folder_Image;

        protected StorageFolder StorageItem { get; set; }

        protected FileSystemStorageFolder(string Path) : base(Path)
        {

        }

        public FileSystemStorageFolder(StorageFolder Item, DateTimeOffset ModifiedTimeRaw) : base(Item.Path)
        {
            StorageItem = Item;
            CreationTimeRaw = Item.DateCreated;
            this.ModifiedTimeRaw = ModifiedTimeRaw;
        }

        public FileSystemStorageFolder(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        public async Task<bool> CheckContainsAnyItemAsync(ItemFilters Filter = ItemFilters.File | ItemFilters.Folder)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Run(() =>
                {
                    return WIN_Native_API.CheckContainsAnyItem(Path, Filter);
                });
            }
            else
            {
                LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                try
                {
                    if (await GetStorageItemAsync().ConfigureAwait(true) is StorageFolder Folder)
                    {
                        if (Filter.HasFlag(ItemFilters.File))
                        {
                            return (await Folder.GetFilesAsync(CommonFileQuery.DefaultQuery, 0, 1)).Any();
                        }

                        if (Filter.HasFlag(ItemFilters.Folder))
                        {
                            return (await Folder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Any();
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(CheckContainsAnyItemAsync)} failed for uwp API");
                    return false;
                }
            }
        }

        public async Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Run(() =>
                {
                    return WIN_Native_API.CalulateSize(Path, CancelToken);
                });
            }
            else
            {
                try
                {
                    LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                    if (await GetStorageItemAsync() is StorageFolder Folder)
                    {
                        QueryOptions Options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Deep,
                            IndexerOption = IndexerOption.UseIndexerWhenAvailable
                        };
                        Options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                        StorageFileQueryResult Query = Folder.CreateFileQueryWithOptions(Options);

                        uint FileCount = await Query.GetItemCountAsync();

                        ulong TotalSize = 0;

                        for (uint Index = 0; Index < FileCount && !CancelToken.IsCancellationRequested; Index += 50)
                        {
                            foreach (StorageFile File in await Query.GetFilesAsync(Index, 50))
                            {
                                TotalSize += await File.GetSizeRawDataAsync().ConfigureAwait(false);

                                if (CancelToken.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        }

                        return TotalSize;
                    }
                    else
                    {
                        return 0;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(GetFolderSizeAsync)} failed for uwp API");
                    return 0;
                }
            }
        }

        public async Task<(uint, uint)> GetFolderAndFileNumAsync(CancellationToken CancelToken = default)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Run(() =>
                {
                    return WIN_Native_API.CalculateFolderAndFileCount(Path, CancelToken);
                });
            }
            else
            {
                try
                {
                    LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                    if (await GetStorageItemAsync().ConfigureAwait(true) is StorageFolder Folder)
                    {
                        QueryOptions Options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Deep,
                            IndexerOption = IndexerOption.UseIndexerWhenAvailable
                        };
                        Options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                        StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                        uint ItemCount = await Query.GetItemCountAsync();

                        uint FolderCount = 0, FileCount = 0;

                        for (uint Index = 0; Index < ItemCount && !CancelToken.IsCancellationRequested; Index += 50)
                        {
                            foreach (IStorageItem Item in await Query.GetItemsAsync(Index, 50))
                            {
                                if (Item.IsOfType(StorageItemTypes.Folder))
                                {
                                    FolderCount++;
                                }
                                else
                                {
                                    FileCount++;
                                }

                                if (CancelToken.IsCancellationRequested)
                                {
                                    break;
                                }
                            }
                        }

                        return (FolderCount, FileCount);
                    }
                    else
                    {
                        return (0, 0);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(GetFolderAndFileNumAsync)} failed for uwp API");
                    return (0, 0);
                }
            }
        }

        public async IAsyncEnumerable<FileSystemStorageItemBase> SearchAsync(string SearchWord, bool SearchInSubFolders = false, bool IncludeHiddenItem = false, bool IsRegexExpresstion = false, bool IgnoreCase = true, [EnumeratorCancellation] CancellationToken CancelToken = default)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                List<FileSystemStorageItemBase> SearchResult = await Task.Run(() =>
                {
                    return WIN_Native_API.Search(Path, SearchWord, SearchInSubFolders, IncludeHiddenItem, IsRegexExpresstion, IgnoreCase, CancelToken);
                });

                foreach (FileSystemStorageItemBase Item in SearchResult)
                {
                    yield return Item;

                    if (CancelToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                }
            }
            else
            {
                if (await GetStorageItemAsync().ConfigureAwait(false) is StorageFolder Folder)
                {
                    QueryOptions Options = new QueryOptions
                    {
                        FolderDepth = SearchInSubFolders ? FolderDepth.Deep : FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    Options.SetThumbnailPrefetch(Windows.Storage.FileProperties.ThumbnailMode.ListView, 150, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
                    Options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size", "System.DateModified" });

                    if (!IsRegexExpresstion)
                    {
                        Options.ApplicationSearchFilter = $"System.FileName:*{SearchWord}*";
                    }

                    StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                    uint FileCount = await Query.GetItemCountAsync();

                    for (uint Index = 0; Index < FileCount && !CancelToken.IsCancellationRequested; Index += 50)
                    {
                        IEnumerable<IStorageItem> Result = IsRegexExpresstion
                                                            ? (await Query.GetItemsAsync(Index, 50)).Where((Item) => Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None))
                                                            : (await Query.GetItemsAsync(Index, 50)).Where((Item) => Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

                        foreach (IStorageItem Item in Result)
                        {
                            switch (Item)
                            {
                                case StorageFolder SubFolder:
                                    {
                                        yield return new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync().ConfigureAwait(false));
                                        break;
                                    }
                                case StorageFile SubFile:
                                    {
                                        yield return new FileSystemStorageFile(SubFile, await SubFile.GetSizeRawDataAsync().ConfigureAwait(false), await SubFile.GetModifiedTimeAsync().ConfigureAwait(false));
                                        break;
                                    }
                            }

                            if (CancelToken.IsCancellationRequested)
                            {
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        public async Task<List<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems, ItemFilters Filter = ItemFilters.File | ItemFilters.Folder)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return WIN_Native_API.GetStorageItems(Path, IncludeHiddenItems, Filter);
            }
            else
            {
                LogTracer.Log($"Native API could not enum subitems in path: \"{Path}\", fall back to UWP storage API");

                try
                {
                    if (await GetStorageItemAsync().ConfigureAwait(false) is StorageFolder Folder)
                    {
                        QueryOptions Options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Shallow,
                            IndexerOption = IndexerOption.UseIndexerWhenAvailable
                        };
                        Options.SetThumbnailPrefetch(Windows.Storage.FileProperties.ThumbnailMode.ListView, 150, Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
                        Options.SetPropertyPrefetch(Windows.Storage.FileProperties.PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size", "System.DateModified" });

                        StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                        uint Count = await Query.GetItemCountAsync();

                        List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>(Convert.ToInt32(Count));

                        for (uint i = 0; i < Count; i += 30)
                        {
                            IReadOnlyList<IStorageItem> CurrentList = await Query.GetItemsAsync(i, 30);

                            foreach (IStorageItem Item in CurrentList.Where((Item) => (Item.IsOfType(StorageItemTypes.Folder) && Filter.HasFlag(ItemFilters.Folder)) || (Item.IsOfType(StorageItemTypes.File) && Filter.HasFlag(ItemFilters.File))))
                            {
                                if (Item is StorageFolder SubFolder)
                                {
                                    Result.Add(new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync().ConfigureAwait(false)));
                                }
                                else if (Item is StorageFile SubFile)
                                {
                                    Result.Add(new FileSystemStorageFile(SubFile, await SubFile.GetSizeRawDataAsync().ConfigureAwait(false), await SubFile.GetModifiedTimeAsync().ConfigureAwait(false)));
                                }
                            }
                        }

                        return Result;
                    }
                    else
                    {
                        return new List<FileSystemStorageItemBase>(0);
                    }
                }
                catch
                {
                    LogTracer.Log($"UWP API could not enum subitems in path: \"{Path}\"");
                    return new List<FileSystemStorageItemBase>(0);
                }
            }
        }

        protected override async Task LoadMorePropertyCore(bool ForceUpdate)
        {
            if ((StorageItem == null || ForceUpdate) && await GetStorageItemAsync().ConfigureAwait(true) is StorageFolder Folder)
            {
                StorageItem = Folder;
                Thumbnail = await Folder.GetThumbnailBitmapAsync().ConfigureAwait(true);

                if (ForceUpdate)
                {
                    ModifiedTimeRaw = await Folder.GetModifiedTimeAsync().ConfigureAwait(true);
                    SizeRaw = await Folder.GetSizeRawDataAsync().ConfigureAwait(true);
                }
            }
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                return await StorageFolder.GetFolderFromPathAsync(Path);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFolder, Path: {Path}");
                return null;
            }
        }
    }
}
