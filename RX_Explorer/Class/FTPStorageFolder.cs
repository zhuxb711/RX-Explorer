using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
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
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FtpStorageFolder : FileSystemStorageFolder, IFtpStorageItem, INotWin32StorageFolder
    {
        private readonly FtpFileData Data;
        private readonly FtpClientController ClientController;

        public string RelatedPath { get => Data.RelatedPath; }

        public override string DisplayName => RelatedPath == @"\" ? ClientController.ServerHost : base.DisplayName;

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return Task.FromResult(new BitmapImage(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                            ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                            : new Uri("ms-appx:///Assets/FolderIcon_Win10.png")));
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            try
            {
                StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                                                                ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                                                                : new Uri("ms-appx:///Assets/FolderIcon_Win10.png"));
                return await ThumbnailFile.OpenReadAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the raw stream of thumbnail");
            }

            return null;
        }

        protected override Task<IStorageItem> GetStorageItemCoreAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        protected override Task LoadCoreAsync(bool ForceUpdate)
        {
            return Task.CompletedTask;
        }

        public override Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Properties.Select((Prop) => new KeyValuePair<string, string>(Prop, string.Empty))));
        }

        public override async IAsyncEnumerable<FileSystemStorageItemBase> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                             bool IncludeSystemItems = false,
                                                                                             bool IncludeAllSubItems = false,
                                                                                             [EnumeratorCancellation] CancellationToken CancelToken = default,
                                                                                             BasicFilters Filter = BasicFilters.File | BasicFilters.Folder,
                                                                                             Func<string, bool> AdvanceFilter = null)
        {
            IReadOnlyList<FtpListItem> SubItems = await ClientController.RunCommandAsync((Client) => Client.GetListing(RelatedPath, FtpListOption.SizeModify, CancelToken));
            IReadOnlyList<FileSystemStorageItemBase> SubTransformedItems = SubItems.Select<FtpListItem, FileSystemStorageItemBase>((Item) =>
            {
                if (Item.Type.HasFlag(FtpObjectType.Directory))
                {
                    return new FtpStorageFolder(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                }
                else
                {
                    return new FtpStorageFile(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                }
            }).ToArray();

            foreach (FileSystemStorageItemBase Item in SubTransformedItems.Where((Item) => (AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true))
                                                                          .Where((Item) => (Item is FileSystemStorageFolder && Filter.HasFlag(BasicFilters.Folder)) || (Item is FileSystemStorageFile && Filter.HasFlag(BasicFilters.File))))

            {
                yield return Item;
            }

            if (IncludeAllSubItems)
            {
                foreach (FileSystemStorageFolder Item in SubTransformedItems.OfType<FileSystemStorageFolder>())
                {
                    CancelToken.ThrowIfCancellationRequested();

                    await foreach (FileSystemStorageItemBase SubItem in Item.GetChildItemsAsync(IncludeHiddenItems, IncludeSystemItems, IncludeAllSubItems, CancelToken, Filter, AdvanceFilter))
                    {
                        yield return SubItem;
                    }
                }
            }
        }

        public override IAsyncEnumerable<FileSystemStorageItemBase> SearchAsync(string SearchWord,
                                                                                bool SearchInSubFolders = false,
                                                                                bool IncludeHiddenItems = false,
                                                                                bool IncludeSystemItems = false,
                                                                                bool IsRegexExpression = false,
                                                                                bool IsAQSExpression = false,
                                                                                bool UseIndexerOnly = false,
                                                                                bool IgnoreCase = true,
                                                                                CancellationToken CancelToken = default)
        {
            if (IsAQSExpression)
            {
                throw new ArgumentException($"{nameof(IsAQSExpression)} is not supported");
            }

            IAsyncEnumerable<FileSystemStorageItemBase> Result = GetChildItemsAsync(IncludeHiddenItems, IncludeSystemItems, SearchInSubFolders, CancelToken: CancelToken);

            return IsRegexExpression ? Result.Where((Item) => Regex.IsMatch(Item.Name, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None))
                                     : Result.Where((Item) => Item.Name.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        }

        public override Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            return Task.FromResult(new SafeFileHandle(IntPtr.Zero, true));
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        public override async Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, CreateType ItemType, CollisionOptions Option = CollisionOptions.None)
        {
            string TargetPath = System.IO.Path.Combine(RelatedPath, Name);

            try
            {
                if (ItemType == CreateType.Folder)
                {
                    switch (Option)
                    {
                        case CollisionOptions.Skip:
                            {
                                if (!await ClientController.RunCommandAsync((Client) => Client.DirectoryExists(TargetPath)))
                                {
                                    if (!await ClientController.RunCommandAsync((Client) => Client.CreateDirectory(TargetPath)))
                                    {
                                        throw new Exception("Could not create the directory on ftp server");
                                    }
                                }

                                if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(TargetPath, true)) is FtpListItem Item)
                                {
                                    return new FtpStorageFolder(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                }

                                break;
                            }
                        case CollisionOptions.RenameOnCollision:
                            {
                                string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.Folder));

                                if (await ClientController.RunCommandAsync((Client) => Client.CreateDirectory(UniquePath)))
                                {
                                    if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(UniquePath, true)) is FtpListItem Item)
                                    {
                                        return new FtpStorageFolder(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.OverrideOnCollision:
                            {
                                await ClientController.RunCommandAsync((Client) => Client.DeleteDirectory(TargetPath, FtpListOption.Recursive));

                                if (await ClientController.RunCommandAsync((Client) => Client.CreateDirectory(TargetPath)))
                                {
                                    if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FtpStorageFolder(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                    }
                                }

                                break;
                            }
                    }
                }
                else
                {
                    switch (Option)
                    {
                        case CollisionOptions.Skip:
                            {
                                if (await ClientController.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), TargetPath, FtpRemoteExists.Skip)) != FtpStatus.Failed)
                                {
                                    if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FtpStorageFile(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.RenameOnCollision:
                            {
                                string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.File));

                                if (await ClientController.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), UniquePath, FtpRemoteExists.NoCheck)) == FtpStatus.Success)
                                {
                                    if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(UniquePath, true)) is FtpListItem Item)
                                    {
                                        return new FtpStorageFile(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.OverrideOnCollision:
                            {
                                if (await ClientController.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), TargetPath, FtpRemoteExists.Overwrite)) == FtpStatus.Success)
                                {
                                    if (await ClientController.RunCommandAsync((Client) => Client.GetObjectInfo(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FtpStorageFile(ClientController, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(Path, Item.Name)), Item));
                                    }
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CreateNewSubItemAsync)} failed and could not create the storage item, path:\"{TargetPath}\"");
            }

            return null;
        }

        public Task<FtpFileData> GetRawDataAsync()
        {
            return Task.FromResult(Data);
        }

        public override async Task CopyAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.DirectoryExists(RelatedPath)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (Regex.IsMatch(DirectoryPath, @"^ftps?:\\{1,2}[^\\]+.*", RegexOptions.IgnoreCase))
                {
                    FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(TargetPath);

                    if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                    {
                        ulong CurrentPosiion = 0;
                        ulong TotalSize = await GetFolderSizeAsync(CancelToken);

                        switch (Option)
                        {
                            case CollisionOptions.OverrideOnCollision:
                                {
                                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                                    {
                                        if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExists(TargetAnalysis.RelatedPath, CancelToken)))
                                        {
                                            await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteDirectory(TargetAnalysis.RelatedPath, FtpListOption.Recursive, CancelToken));
                                        }

                                        await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(TargetAnalysis.RelatedPath, true, CancelToken));

                                        using (FtpClientController AuxiliaryReadController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
                                        {
                                            await foreach (FileSystemStorageItemBase Item in new FtpStorageFolder(AuxiliaryReadController, Data).GetChildItemsAsync(true, true, true, CancelToken))
                                            {
                                                switch (Item)
                                                {
                                                    case FtpStorageFolder Folder:
                                                        {
                                                            await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(@$"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken));

                                                            break;
                                                        }
                                                    case FtpStorageFile File:
                                                        {
                                                            using (Stream OriginStream = await AuxiliaryReadController.RunCommandAsync((Client) => Client.GetFtpFileStreamForReadAsync(File.RelatedPath, FtpDataType.Binary, 0, (long)File.Size, CancelToken)))
                                                            {
                                                                string RelativePath = Path.Equals(System.IO.Path.GetDirectoryName(File.Path), StringComparison.OrdinalIgnoreCase)
                                                                                          ? string.Empty
                                                                                          : System.IO.Path.GetRelativePath(Path, System.IO.Path.GetDirectoryName(File.Path));

                                                                FtpPathAnalysis InnerTargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(TargetPath, RelativePath, File.Name));

                                                                using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(InnerTargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                                                {
                                                                    await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, (s, e) =>
                                                                    {
                                                                        if (TotalSize > 0)
                                                                        {
                                                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                                        }
                                                                    });
                                                                }
                                                            }

                                                            CurrentPosiion += File.Size;
                                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(CurrentPosiion * 100d / TotalSize)))), null));

                                                            break;
                                                        }
                                                }
                                            }
                                        }
                                    }

                                    break;
                                }
                            case CollisionOptions.RenameOnCollision:
                                {
                                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                                    {
                                        string UniquePath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.Folder));

                                        await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(UniquePath, true, CancelToken));

                                        using (FtpClientController AuxiliaryReadController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
                                        {
                                            await foreach (FileSystemStorageItemBase Item in new FtpStorageFolder(AuxiliaryReadController, Data).GetChildItemsAsync(true, true, true, CancelToken))
                                            {
                                                switch (Item)
                                                {
                                                    case FtpStorageFolder Folder:
                                                        {
                                                            await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(@$"{UniquePath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken));

                                                            break;
                                                        }
                                                    case FtpStorageFile File:
                                                        {
                                                            using (Stream OriginStream = await AuxiliaryReadController.RunCommandAsync((Client) => Client.GetFtpFileStreamForReadAsync(File.RelatedPath, FtpDataType.Binary, 0, (long)File.Size, CancelToken)))
                                                            using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync($@"{UniquePath}\{System.IO.Path.GetRelativePath(Path, File.Path)}", FtpDataType.Binary, CancelToken)))
                                                            {
                                                                await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, (s, e) =>
                                                                {
                                                                    if (TotalSize > 0)
                                                                    {
                                                                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                                    }
                                                                });
                                                            }

                                                            CurrentPosiion += File.Size;
                                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(CurrentPosiion * 100d / TotalSize)))), null));

                                                            break;
                                                        }
                                                }
                                            }
                                        }
                                    }

                                    break;
                                }
                            case CollisionOptions.Skip:
                                {
                                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                                    {
                                        if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExists(TargetAnalysis.RelatedPath, CancelToken)))
                                        {
                                            await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(TargetAnalysis.RelatedPath, true, CancelToken));

                                            using (FtpClientController AuxiliaryReadController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
                                            {
                                                await foreach (FileSystemStorageItemBase Item in new FtpStorageFolder(AuxiliaryReadController, Data).GetChildItemsAsync(true, true, true, CancelToken))
                                                {
                                                    switch (Item)
                                                    {
                                                        case FtpStorageFolder Folder:
                                                            {
                                                                await AuxiliaryWriteController.RunCommandAsync((Client) => Client.CreateDirectory(@$"{TargetAnalysis.RelatedPath}\{System.IO.Path.GetRelativePath(Path, Folder.Path)}", true, CancelToken));

                                                                break;
                                                            }
                                                        case FtpStorageFile File:
                                                            {
                                                                using (Stream OriginStream = await AuxiliaryReadController.RunCommandAsync((Client) => Client.GetFtpFileStreamForReadAsync(File.RelatedPath, FtpDataType.Binary, 0, (long)File.Size, CancelToken)))
                                                                {
                                                                    string RelativePath = Path.Equals(System.IO.Path.GetDirectoryName(File.Path), StringComparison.OrdinalIgnoreCase)
                                                                                              ? string.Empty
                                                                                              : System.IO.Path.GetRelativePath(Path, System.IO.Path.GetDirectoryName(File.Path));

                                                                    FtpPathAnalysis InnerTargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(TargetPath, RelativePath, File.Name));

                                                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(InnerTargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                                                    {
                                                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, (s, e) =>
                                                                        {
                                                                            if (TotalSize > 0)
                                                                            {
                                                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                                            }
                                                                        });
                                                                    }
                                                                }

                                                                CurrentPosiion += File.Size;
                                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(CurrentPosiion * 100d / TotalSize)))), null));

                                                                break;
                                                            }
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    break;
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
                    ulong CurrentPosiion = 0;
                    ulong TotalSize = await GetFolderSizeAsync(CancelToken);

                    if (Option == CollisionOptions.Skip)
                    {
                        if (await CheckExistsAsync(TargetPath))
                        {
                            return;
                        }
                    }

                    if (await CreateNewAsync(TargetPath, CreateType.Folder, Option) is FileSystemStorageFolder NewFolder)
                    {
                        using (FtpClientController AuxiliaryReadController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
                        {
                            await foreach (FileSystemStorageItemBase Item in new FtpStorageFolder(AuxiliaryReadController, Data).GetChildItemsAsync(true, true, true, CancelToken))
                            {
                                switch (Item)
                                {
                                    case FtpStorageFolder Folder:
                                        {
                                            string SubFolderPath = System.IO.Path.Combine(NewFolder.Path, System.IO.Path.GetRelativePath(Path, Folder.Path));

                                            if (await CreateNewAsync(SubFolderPath, CreateType.Folder) is not FileSystemStorageFolder)
                                            {
                                                throw new UnauthorizedAccessException(SubFolderPath);
                                            }

                                            break;
                                        }
                                    case FtpStorageFile File:
                                        {
                                            using (Stream OriginStream = await AuxiliaryReadController.RunCommandAsync((Client) => Client.GetFtpFileStreamForReadAsync(File.RelatedPath, FtpDataType.Binary, 0, (long)File.Size, CancelToken)))
                                            {
                                                string RelativePath = Path.Equals(System.IO.Path.GetDirectoryName(File.Path), StringComparison.OrdinalIgnoreCase)
                                                                          ? string.Empty
                                                                          : System.IO.Path.GetRelativePath(Path, System.IO.Path.GetDirectoryName(File.Path));

                                                if (await CreateNewAsync(System.IO.Path.Combine(NewFolder.Path, RelativePath, File.Name), CreateType.File) is FileSystemStorageFile NewFile)
                                                {
                                                    using (Stream TargetStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
                                                    {
                                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, (s, e) =>
                                                        {
                                                            if (TotalSize > 0)
                                                            {
                                                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling((CurrentPosiion + (e.ProgressPercentage / 100d * File.Size)) * 100 / TotalSize)))), null));
                                                            }
                                                        });
                                                    }
                                                }
                                            }

                                            CurrentPosiion += File.Size;
                                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(CurrentPosiion * 100d / TotalSize)))), null));

                                            break;
                                        }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new UnauthorizedAccessException(TargetPath);
                    }
                }
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task MoveAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.DirectoryExists(RelatedPath)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (Regex.IsMatch(DirectoryPath, @"^ftps?:\\{1,2}[^\\]+.*", RegexOptions.IgnoreCase))
                {
                    FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(TargetPath);

                    if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                    {
                        if (TargetClientController == ClientController)
                        {
                            switch (Option)
                            {
                                case CollisionOptions.None:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.DirectoryExists(RelatedPath, CancelToken)))
                                        {
                                            throw new Exception($"{Path} is already exists");
                                        }

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveDirectory(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.NoCheck, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetAnalysis.Host + TargetAnalysis.RelatedPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveDirectory(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveDirectory(RelatedPath, UniquePath, FtpRemoteExists.NoCheck, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetAnalysis.Host + UniquePath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DirectoryExists(TargetAnalysis.RelatedPath, CancelToken)))
                                        {
                                            if (!await ClientController.RunCommandAsync((Client) => Client.MoveDirectory(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.NoCheck, CancelToken)))
                                            {
                                                throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                            }
                                        }

                                        break;
                                    }
                            }

                            ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(100, null));
                        }
                        else
                        {
                            await CopyAsync(DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                            await DeleteAsync(true, true, CancelToken);
                        }
                    }
                    else
                    {
                        throw new Exception($"Could not find the ftp server: {TargetAnalysis.Host}");
                    }
                }
                else
                {
                    await CopyAsync(DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                    await DeleteAsync(true, true, CancelToken);
                }
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task<string> RenameAsync(string DesireName, bool SkipOperationRecord = false, CancellationToken CancelToken = default)
        {
            using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
            {
                if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExists(RelatedPath, CancelToken)))
                {
                    string TargetPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RelatedPath), DesireName);

                    if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExists(TargetPath, CancelToken)))
                    {
                        TargetPath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.Folder));
                    }

                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.Rename(RelatedPath, TargetPath, CancelToken));

                    return System.IO.Path.GetFileName(TargetPath);
                }

                throw new FileNotFoundException(Path);
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
            {
                if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DirectoryExists(RelatedPath, CancelToken)))
                {
                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteDirectory(RelatedPath, FtpListOption.Recursive, CancelToken));
                }
                else
                {
                    throw new FileNotFoundException(Path);
                }
            }
        }

        public FtpStorageFolder(FtpClientController ClientController, FtpFileData Data) : base(Data)
        {
            this.Data = Data;
            this.ClientController = ClientController;
        }
    }
}
