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

        protected override bool CheckIfPropertiesLoaded()
        {
            return true;
        }

        protected override Task LoadPropertiesAsync(FullTrustProcessController Controller, bool ForceUpdate)
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

            async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchInUwpApi(DriveDataBase Drive)
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                if (Drive.DriveFolder != null)
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

                    StorageItemQueryResult Query = Drive.DriveFolder.CreateItemQueryWithOptions(Options);

                    for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 50)
                    {
                        IReadOnlyList<IStorageItem> ReadOnlyItemList = await Query.GetItemsAsync(Index, 50).AsTask(CancelToken);

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
                                            Result.Add(new FileSystemStorageFolder(SubFolder, await SubFolder.GetModifiedTimeAsync()));
                                            break;
                                        }
                                    case StorageFile SubFile:
                                        {
                                            Result.Add(new FileSystemStorageFile(SubFile, await SubFile.GetModifiedTimeAsync(), await SubFile.GetSizeRawDataAsync()));
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

            try
            {
                foreach (DriveDataBase Drive in CommonAccessCollection.DriveList)
                {
                    if (IsAQSExpression)
                    {
                        await SearchInUwpApi(Drive);
                    }
                    else
                    {
                        try
                        {
                            return await Task.Factory.StartNew(() => Win32_Native_API.Search(Drive.Path,
                                                                                             SearchWord,
                                                                                             SearchInSubFolders,
                                                                                             IncludeHiddenItem,
                                                                                             IncludeSystemItem,
                                                                                             IsRegexExpression,
                                                                                             IgnoreCase,
                                                                                             CancelToken), TaskCreationOptions.LongRunning);
                        }
                        catch (LocationNotAvailableException)
                        {
                            await SearchInUwpApi(Drive);
                        }
                    }
                }

                return new List<FileSystemStorageItemBase>(0);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SearchAsync)} failed for uwp API");
                return new List<FileSystemStorageItemBase>(0);
            }
        }

        private RootStorageFolder() : base(new Win32_File_Data("RootFolderUniquePath", default))
        {

        }
    }
}
