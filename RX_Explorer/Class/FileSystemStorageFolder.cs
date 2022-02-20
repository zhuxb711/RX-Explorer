using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
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
    public class FileSystemStorageFolder : FileSystemStorageItemBase, ICoreStorageItem<StorageFolder>
    {
        public override string Name => System.IO.Path.GetPathRoot(Path) == Path ? Path : System.IO.Path.GetFileName(Path);

        public override string DisplayName => (StorageItem?.DisplayName) ?? Name;

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

        public StorageFolder StorageItem { get; protected set; }

        private static readonly Uri Const_Folder_Image_Uri = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                 ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                 : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");

        public override BitmapImage Thumbnail => base.Thumbnail ?? new BitmapImage(Const_Folder_Image_Uri);

        public FileSystemStorageFolder(StorageFolder Item) : base(Item.Path, Item.GetSafeFileHandle(AccessMode.Read, OptimizeOption.None), false)
        {
            StorageItem = Item;
        }

        public FileSystemStorageFolder(Win32_File_Data Data) : base(Data)
        {

        }

        public FileSystemStorageFolder(MTP_File_Data Data) : base(Data)
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
                                                                                         else if (PreviousTask.Exception.InnerExceptions.Any((Ex) => Ex is DirectoryNotFoundException))
                                                                                         {
                                                                                             LogTracer.Log(PreviousTask.Exception.InnerExceptions.FirstOrDefault() ?? new Exception(), $"Path not found");
                                                                                         }

                                                                                         return new List<FileSystemStorageItemBase>(0);
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

        public virtual async Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, StorageItemTypes ItemTypes, CreateOption Option)
        {
            string SubItemPath = System.IO.Path.Combine(Path, Name);

            try
            {
                switch (ItemTypes)
                {
                    case StorageItemTypes.File:
                        {
                            try
                            {
                                if (Win32_Native_API.CreateFileFromPath(SubItemPath, Option, out string NewPath))
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
                    case StorageItemTypes.Folder:
                        {
                            try
                            {
                                if (Win32_Native_API.CreateDirectoryFromPath(SubItemPath, Option, out string NewPath))
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
                                foreach (IStorageItem Item in ReadOnlyItemList)
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
                    IReadOnlyList<FileSystemStorageItemBase> SubItems;

                    try
                    {
                        SubItems = await Task.Run(() => Win32_Native_API.GetStorageItems(Path, IncludeHiddenItems, IncludeSystemItems, MaxNumLimit));
                    }
                    catch (LocationNotAvailableException)
                    {
                        SubItems = await GetChildItemsInUwpApiAsync(Path);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        SubItems = new List<FileSystemStorageItemBase>(0);
                    }

                    Result.AddRange(SubItems.Where((Item) => (Item is FileSystemStorageFolder && Filter.HasFlag(BasicFilters.Folder)) || (Item is FileSystemStorageFile && Filter.HasFlag(BasicFilters.File)))
                                            .Where((Item) => (AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true)));

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

        public static explicit operator StorageFolder(FileSystemStorageFolder File)
        {
            return File.StorageItem;
        }
    }
}
