using FluentFTP;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
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
    public class FileSystemStorageFolder : FileSystemStorageItemBase, ICoreStorageItem<StorageFolder>
    {
        public override string Name => (System.IO.Path.GetPathRoot(Path)?.Equals(Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault() ? Path : System.IO.Path.GetFileName(Path);

        public override string DisplayName => (StorageItem?.DisplayName) ?? Name;

        public override string SizeDescription => string.Empty;

        public override string DisplayType => Type;

        public override string Type => Globalization.GetString("Folder_Admin_DisplayType");

        public override bool IsReadOnly => false;

        public override ulong Size => 0;

        public StorageFolder StorageItem { get; protected set; }

        private static readonly Uri Const_Folder_Image_Uri = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                 ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                 : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");

        public override BitmapImage Thumbnail => base.Thumbnail ?? new BitmapImage(Const_Folder_Image_Uri);

        public FileSystemStorageFolder(StorageFolder Item) : base(Item.Path, Item.GetSafeFileHandle(AccessMode.Read, OptimizeOption.None), false)
        {
            StorageItem = Item;
        }

        public FileSystemStorageFolder(NativeFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFolder(MTPFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFolder(FTPFileData Data) : base(Data)
        {

        }

        public virtual async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItems = false,
                                                                  bool IncludeSystemItems = false,
                                                                  BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            try
            {
                try
                {
                    return await Task.Run(() => NativeWin32API.CheckContainsAnyItem(Path, IncludeHiddenItems, IncludeSystemItems, Filter));
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
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckContainsAnyItemAsync)} failed and could not get the storage item, path:\"{Path}\"");
            }

            return false;
        }

        public virtual async Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            return Convert.ToUInt64(await GetChildItemsAsync(true, true, true, Filter: BasicFilters.File, CancelToken: CancelToken).Cast<FileSystemStorageFile>().SumAsync((Item) => Convert.ToInt64(Item.Size)));
        }

        public virtual IAsyncEnumerable<FileSystemStorageItemBase> SearchAsync(string SearchWord,
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

            async IAsyncEnumerable<FileSystemStorageItemBase> SearchInUwpApiAsync(string Path,
                                                                                  bool SearchInSubFolders,
                                                                                  bool IsRegexExpression,
                                                                                  bool IsAQSExpression,
                                                                                  bool UseIndexerOnly,
                                                                                  bool IgnoreCase,
                                                                                  [EnumeratorCancellation] CancellationToken CancelToken)
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

                            for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += 25)
                            {
                                Task<IReadOnlyList<IStorageItem>> SearchTask = Query.GetItemsAsync(Index, 25).AsTask();

                                if (await Task.WhenAny(SearchTask, CancelCompletionSource.Task) == CancelCompletionSource.Task)
                                {
                                    yield break;
                                }

                                IReadOnlyList<IStorageItem> ReadOnlyItemList = await SearchTask;

                                if (ReadOnlyItemList.Count == 0)
                                {
                                    yield break;
                                }

                                foreach (IStorageItem Item in IsRegexExpression
                                                              ? ReadOnlyItemList.Where((Item) => Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None))
                                                              : (IsAQSExpression ? ReadOnlyItemList : ReadOnlyItemList.Where((Item) => Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))))
                                {
                                    CancelToken.ThrowIfCancellationRequested();

                                    switch (Item)
                                    {
                                        case StorageFolder SubFolder:
                                            {
                                                yield return new FileSystemStorageFolder(SubFolder);
                                                break;
                                            }
                                        case StorageFile SubFile:
                                            {
                                                yield return new FileSystemStorageFile(SubFile);
                                                break;
                                            }
                                    }
                                }
                            }

                            CancelToken.ThrowIfCancellationRequested();
                        }
                    }
                }
            }

            async IAsyncEnumerable<FileSystemStorageItemBase> SearchCoreAsync(string Path,
                                                                              bool SearchInSubFolders,
                                                                              bool IncludeHiddenItems,
                                                                              bool IncludeSystemItems,
                                                                              bool IsRegexExpression,
                                                                              bool IsAQSExpression,
                                                                              bool UseIndexerOnly,
                                                                              bool IgnoreCase,
                                                                              [EnumeratorCancellation] CancellationToken CancelToken)
            {
                IReadOnlyList<FileSystemStorageItemBase> CurrentFolderItems;

                try
                {
                    CurrentFolderItems = new List<FileSystemStorageItemBase>(await Task.Run(() => NativeWin32API.Search(Path,
                                                                                                                        SearchWord,
                                                                                                                        IncludeHiddenItems,
                                                                                                                        IncludeSystemItems,
                                                                                                                        IsRegexExpression,
                                                                                                                        IgnoreCase,
                                                                                                                        CancelToken)));
                }
                catch (LocationNotAvailableException)
                {
                    CurrentFolderItems = await SearchInUwpApiAsync(Path, false, IsRegexExpression, IsAQSExpression, UseIndexerOnly, IgnoreCase, CancelToken).ToListAsync();
                }
                catch (Exception)
                {
                    CurrentFolderItems = new List<FileSystemStorageItemBase>(0);
                }

                foreach (FileSystemStorageItemBase Item in CurrentFolderItems)
                {
                    yield return Item;
                }

                if (SearchInSubFolders)
                {
                    if (await OpenAsync(Path) is FileSystemStorageFolder Folder)
                    {
                        await foreach (FileSystemStorageFolder Item in Folder.GetChildItemsAsync(IncludeHiddenItems, IncludeSystemItems, CancelToken: CancelToken, Filter: BasicFilters.Folder))
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            await foreach (FileSystemStorageItemBase SubItem in SearchCoreAsync(Item.Path, SearchInSubFolders, IncludeHiddenItems, IncludeSystemItems, IsRegexExpression, IsAQSExpression, UseIndexerOnly, IgnoreCase, CancelToken))
                            {
                                yield return SubItem;
                            }
                        }
                    }
                }
            }

            if (IsAQSExpression)
            {
                return SearchInUwpApiAsync(Path, true, IsRegexExpression, IsAQSExpression, UseIndexerOnly, IgnoreCase, CancelToken);
            }
            else
            {
                return SearchCoreAsync(Path, SearchInSubFolders, IncludeHiddenItems, IncludeSystemItems, IsRegexExpression, IsAQSExpression, UseIndexerOnly, IgnoreCase, CancelToken);
            }
        }

        public virtual async Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, CreateType ItemType, CreateOption Option)
        {
            string SubItemPath = System.IO.Path.Combine(Path, Name);

            try
            {
                switch (ItemType)
                {
                    case CreateType.File:
                        {
                            try
                            {
                                if (NativeWin32API.CreateFileFromPath(SubItemPath, Option, out string NewPath))
                                {
                                    return await OpenAsync(NewPath);
                                }
                                else if (await GetStorageItemAsync() is StorageFolder Folder)
                                {
                                    switch (Option)
                                    {
                                        case CreateOption.GenerateUniqueName:
                                            {
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(Name, CreationCollisionOption.GenerateUniqueName));
                                            }
                                        case CreateOption.OpenIfExist:
                                            {
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(Name, CreationCollisionOption.OpenIfExists));
                                            }
                                        case CreateOption.ReplaceExisting:
                                            {
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(Name, CreationCollisionOption.ReplaceExisting));
                                            }
                                        default:
                                            {
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.File, SubItemPath);

                                    if (string.IsNullOrEmpty(NewItemPath))
                                    {
                                        LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{SubItemPath}\"");
                                    }
                                    else
                                    {
                                        return await OpenAsync(NewItemPath);
                                    }
                                }
                            }

                            break;
                        }
                    case CreateType.Folder:
                        {
                            try
                            {
                                if (NativeWin32API.CreateDirectoryFromPath(SubItemPath, Option, out string NewPath))
                                {
                                    return await OpenAsync(NewPath);
                                }
                                else if (await GetStorageItemAsync() is StorageFolder Folder)
                                {
                                    switch (Option)
                                    {
                                        case CreateOption.GenerateUniqueName:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.GenerateUniqueName);
                                                return new FileSystemStorageFolder(NewFolder);
                                            }
                                        case CreateOption.OpenIfExist:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.OpenIfExists);
                                                return new FileSystemStorageFolder(NewFolder);
                                            }
                                        case CreateOption.ReplaceExisting:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.ReplaceExisting);
                                                return new FileSystemStorageFolder(NewFolder);
                                            }
                                        default:
                                            {
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.Folder, SubItemPath);

                                    if (string.IsNullOrEmpty(NewItemPath))
                                    {
                                        LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{SubItemPath}\"");
                                    }
                                    else
                                    {
                                        return await OpenAsync(NewItemPath);
                                    }
                                }
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CreateNewSubItemAsync)} failed and could not create the storage item, path:\"{SubItemPath}\"");
            }

            return null;
        }

        public virtual IAsyncEnumerable<FileSystemStorageItemBase> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                      bool IncludeSystemItems = false,
                                                                                      bool IncludeAllSubItems = false,
                                                                                      CancellationToken CancelToken = default,
                                                                                      BasicFilters Filter = BasicFilters.File | BasicFilters.Folder,
                                                                                      Func<string, bool> AdvanceFilter = null)
        {
            async IAsyncEnumerable<FileSystemStorageItemBase> GetChildItemsInUwpApiAsync(string Path, [EnumeratorCancellation] CancellationToken CancelToken)
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

                        if (ReadOnlyItemList.Count == 0)
                        {
                            yield break;
                        }

                        foreach (IStorageItem Item in ReadOnlyItemList)
                        {
                            if (Item is StorageFolder SubFolder)
                            {
                                yield return new FileSystemStorageFolder(SubFolder);
                            }
                            else if (Item is StorageFile SubFile)
                            {
                                yield return new FileSystemStorageFile(SubFile);
                            }
                        }
                    }

                    CancelToken.ThrowIfCancellationRequested();
                }
                else
                {
                    LogTracer.Log($"Uwp API in {nameof(GetChildItemsAsync)} failed to get the storage item, path:\"{Path}\"");
                }
            }

            async IAsyncEnumerable<FileSystemStorageItemBase> GetChildItemsCoreAsync(string Path,
                                                                                     bool IncludeHiddenItems,
                                                                                     bool IncludeSystemItems,
                                                                                     bool IncludeAllSubItems,
                                                                                     [EnumeratorCancellation] CancellationToken CancelToken,
                                                                                     BasicFilters Filter,
                                                                                     Func<string, bool> AdvanceFilter)
            {
                IReadOnlyList<FileSystemStorageItemBase> SubItems;

                try
                {
                    SubItems = await Task.Run(() => NativeWin32API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItems));
                }
                catch (LocationNotAvailableException)
                {
                    SubItems = await GetChildItemsInUwpApiAsync(Path, CancelToken).ToListAsync();
                }
                catch (Exception ex) when (ex is not DirectoryNotFoundException)
                {
                    SubItems = new List<FileSystemStorageItemBase>(0);
                }

                foreach (FileSystemStorageItemBase Item in SubItems.Where((Item) => (Item is FileSystemStorageFolder && Filter.HasFlag(BasicFilters.Folder)) || (Item is FileSystemStorageFile && Filter.HasFlag(BasicFilters.File)))
                                                                   .Where((Item) => (AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true)))
                {
                    yield return Item;
                }

                if (IncludeAllSubItems)
                {
                    foreach (FileSystemStorageFolder Item in SubItems.OfType<FileSystemStorageFolder>())
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        await foreach (FileSystemStorageItemBase SubItem in GetChildItemsCoreAsync(Item.Path, IncludeHiddenItems, IncludeSystemItems, IncludeAllSubItems, CancelToken, Filter, AdvanceFilter))
                        {
                            yield return SubItem;
                        }
                    }
                }
            }

            return GetChildItemsCoreAsync(Path, IncludeHiddenItems, IncludeSystemItems, IncludeAllSubItems, CancelToken, Filter, AdvanceFilter);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                try
                {
                    NativeFileData Data = NativeWin32API.GetStorageItemRawData(Path);

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
                if (!IsHiddenItem && !IsSystemItem)
                {
                    return StorageItem ??= await StorageFolder.GetFolderFromPathAsync(Path);
                }
            }
            catch (FileNotFoundException)
            {
                LogTracer.Log($"Could not get StorageFolder because directory is not found, path: {Path}");
            }
            catch (UnauthorizedAccessException)
            {
                LogTracer.Log($"Could not get StorageFolder because do not have enough permission to access this directory, path: {Path}");
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFolder, path: {Path}");
            }

            return null;
        }

        public static explicit operator StorageFolder(FileSystemStorageFolder File)
        {
            return File.StorageItem;
        }
    }
}
