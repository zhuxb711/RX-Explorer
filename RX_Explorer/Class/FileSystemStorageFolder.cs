using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFolder : FileSystemStorageItemBase<StorageFolder>
    {
        public override string Name
        {
            get
            {
                if (Path == Globalization.GetString("MainPage_PageDictionary_ThisPC_Label")) return Path;
                return (StorageItem?.Name) ?? (System.IO.Path.GetPathRoot(Path) == Path ? Path : System.IO.Path.GetFileName(Path));
            }
        }

        public override string DisplayName
        {
            get
            {
                return (StorageItem?.DisplayName) ?? Name;
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

        public override bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public override bool IsSystemItem
        {
            get
            {
                if (StorageItem == null)
                {
                    return base.IsSystemItem;
                }
                else
                {
                    return false;
                }
            }
        }

        private BitmapImage InnerThumbnail;

        public override BitmapImage Thumbnail
        {
            get
            {
                return InnerThumbnail ??= new BitmapImage(Const_Folder_Image_Uri);
            }
            protected set
            {
                if (value != null && value != InnerThumbnail)
                {
                    InnerThumbnail = value;
                }
            }
        }

        private readonly StorageFolder TempStorageItem;

        protected FileSystemStorageFolder(StorageFolder Item, DateTimeOffset ModifiedTime) : base(Item)
        {
            TempStorageItem = Item;
            CreationTimeRaw = Item.DateCreated;
            ModifiedTimeRaw = ModifiedTime;
        }
        public FileSystemStorageFolder(string Path) : base(Path)
        {

        }
        public FileSystemStorageFolder(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        public async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false, bool IncludeSystemItem = false, ItemFilters Filter = ItemFilters.File | ItemFilters.Folder)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Run(() =>
                {
                    return WIN_Native_API.CheckContainsAnyItem(Path, IncludeHiddenItem, IncludeSystemItem, Filter);
                });
            }
            else
            {
                LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                try
                {
                    if (await GetStorageItemAsync() is StorageFolder Folder)
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
                            IndexerOption = IndexerOption.DoNotUseIndexer
                        };
                        Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                        StorageFileQueryResult Query = Folder.CreateFileQueryWithOptions(Options);

                        ulong TotalSize = 0;

                        for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 25)
                        {
                            IReadOnlyList<StorageFile> ReadOnlyItemList = await Query.GetFilesAsync(Index, 25);

                            if (ReadOnlyItemList.Count > 0)
                            {
                                foreach (StorageFile File in ReadOnlyItemList)
                                {
                                    TotalSize += await File.GetSizeRawDataAsync().ConfigureAwait(false);

                                    if (CancelToken.IsCancellationRequested)
                                    {
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                break;
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

                    if (await GetStorageItemAsync() is StorageFolder Folder)
                    {
                        QueryOptions Options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Deep,
                            IndexerOption = IndexerOption.DoNotUseIndexer
                        };
                        Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                        StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                        uint FolderCount = 0, FileCount = 0;

                        for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 25)
                        {
                            IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(Index, 25);

                            if (ReadOnlyItemList.Count > 0)
                            {
                                foreach (IStorageItem Item in ReadOnlyItemList)
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
                            else
                            {
                                break;
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

        public async IAsyncEnumerable<FileSystemStorageItemBase> SearchAsync(string SearchWord, bool SearchInSubFolders = false, bool IncludeHiddenItem = false, bool IncludeSystemItem = false, bool IsRegexExpresstion = false, bool IgnoreCase = true, [EnumeratorCancellation] CancellationToken CancelToken = default)
        {
            if(Path == Globalization.GetString("MainPage_PageDictionary_ThisPC_Label"))
            {
                foreach (var item in CommonAccessCollection.DriveList)
                {
                    foreach (FileSystemStorageItemBase Item in await Task.Run(() => WIN_Native_API.Search(item.Path, SearchWord, SearchInSubFolders, IncludeHiddenItem, IncludeSystemItem, IsRegexExpresstion, IgnoreCase, CancelToken)))
                    {
                        yield return Item;
                    }
                }
            }
            else if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                foreach (FileSystemStorageItemBase Item in await Task.Run(() => WIN_Native_API.Search(Path, SearchWord, SearchInSubFolders, IncludeHiddenItem, IncludeSystemItem, IsRegexExpresstion, IgnoreCase, CancelToken)))
                {
                    yield return Item;
                }
            }
            else
            {
                if (await GetStorageItemAsync() is StorageFolder Folder)
                {
                    QueryOptions Options = new QueryOptions
                    {
                        FolderDepth = SearchInSubFolders ? FolderDepth.Deep : FolderDepth.Shallow,
                        IndexerOption = IndexerOption.DoNotUseIndexer
                    };
                    Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 150, ThumbnailOptions.UseCurrentScale);
                    Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FileName", "System.Size", "System.DateModified", "System.DateCreated" });

                    if (!IsRegexExpresstion)
                    {
                        Options.ApplicationSearchFilter = $"System.FileName:~=\"{SearchWord}\"";
                    }

                    StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                    for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 25)
                    {
                        IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(Index, 25);

                        if (ReadOnlyItemList.Count > 0)
                        {
                            IEnumerable<IStorageItem> Result = IsRegexExpresstion
                                                                ? ReadOnlyItemList.Where((Item) => Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None))
                                                                : ReadOnlyItemList.Where((Item) => Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

                            foreach (IStorageItem Item in Result)
                            {
                                if (CancelToken.IsCancellationRequested)
                                {
                                    yield break;
                                }

                                switch (Item)
                                {
                                    case StorageFolder SubFolder:
                                        {
                                            yield return new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync());
                                            break;
                                        }
                                    case StorageFile SubFile:
                                        {
                                            yield return await CreateFromStorageItemAsync(SubFile);
                                            break;
                                        }
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
        }

        public async Task<List<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems, bool IncludeSystemItem, ItemFilters Filter = ItemFilters.File | ItemFilters.Folder)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return WIN_Native_API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItem, Filter);
            }
            else
            {
                LogTracer.Log($"Native API could not enum subitems in path: \"{Path}\", fall back to UWP storage API");

                try
                {
                    if (await GetStorageItemAsync() is StorageFolder Folder)
                    {
                        QueryOptions Options = new QueryOptions
                        {
                            FolderDepth = FolderDepth.Shallow,
                            IndexerOption = IndexerOption.DoNotUseIndexer
                        };
                        Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 150, ThumbnailOptions.UseCurrentScale);
                        Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size", "System.DateModified" });

                        StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                        List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                        for (uint i = 0; ; i += 25)
                        {
                            IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(i, 25);

                            if (ReadOnlyItemList.Count > 0)
                            {
                                foreach (IStorageItem Item in ReadOnlyItemList.Where((Item) => (Item.IsOfType(StorageItemTypes.Folder) && Filter.HasFlag(ItemFilters.Folder)) || (Item.IsOfType(StorageItemTypes.File) && Filter.HasFlag(ItemFilters.File))))
                                {
                                    if (Item is StorageFolder SubFolder)
                                    {
                                        Result.Add(new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync()));
                                    }
                                    else if (Item is StorageFile SubFile)
                                    {
                                        Result.Add(await CreateFromStorageItemAsync(SubFile));
                                    }
                                }
                            }
                            else
                            {
                                break;
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

        protected override async Task LoadMorePropertiesCore(bool ForceUpdate)
        {
            if (await GetStorageItemAsync() is StorageFolder Folder)
            {
                Thumbnail = await Folder.GetThumbnailBitmapAsync(ThumbnailMode.ListView);

                if (ForceUpdate)
                {
                    ModifiedTimeRaw = await Folder.GetModifiedTimeAsync();
                }
            }
        }

        protected override bool CheckIfPropertiesLoaded()
        {
            return StorageItem != null;
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                return StorageItem ??= (TempStorageItem ?? await StorageFolder.GetFolderFromPathAsync(Path));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFolder, Path: {Path}");
                return null;
            }
        }
    }
}
