using Microsoft.Win32.SafeHandles;
using ShareClassLibrary;
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
        public override string Name => System.IO.Path.GetPathRoot(Path) == Path ? Path : System.IO.Path.GetFileName(Path);

        public override string DisplayName => ((StorageItem as StorageFolder)?.DisplayName) ?? Name;

        public override string SizeDescription => string.Empty;

        public override string DisplayType => Type;

        public override string Type => Globalization.GetString("Folder_Admin_DisplayType");

        public override bool IsReadOnly => false;

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

        public override BitmapImage Thumbnail => base.Thumbnail ?? new BitmapImage(Const_Folder_Image_Uri);

        public FileSystemStorageFolder(StorageFolder Item) : base(Item.Path, Item.GetSafeFileHandle(AccessMode.Read), false)
        {
            StorageItem = Item;
        }

        public FileSystemStorageFolder(Win32_File_Data Data) : base(Data)
        {

        }

        public virtual async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false,
                                                                  bool IncludeSystemItem = false,
                                                                  BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            try
            {
                try
                {
                    return await Task.Run(() => Win32_Native_API.CheckContainsAnyItem(Path, IncludeHiddenItem, IncludeSystemItem, Filter));
                }
                catch (LocationNotAvailableException)
                {
                    if (await GetStorageItemAsync() is StorageFolder Folder)
                    {
                        if (Filter.HasFlag(BasicFilters.File) && Filter.HasFlag(BasicFilters.Folder))
                        {
                            return (await Folder.GetItemsAsync(0, 1)).Any();
                        }
                        else if (Filter.HasFlag(BasicFilters.File))
                        {
                            return (await Folder.GetFilesAsync(CommonFileQuery.DefaultQuery, 0, 1)).Any();
                        }
                        else if (Filter.HasFlag(BasicFilters.Folder))
                        {
                            return (await Folder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Any();
                        }
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckContainsAnyItemAsync)} failed and could not get the storage item, path:\"{Path}\"");
                return false;
            }
        }

        public virtual async Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            try
            {
                try
                {
                    return await Task.Factory.StartNew(() => Win32_Native_API.CalulateSize(Path, CancelToken), TaskCreationOptions.LongRunning);
                }
                catch (LocationNotAvailableException)
                {
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
                                    if (CancelToken.IsCancellationRequested)
                                    {
                                        break;
                                    }

                                    using (SafeFileHandle Handle = File.GetSafeFileHandle(AccessMode.Read))
                                    {
                                        if (!Handle.IsInvalid)
                                        {
                                            Win32_File_Data Data = Win32_Native_API.GetStorageItemRawDataFromHandle(File.Path, Handle.DangerousGetHandle());

                                            if (Data.IsDataValid)
                                            {
                                                TotalSize += Data.Size;
                                                continue;
                                            }
                                        }
                                    }

                                    TotalSize += await File.GetSizeRawDataAsync();
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetFolderSizeAsync)} failed and could not get the storage item, path:\"{Path}\"");
                return 0;
            }
        }

        public virtual async Task<(uint, uint)> GetFolderAndFileNumAsync(CancellationToken CancelToken = default)
        {
            try
            {
                try
                {
                    return await Task.Factory.StartNew(() => Win32_Native_API.CalculateFolderAndFileCount(Path, CancelToken), TaskCreationOptions.LongRunning);
                }
                catch (LocationNotAvailableException)
                {
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetFolderAndFileNumAsync)} failed and could not get the storage item, path:\"{Path}\"");
                return (0, 0);
            }
        }

        public virtual async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchAsync(string SearchWord,
                                                                                        bool SearchInSubFolders = false,
                                                                                        bool IncludeHiddenItem = false,
                                                                                        bool IncludeSystemItem = false,
                                                                                        bool IsRegexExpression = false,
                                                                                        bool IsAQSExpression = false,
                                                                                        bool UseIndexerOnly = false,
                                                                                        bool IgnoreCase = true,
                                                                                        CancellationToken CancelToken = default)
        {
            if (IsRegexExpression && IsAQSExpression)
            {
                throw new ArgumentException($"{nameof(IsRegexExpression)} and {nameof(IsAQSExpression)} could not be true at the same time");
            }

            async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchInUwpApi(string Path, bool SearchInSubFolders)
            {
                try
                {
                    List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                    if (await OpenAsync(Path) is FileSystemStorageFolder ParentFolder)
                    {
                        if (await ParentFolder.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            TaskCompletionSource<IReadOnlyList<IStorageItem>> CancelCompletionSource = new TaskCompletionSource<IReadOnlyList<IStorageItem>>();

                            using (CancelToken.Register(() => CancelCompletionSource.TrySetResult(new List<IStorageItem>(0))))
                            {
                                QueryOptions Options = new QueryOptions
                                {
                                    FolderDepth = SearchInSubFolders ? FolderDepth.Deep : FolderDepth.Shallow,
                                    IndexerOption = UseIndexerOnly ? IndexerOption.OnlyUseIndexer : IndexerOption.DoNotUseIndexer
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
                                    IReadOnlyList<IStorageItem> ReadOnlyItemList = (await Task.WhenAny(Query.GetItemsAsync(Index, 50).AsTask(), CancelCompletionSource.Task)).Result;

                                    if (ReadOnlyItemList.Count > 0)
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
                                                        Result.Add(new FileSystemStorageFolder(SubFolder));
                                                        break;
                                                    }
                                                case StorageFile SubFile:
                                                    {
                                                        Result.Add(new FileSystemStorageFile(SubFile));
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

                    return Result;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"UWP Search API could not search the folder: \"{Path}\"");
                    return new List<FileSystemStorageItemBase>(0);
                }
            }

            async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchCoreAsync(string Path)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                try
                {
                    Result.AddRange(await Task.Run(() => Win32_Native_API.Search(Path,
                                                                                 SearchWord,
                                                                                 IncludeHiddenItem,
                                                                                 IncludeSystemItem,
                                                                                 IsRegexExpression,
                                                                                 IgnoreCase,
                                                                                 CancelToken)).ContinueWith((PreviousTask) =>
                                                                                 {
                                                                                     if (PreviousTask.IsFaulted)
                                                                                     {
                                                                                         if (PreviousTask.Exception.InnerExceptions.Any((Ex) => Ex is LocationNotAvailableException))
                                                                                         {
                                                                                             return SearchInUwpApi(Path, false).Result;
                                                                                         }
                                                                                         else
                                                                                         {
                                                                                             return new List<FileSystemStorageItemBase>(0);
                                                                                         }
                                                                                     }
                                                                                     else
                                                                                     {
                                                                                         return PreviousTask.Result;
                                                                                     }
                                                                                 }));

                    if (SearchInSubFolders)
                    {
                        if (await OpenAsync(Path) is FileSystemStorageFolder Folder)
                        {
                            foreach (FileSystemStorageFolder Item in await Folder.GetChildItemsAsync(IncludeHiddenItem, IncludeSystemItem, Filter: BasicFilters.Folder))
                            {
                                if (CancelToken.IsCancellationRequested)
                                {
                                    break;
                                }
                                else
                                {
                                    Result.AddRange(await SearchCoreAsync(Item.Path));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Mixed Search API could not search the folder: \"{Path}\"");
                }

                return Result;
            }

            if (IsAQSExpression)
            {
                return await SearchInUwpApi(Path, true);
            }
            else
            {
                return await SearchCoreAsync(Path);
            }
        }

        public virtual async Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems,
                                                                                               bool IncludeSystemItem,
                                                                                               uint MaxNumLimit = uint.MaxValue,
                                                                                               BasicFilters Filter = BasicFilters.File | BasicFilters.Folder,
                                                                                               Func<string, bool> AdvanceFilter = null)
        {
            try
            {
                try
                {
                    return await Task.Run(() => Win32_Native_API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItem, MaxNumLimit, Filter, AdvanceFilter));
                }
                catch (LocationNotAvailableException)
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
                                        Result.Add(new FileSystemStorageFolder(SubFolder));
                                    }
                                    else if (Item is StorageFile SubFile)
                                    {
                                        Result.Add(new FileSystemStorageFile(SubFile));
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
                        LogTracer.Log($"{nameof(GetChildItemsAsync)} failed and could not get the storage item, path:\"{Path}\"");
                        return new List<FileSystemStorageItemBase>(0);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetChildItemsAsync)} thew an unexpected exception, path:\"{Path}\"");
                return new List<FileSystemStorageItemBase>(0);
            }
        }

        protected override async Task LoadCoreAsync(FullTrustProcessController Controller, bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                try
                {
                    Win32_File_Data Data = Win32_Native_API.GetStorageItemRawData(Path);

                    if (Data.IsDataValid)
                    {
                        ModifiedTime = Data.ModifiedTime;
                    }
                    else if (await GetStorageItemAsync() is StorageFolder Folder)
                    {
                        ModifiedTime = await Folder.GetModifiedTimeAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(LoadCoreAsync)}");
                }
            }
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
    }
}
