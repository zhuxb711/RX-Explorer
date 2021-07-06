using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
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
                return System.IO.Path.GetPathRoot(Path) == Path ? Path : System.IO.Path.GetFileName(Path);
            }
        }

        public override string DisplayName
        {
            get
            {
                return ((StorageItem as StorageFolder)?.DisplayName) ?? Name;
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
                return InnerThumbnail ?? new BitmapImage(Const_Folder_Image_Uri);
            }
            protected set
            {
                if (value != null && value != InnerThumbnail)
                {
                    InnerThumbnail = value;
                }
            }
        }

        protected override bool IsFullTrustProcessNeeded
        {
            get
            {
                return false;
            }
        }

        protected FileSystemStorageFolder(StorageFolder Item, DateTimeOffset ModifiedTime) : base(Item.Path)
        {
            CreationTimeRaw = Item.DateCreated;
            ModifiedTimeRaw = ModifiedTime;
        }

        public FileSystemStorageFolder(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        public virtual async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false, bool IncludeSystemItem = false, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
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
                        if (Filter.HasFlag(BasicFilters.File))
                        {
                            return (await Folder.GetFilesAsync(CommonFileQuery.DefaultQuery, 0, 1)).Any();
                        }

                        if (Filter.HasFlag(BasicFilters.Folder))
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

        public virtual async Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Factory.StartNew(() =>
                {
                    return WIN_Native_API.CalulateSize(Path, CancelToken);
                }, TaskCreationOptions.LongRunning);
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
                            IndexerOption = IndexerOption.DoNotUseIndexer,
                            ApplicationSearchFilter = "System.Size:>0"
                        };
                        Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                        StorageFileQueryResult Query = Folder.CreateFileQueryWithOptions(Options);

                        ulong TotalSize = 0;

                        for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 50)
                        {
                            IReadOnlyList<StorageFile> ReadOnlyItemList = await Query.GetFilesAsync(Index, 50);

                            if (ReadOnlyItemList.Any())
                            {
                                foreach (StorageFile File in ReadOnlyItemList)
                                {
                                    TotalSize += await File.GetSizeRawDataAsync();

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

        public virtual async Task<(uint, uint)> GetFolderAndFileNumAsync(CancellationToken CancelToken = default)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return await Task.Factory.StartNew(() =>
                {
                    return WIN_Native_API.CalculateFolderAndFileCount(Path, CancelToken);
                }, TaskCreationOptions.LongRunning);
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

                        StorageFileQueryResult FileQuery = Folder.CreateFileQueryWithOptions(Options);
                        StorageFolderQueryResult FolderQuery = Folder.CreateFolderQueryWithOptions(Options);

                        uint[] Results = await Task.WhenAll(FolderQuery.GetItemCountAsync().AsTask(CancelToken), FileQuery.GetItemCountAsync().AsTask(CancelToken));

                        return (Results[0], Results[1]);
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

        public virtual async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchAsync(string SearchWord,
                                                                                        bool SearchInSubFolders = false,
                                                                                        bool IncludeHiddenItem = false,
                                                                                        bool IncludeSystemItem = false,
                                                                                        bool IsRegexExpression = false,
                                                                                        bool IsAQSExpression = false,
                                                                                        bool IgnoreCase = true,
                                                                                        CancellationToken CancelToken = default)
        {
            if (IsRegexExpression && IsAQSExpression)
            {
                throw new ArgumentException($"{nameof(IsRegexExpression)} and {nameof(IsAQSExpression)} could not be true at the same time");
            }

            if (WIN_Native_API.CheckLocationAvailability(Path) && !IsAQSExpression)
            {
                return await Task.Factory.StartNew(() => WIN_Native_API.Search(Path,
                                                                               SearchWord,
                                                                               SearchInSubFolders,
                                                                               IncludeHiddenItem,
                                                                               IncludeSystemItem,
                                                                               IsRegexExpression,
                                                                               IgnoreCase,
                                                                               CancelToken), TaskCreationOptions.LongRunning);
            }
            else
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                if (await GetStorageItemAsync() is StorageFolder Folder)
                {
                    QueryOptions Options = new QueryOptions
                    {
                        FolderDepth = SearchInSubFolders ? FolderDepth.Deep : FolderDepth.Shallow,
                        IndexerOption = IndexerOption.DoNotUseIndexer
                    };
                    Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 150, ThumbnailOptions.UseCurrentScale);
                    Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FileName", "System.Size", "System.DateModified", "System.DateCreated", "System.ParsingPath" });

                    if (IsAQSExpression)
                    {
                        Options.UserSearchFilter = SearchWord;
                    }
                    else if (!IsRegexExpression)
                    {
                        Options.ApplicationSearchFilter = $"System.FileName:~~\"{SearchWord}\"";
                    }

                    StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                    for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 50)
                    {
                        IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(Index, 50).AsTask(CancelToken);

                        if (ReadOnlyItemList.Any())
                        {
                            foreach (IStorageItem Item in IsRegexExpression
                                                          ? ReadOnlyItemList.Where((Item) => Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None))
                                                          : (IsAQSExpression ? ReadOnlyItemList : ReadOnlyItemList.Where((Item) => Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
                            {
                                if (CancelToken.IsCancellationRequested)
                                {
                                    break;
                                }

                                switch (Item)
                                {
                                    case StorageFolder SubFolder:
                                        {
                                            Result.Add(new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync()));
                                            break;
                                        }
                                    case StorageFile SubFile:
                                        {
                                            Result.Add(await CreateFromStorageItemAsync(SubFile));
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

                return Result;
            }
        }

        public virtual async Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems, bool IncludeSystemItem, uint MaxNumLimit = uint.MaxValue, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder, Func<string, bool> AdvanceFilter = null)
        {
            if (WIN_Native_API.CheckLocationAvailability(Path))
            {
                return WIN_Native_API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItem, MaxNumLimit, Filter, AdvanceFilter);
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
                        Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FileName", "System.Size", "System.DateModified", "System.DateCreated", "System.ParsingPath" });

                        StorageItemQueryResult Query = Folder.CreateItemQueryWithOptions(Options);

                        List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                        for (uint i = 0; ; i += 25)
                        {
                            IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(i, 25);

                            if (ReadOnlyItemList.Count > 0)
                            {
                                foreach (IStorageItem Item in ReadOnlyItemList.Where((Item) => (Item.IsOfType(StorageItemTypes.Folder) && Filter.HasFlag(BasicFilters.Folder)) || (Item.IsOfType(StorageItemTypes.File) && Filter.HasFlag(BasicFilters.File))))
                                {
                                    if (Result.Count >= MaxNumLimit)
                                    {
                                        return Result;
                                    }

                                    if (AdvanceFilter != null && !AdvanceFilter(Item.Name))
                                    {
                                        continue;
                                    }

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

        protected override async Task LoadPropertiesAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                if (await GetStorageItemAsync() is StorageFolder Folder)
                {
                    ModifiedTimeRaw = await Folder.GetModifiedTimeAsync();
                }
            }
        }

        protected override Task LoadPropertiesAsync(bool ForceUpdate, FullTrustProcessController Controller)
        {
            return LoadPropertiesAsync(ForceUpdate);
        }

        protected override bool CheckIfPropertiesLoaded()
        {
            return StorageItem != null && InnerThumbnail != null;
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                return StorageItem ??= await StorageFolder.GetFolderFromPathAsync(Path);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFolder, Path: {Path}");
                return null;
            }
        }

        protected override bool CheckIfNeedLoadThumbnailOverlay()
        {
            return SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive);
        }

        protected override async Task LoadThumbnailAsync(ThumbnailMode Mode)
        {
            if (await GetStorageItemAsync() is StorageFolder Folder)
            {
                Thumbnail = await Folder.GetThumbnailBitmapAsync(Mode);
            }
        }
    }
}
