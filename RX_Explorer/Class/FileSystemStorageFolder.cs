using FluentFTP;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFolder : FileSystemStorageItemBase
    {
        public override string Name => (System.IO.Path.GetPathRoot(Path)?.Equals(Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault() ? Path : System.IO.Path.GetFileName(Path);

        public override string DisplayName => ((StorageItem as StorageFolder)?.DisplayName) ?? Name;

        public override string DisplayType => Type;

        public override string Type => Globalization.GetString("Folder_Admin_DisplayType");

        public override bool IsReadOnly => false;

        public override ulong Size => 0;

        public override BitmapImage Thumbnail => base.Thumbnail ??= new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                                       ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                                       : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));

        public FileSystemStorageFolder(NativeFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFolder(MTPFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFolder(FtpFileData Data) : base(Data)
        {

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
                                                yield return new FileSystemStorageFolder(await SubFolder.GetNativeFileDataAsync());
                                                break;
                                            }
                                        case StorageFile SubFile:
                                            {
                                                yield return new FileSystemStorageFile(await SubFile.GetNativeFileDataAsync());
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
                                try
                                {
                                    if (NativeWin32API.CreateFileFromPath(SubItemPath, Option, out string NewFilePath))
                                    {
                                        if (await OpenAsync(NewFilePath) is FileSystemStorageFile NewFile)
                                        {
                                            return NewFile;
                                        }
                                    }

                                    throw new Exception();
                                }
                                catch (Exception)
                                {
                                    if (await GetStorageItemAsync() is StorageFolder Folder)
                                    {
                                        switch (Option)
                                        {
                                            case CreateOption.GenerateUniqueName:
                                                {
                                                    StorageFile NewFile = await Folder.CreateFileAsync(Name, CreationCollisionOption.GenerateUniqueName);
                                                    return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                }
                                            case CreateOption.OpenIfExist:
                                                {
                                                    StorageFile NewFile = await Folder.CreateFileAsync(Name, CreationCollisionOption.OpenIfExists);
                                                    return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                }
                                            case CreateOption.ReplaceExisting:
                                                {
                                                    StorageFile NewFile = await Folder.CreateFileAsync(Name, CreationCollisionOption.ReplaceExisting);
                                                    return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                }
                                        }
                                    }

                                    throw;
                                }
                            }
                            catch (Exception)
                            {
                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                try
                                {
                                    if (NativeWin32API.CreateDirectoryFromPath(SubItemPath, Option, out string NewPath))
                                    {
                                        return await OpenAsync(NewPath);
                                    }
                                }
                                catch (Exception ex) when (ex is not LocationNotAvailableException)
                                {
                                    throw;
                                }

                                if (await GetStorageItemAsync() is StorageFolder Folder)
                                {
                                    switch (Option)
                                    {
                                        case CreateOption.GenerateUniqueName:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.GenerateUniqueName);
                                                return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
                                            }
                                        case CreateOption.OpenIfExist:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.OpenIfExists);
                                                return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
                                            }
                                        case CreateOption.ReplaceExisting:
                                            {
                                                StorageFolder NewFolder = await Folder.CreateFolderAsync(Name, CreationCollisionOption.ReplaceExisting);
                                                return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
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
                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                yield return new FileSystemStorageFolder(await SubFolder.GetNativeFileDataAsync());
                            }
                            else if (Item is StorageFile SubFile)
                            {
                                yield return new FileSystemStorageFile(await SubFile.GetNativeFileDataAsync());
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

                foreach (FileSystemStorageItemBase Item in SubItems.Where((Item) => (AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true))
                                                                   .Where((Item) => (Item is FileSystemStorageFolder && Filter.HasFlag(BasicFilters.Folder)) || (Item is FileSystemStorageFile && Filter.HasFlag(BasicFilters.File))))
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
                        IsReadOnly = Data.IsReadOnly;
                        IsHiddenItem = Data.IsHiddenItem;
                        IsSystemItem = Data.IsSystemItem;
                        ModifiedTime = Data.ModifiedTime;
                        LastAccessTime = Data.LastAccessTime;
                    }
                    else if (await GetStorageItemCoreAsync() is StorageFolder Folder)
                    {
                        ModifiedTime = await Folder.GetModifiedTimeAsync();
                        LastAccessTime = await Folder.GetLastAccessTimeAsync();

                        if (GetBulkAccessSharedController(out var ControllerRef))
                        {
                            using (ControllerRef)
                            {
                                FileAttributes Attribute = await ControllerRef.Value.Controller.GetFileAttributeAsync(Path);

                                IsReadOnly = Attribute.HasFlag(FileAttributes.ReadOnly);
                                IsHiddenItem = Attribute.HasFlag(FileAttributes.Hidden);
                                IsSystemItem = Attribute.HasFlag(FileAttributes.System);
                            }
                        }
                        else
                        {
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                            {
                                FileAttributes Attribute = await ControllerRef.Value.Controller.GetFileAttributeAsync(Path);

                                IsReadOnly = Attribute.HasFlag(FileAttributes.ReadOnly);
                                IsHiddenItem = Attribute.HasFlag(FileAttributes.Hidden);
                                IsSystemItem = Attribute.HasFlag(FileAttributes.System);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(LoadCoreAsync)}");
                }
            }
        }

        protected override async Task<IStorageItem> GetStorageItemCoreAsync()
        {
            try
            {
                return await StorageFolder.GetFolderFromPathAsync(Path);
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

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return await base.GetThumbnailCoreAsync(Mode, ForceUpdate)
                                ?? new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                        ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                        : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            try
            {
                return await base.GetThumbnailRawStreamCoreAsync(Mode, ForceUpdate);
            }
            catch (Exception)
            {
                StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                                            ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                                            : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
                return await ThumbnailFile.OpenReadAsync();
            }
        }

        public override async Task CopyAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Regex.IsMatch(DirectoryPath, @"^(ftp(s)?:\\{1,2}$)|(ftp(s)?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
            {
                FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(DirectoryPath, Name));

                if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                {
                    ulong CurrentPosiion = 0;
                    ulong TotalSize = await GetFolderSizeAsync(CancelToken);

                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                    {
                        switch (Option)
                        {
                            case CollisionOptions.OverrideOnCollision:
                                {
                                    if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExistsAsync(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteDirectoryAsync(TargetAnalysis.RelatedPath, CancelToken));
                                    }

                                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectoryAsync(TargetAnalysis.RelatedPath, true, CancelToken));

                                    await foreach (FileSystemStorageItemBase Item in GetChildItemsAsync(true, true, true, CancelToken))
                                    {
                                        switch (Item)
                                        {
                                            case FileSystemStorageFolder Folder:
                                                {
                                                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectoryAsync(@$"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken));

                                                    break;
                                                }
                                            case FileSystemStorageFile File:
                                                {
                                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync($@"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, File.Path)}", FtpDataType.Binary, CancelToken)))
                                                    using (Stream SourceStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                                    {
                                                        await SourceStream.CopyToAsync(TargetStream, SourceStream.Length, CancelToken, (s, e) =>
                                                        {
                                                            if (TotalSize > 0)
                                                            {
                                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                            }
                                                        });
                                                    }

                                                    CurrentPosiion += File.Size;

                                                    break;
                                                }
                                        }
                                    }

                                    break;
                                }

                            case CollisionOptions.RenameOnCollision:
                                {
                                    string UniquePath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.Folder));

                                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectoryAsync(UniquePath, true, CancelToken));

                                    await foreach (FileSystemStorageItemBase Item in GetChildItemsAsync(true, true, true, CancelToken))
                                    {
                                        switch (Item)
                                        {
                                            case FileSystemStorageFolder Folder:
                                                {
                                                    if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectoryAsync(@$"{UniquePath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken)))
                                                    {
                                                        throw new UnauthorizedAccessException(Folder.Path);
                                                    }

                                                    break;
                                                }
                                            case FileSystemStorageFile File:
                                                {
                                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync($@"{UniquePath}\{System.IO.Path.GetRelativePath(Path, File.Path)}", FtpDataType.Binary, CancelToken)))
                                                    using (Stream SourceStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                                    {
                                                        await SourceStream.CopyToAsync(TargetStream, SourceStream.Length, CancelToken, (s, e) =>
                                                        {
                                                            if (TotalSize > 0)
                                                            {
                                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                            }
                                                        });
                                                    }

                                                    CurrentPosiion += File.Size;

                                                    break;
                                                }
                                        }
                                    }

                                    break;
                                }
                            case CollisionOptions.Skip:
                                {
                                    if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExistsAsync(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        await foreach (FileSystemStorageItemBase Item in GetChildItemsAsync(true, true, true, CancelToken))
                                        {
                                            switch (Item)
                                            {
                                                case FileSystemStorageFolder Folder:
                                                    {
                                                        if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectoryAsync(@$"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken)))
                                                        {
                                                            throw new UnauthorizedAccessException(Folder.Path);
                                                        }

                                                        break;
                                                    }
                                                case FileSystemStorageFile File:
                                                    {
                                                        using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync($@"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, File.Path)}", FtpDataType.Binary, CancelToken)))
                                                        using (Stream SourceStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                                                        {
                                                            await SourceStream.CopyToAsync(TargetStream, SourceStream.Length, CancelToken, (s, e) =>
                                                            {
                                                                if (TotalSize > 0)
                                                                {
                                                                    ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                                }
                                                            });
                                                        }

                                                        CurrentPosiion += File.Size;

                                                        break;
                                                    }
                                            }
                                        }
                                    }

                                    break;
                                }
                        }
                    }
                }
                else
                {
                    throw new Exception($"Could not find the ftp server: {TargetAnalysis.Host}");
                }
            }
            else
            {
                await base.CopyAsync(DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
            }
        }

        public override async Task MoveAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Regex.IsMatch(DirectoryPath, @"^(ftp(s)?:\\{1,2}$)|(ftp(s)?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
            {
                FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(DirectoryPath, Name));

                if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                {
                    await CopyAsync(DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                    await DeleteAsync(true, true, CancelToken);
                }
                else
                {
                    throw new Exception($"Could not find the ftp server: {TargetAnalysis.Host}");
                }
            }
            else
            {
                await base.MoveAsync(DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
            }
        }

        public static explicit operator StorageFolder(FileSystemStorageFolder File)
        {
            return File.StorageItem as StorageFolder;
        }
    }
}
