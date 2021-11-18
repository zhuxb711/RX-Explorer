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
            IReadOnlyList<FileSystemStorageItemBase> Result = await GetChildItemsAsync(true, true, true, Filter: BasicFilters.File, CancelToken: CancelToken);
            return Convert.ToUInt64(Result.Cast<FileSystemStorageFile>().Sum((Item) => Convert.ToInt64(Item.Size)));
        }

        public virtual Task<IReadOnlyList<FileSystemStorageItemBase>> SearchAsync(string SearchWord,
                                                                                        bool SearchInSubFolders = false,
                                                                                        bool IncludeHiddenItems = false,
                                                                                        bool IncludeSystemItems = false,
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

            async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchInUwpApiAsync(string Path, bool SearchInSubFolders)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                try
                {
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
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"UWP Search API could not search the folder: \"{Path}\"");
                }

                return Result;
            }

            async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchCoreAsync(string Path)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                try
                {
                    Result.AddRange(await Task.Run(() => Win32_Native_API.Search(Path,
                                                                                 SearchWord,
                                                                                 IncludeHiddenItems,
                                                                                 IncludeSystemItems,
                                                                                 IsRegexExpression,
                                                                                 IgnoreCase,
                                                                                 CancelToken)).ContinueWith((PreviousTask) =>
                                                                                 {
                                                                                     if (PreviousTask.IsFaulted)
                                                                                     {
                                                                                         if (PreviousTask.Exception.InnerExceptions.Any((Ex) => Ex is LocationNotAvailableException))
                                                                                         {
                                                                                             return SearchInUwpApiAsync(Path, false).Result;
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
                            foreach (FileSystemStorageFolder Item in await Folder.GetChildItemsAsync(IncludeHiddenItems, IncludeSystemItems, Filter: BasicFilters.Folder))
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
                return SearchInUwpApiAsync(Path, true);
            }
            else
            {
                return SearchCoreAsync(Path);
            }
        }

        public virtual Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                         bool IncludeSystemItems = false,
                                                                                         bool IncludeAllSubItems = false,
                                                                                         uint MaxNumLimit = uint.MaxValue,
                                                                                         CancellationToken CancelToken = default,
                                                                                         Func<string, bool> AdvanceFilter = null,
                                                                                         BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            async Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsInUwpApiAsync(string Path)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

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

                        for (uint i = 0; !CancelToken.IsCancellationRequested; i += 25)
                        {
                            IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(i, 25);

                            if (ReadOnlyItemList.Count > 0)
                            {
                                foreach (IStorageItem Item in ReadOnlyItemList.Where((Item) => (Item.IsOfType(StorageItemTypes.Folder) && Filter.HasFlag(BasicFilters.Folder)) || (Item.IsOfType(StorageItemTypes.File) && Filter.HasFlag(BasicFilters.File)))
                                                                              .Where((Item) => (AdvanceFilter?.Invoke(Item.Name)) ?? true))
                                {
                                    if (Result.Count >= MaxNumLimit)
                                    {
                                        goto BACK;
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
                    }
                    else
                    {
                        LogTracer.Log($"Uwp API in {nameof(GetChildItemsAsync)} failed to get the storage item, path:\"{Path}\"");
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(GetChildItemsAsync)} threw an exception, path:\"{Path}\"");
                }

            BACK:
                return Result;
            }

            async Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsCoreAsync(string Path)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                try
                {
                    IReadOnlyList<FileSystemStorageItemBase> SubItems = await Task.Run(() => Win32_Native_API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItems, MaxNumLimit, Filter, AdvanceFilter))
                                                                                  .ContinueWith((PreviousTask) =>
                                                                                  {
                                                                                      if (PreviousTask.IsFaulted)
                                                                                      {
                                                                                          if (PreviousTask.Exception.InnerExceptions.Any((Ex) => Ex is LocationNotAvailableException))
                                                                                          {
                                                                                              return GetChildItemsInUwpApiAsync(Path).Result;
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
                                                                                  });

                    Result.AddRange(SubItems);

                    if (IncludeAllSubItems)
                    {
                        foreach (FileSystemStorageFolder Item in SubItems.OfType<FileSystemStorageFolder>())
                        {
                            if (CancelToken.IsCancellationRequested)
                            {
                                break;
                            }
                            else
                            {
                                Result.AddRange(await GetChildItemsCoreAsync(Item.Path));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(GetChildItemsAsync)} threw an exception, path:\"{Path}\"");
                }

                return Result;
            }

            return GetChildItemsCoreAsync(Path);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                try
                {
                    Win32_File_Data Data = await Task.Run(() => Win32_Native_API.GetStorageItemRawData(Path));

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
