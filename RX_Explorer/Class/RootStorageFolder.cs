using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;

namespace RX_Explorer.Class
{
    public sealed class RootStorageFolder : FileSystemStorageFolder
    {
        private static RootStorageFolder instance;

        public static RootStorageFolder Instance
        {
            get
            {
                return instance ??= new RootStorageFolder();
            }
        }

        public override string Name
        {
            get
            {
                return Globalization.GetString("RootStorageFolderDisplayName");
            }
        }

        public override string DisplayName
        {
            get
            {
                return Name;
            }
        }

        protected override Task LoadCoreAsync(FullTrustProcessController Controller, bool ForceUpdate)
        {
            return Task.CompletedTask;
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            return Task.FromResult((ulong)0);
        }

        public override Task<(uint, uint)> GetFolderAndFileNumAsync(CancellationToken CancelToken = default)
        {
            return Task.FromResult(((uint)0, (uint)0));
        }

        public override Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems, bool IncludeSystemItem, uint MaxNumLimit = uint.MaxValue, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder, Func<string, bool> AdvanceFilter = null)
        {
            return Task.FromResult<IReadOnlyList<FileSystemStorageItemBase>>(new List<FileSystemStorageItemBase>(0));
        }

        public override Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false, bool IncludeSystemItem = false, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            return Task.FromResult(false);
        }

        public override async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchAsync(string SearchWord,
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

            List<Task<IReadOnlyList<FileSystemStorageItemBase>>> ParallelTask = new List<Task<IReadOnlyList<FileSystemStorageItemBase>>>(CommonAccessCollection.DriveList.Count);

            foreach (DriveDataBase Drive in CommonAccessCollection.DriveList)
            {
                if (IsAQSExpression)
                {
                    ParallelTask.Add(SearchInUwpApi(Drive.Path, true));
                }
                else
                {
                    ParallelTask.Add(SearchCoreAsync(Drive.Path));
                }
            }

            return new List<FileSystemStorageItemBase>((await Task.WhenAll(ParallelTask)).SelectMany((Array) => Array));
        }

        private RootStorageFolder() : base(new Win32_File_Data("RootFolderUniquePath", default))
        {

        }
    }
}
