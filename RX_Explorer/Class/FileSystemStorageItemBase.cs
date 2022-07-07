using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using RX_Explorer.View;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public abstract class FileSystemStorageItemBase : IStorageItemPropertiesBase, INotifyPropertyChanged, IStorageItemOperation, IEquatable<FileSystemStorageItemBase>
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int IsContentLoaded;
        private ThumbnailStatus thumbnailStatus;
        private RefSharedRegion<FullTrustProcessController.Exclusive> ControllerSharedRef;

        public string Path { get; protected set; }

        public virtual string Name => System.IO.Path.GetFileName(Path) ?? string.Empty;

        public virtual string Type => System.IO.Path.GetExtension(Path)?.ToUpper() ?? string.Empty;

        public abstract string DisplayName { get; }

        public abstract string DisplayType { get; }

        public virtual ulong Size { get; protected set; }

        public virtual DateTimeOffset CreationTime { get; protected set; }

        public virtual DateTimeOffset ModifiedTime { get; protected set; }

        public virtual DateTimeOffset LastAccessTime { get; protected set; }

        public virtual BitmapImage Thumbnail { get; protected set; }

        public virtual BitmapImage ThumbnailOverlay { get; protected set; }

        public virtual bool IsReadOnly { get; protected set; }

        public virtual bool IsSystemItem { get; protected set; }

        public virtual bool IsHiddenItem { get; protected set; }

        public virtual SyncStatus SyncStatus { get; protected set; }

        protected IStorageItem StorageItem { get; private set; }

        protected ThumbnailMode ThumbnailMode { get; private set; } = ThumbnailMode.ListView;

        protected virtual bool ShouldGenerateThumbnail => (this is FileSystemStorageFile && SettingPage.ContentLoadMode == LoadMode.OnlyFile) || SettingPage.ContentLoadMode == LoadMode.All;

        public LabelKind Label
        {
            get => SQLite.Current.GetLabelKindFromPath(Path);
            set
            {
                SQLite.Current.SetLabelKindByPath(Path, value);
                OnPropertyChanged();
            }
        }

        public ThumbnailStatus ThumbnailStatus
        {
            get
            {
                if (IsHiddenItem)
                {
                    return ThumbnailStatus.HalfOpacity;
                }

                return thumbnailStatus;
            }
            set
            {
                if (!IsHiddenItem && thumbnailStatus != value)
                {
                    thumbnailStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public static Task<IDisposable> SelfCreateBulkAccessSharedControllerAsync<T>(T Item, CancellationToken CancelToken = default, PriorityLevel Priority = PriorityLevel.Normal) where T : FileSystemStorageItemBase
        {
            return SelfCreateBulkAccessSharedControllerAsync(new T[] { Item }, CancelToken, Priority);
        }

        public static async Task<IDisposable> SelfCreateBulkAccessSharedControllerAsync<T>(IEnumerable<T> Items, CancellationToken CancelToken = default, PriorityLevel Priority = PriorityLevel.Normal) where T : FileSystemStorageItemBase
        {
            if (Items.Any())
            {
                FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync(CancelToken, Priority);
                RefSharedRegion<FullTrustProcessController.Exclusive> SharedRef = new RefSharedRegion<FullTrustProcessController.Exclusive>(Exclusive, true);

                foreach (T Item in Items)
                {
                    Item.SetBulkAccessSharedController(SharedRef);
                }

                return new DisposeNotification(() =>
                {
                    SharedRef.Dispose();
                });
            }
            else
            {
                throw new ArgumentException("Input items should not be empty", nameof(Items));
            }
        }

        public static IDisposable SetBulkAccessSharedController<T>(T Item, FullTrustProcessController.Exclusive Exclusive) where T : FileSystemStorageItemBase
        {
            return SetBulkAccessSharedController(new T[] { Item }, Exclusive);
        }

        public static IDisposable SetBulkAccessSharedController<T>(IEnumerable<T> Items, FullTrustProcessController.Exclusive Exclusive) where T : FileSystemStorageItemBase
        {
            if (Items.Any())
            {
                RefSharedRegion<FullTrustProcessController.Exclusive> SharedRef = new RefSharedRegion<FullTrustProcessController.Exclusive>(Exclusive, false);

                foreach (T Item in Items)
                {
                    Item.SetBulkAccessSharedController(SharedRef);
                }

                return new DisposeNotification(() =>
                {
                    SharedRef.Dispose();
                });
            }
            else
            {
                throw new ArgumentException("Input items should not be empty", nameof(Items));
            }
        }

        public static async Task<bool> CheckExistsAsync(string Path)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                        {
                            return await Exclusive.Controller.MTPCheckExistsAsync(Path);
                        }
                    }
                    else if (Path.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                             || Path.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPathAnalysis Analysis = new FTPPathAnalysis(Path);

                        if (await FTPClientManager.GetClientControllerAsync(Analysis) is FTPClientController Controller)
                        {
                            if (Analysis.IsRootDirectory
                                || await Controller.RunCommandAsync((Client) => Client.DirectoryExistsAsync(Analysis.RelatedPath))
                                || await Controller.RunCommandAsync((Client) => Client.FileExistsAsync(Analysis.RelatedPath)))
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            return await Task.Run(() => NativeWin32API.CheckExists(Path));
                        }
                        catch (LocationNotAvailableException)
                        {
                            string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                            if (string.IsNullOrEmpty(DirectoryPath))
                            {
                                await StorageFolder.GetFolderFromPathAsync(Path);
                                return true;
                            }
                            else
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                                if (await Folder.TryGetItemAsync(System.IO.Path.GetFileName(Path)) != null)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (ArgumentException)
                {
                    //No need to handle this exception
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(CheckExistsAsync)}, path: {Path}");
                }
            }

            return false;
        }

        public static async IAsyncEnumerable<FileSystemStorageItemBase> OpenInBatchAsync(IEnumerable<string> PathArray, [EnumeratorCancellation] CancellationToken CancelToken = default)
        {
            using (FullTrustProcessController.LazyExclusive LazyExclusive = FullTrustProcessController.GetLazyControllerExclusive())
            {
                foreach (string Path in PathArray)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        yield break;
                    }

                    if (await OpenCoreAsync(Path, LazyExclusive) is FileSystemStorageItemBase Item)
                    {
                        yield return Item;
                    }
                    else
                    {
                        throw new FileNotFoundException(Path);
                    }
                }
            }
        }

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            try
            {
                using (FullTrustProcessController.LazyExclusive LazyExclusive = FullTrustProcessController.GetLazyControllerExclusive())
                {
                    return await OpenCoreAsync(Path, LazyExclusive);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not open the specific file: {Path}");
            }

            return null;
        }

        private static async Task<FileSystemStorageItemBase> OpenCoreAsync(string Path, FullTrustProcessController.LazyExclusive LazyExclusive)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        FullTrustProcessController Controller = await LazyExclusive.GetRealControllerAsync();

                        if (await Controller.GetMTPItemDataAsync(Path) is MTPFileData Data)
                        {
                            if (Data.Attributes.HasFlag(System.IO.FileAttributes.Directory))
                            {
                                return new MTPStorageFolder(Data);
                            }
                            else
                            {
                                return new MTPStorageFile(Data);
                            }
                        }
                    }
                    else if (Path.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                             || Path.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPathAnalysis Analysis = new FTPPathAnalysis(Path);

                        if (await FTPClientManager.GetClientControllerAsync(Analysis) is FTPClientController FTPController)
                        {
                            if (Analysis.IsRootDirectory)
                            {
                                return new FTPStorageFolder(FTPController, new FTPFileData(Path));
                            }
                            else if (await FTPController.RunCommandAsync((Client) => Client.GetObjectInfoAsync(Analysis.RelatedPath, true)) is FtpListItem Item)
                            {
                                if (Item.Type.HasFlag(FtpFileSystemObjectType.Directory))
                                {
                                    return new FTPStorageFolder(FTPController, new FTPFileData(Path, Item));
                                }
                                else
                                {
                                    return new FTPStorageFile(FTPController, new FTPFileData(Path, Item));
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            return await Task.Run(() => NativeWin32API.GetStorageItem(Path));
                        }
                        catch (LocationNotAvailableException)
                        {
                            try
                            {
                                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                if (string.IsNullOrEmpty(DirectoryPath))
                                {
                                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                                    return new FileSystemStorageFolder(await Folder.GetNativeFileDataAsync());
                                }
                                else
                                {
                                    StorageFolder ParentFolder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                                    switch (await ParentFolder.TryGetItemAsync(System.IO.Path.GetFileName(Path)))
                                    {
                                        case StorageFolder Folder:
                                            {
                                                return new FileSystemStorageFolder(await Folder.GetNativeFileDataAsync());
                                            }
                                        case StorageFile File:
                                            {
                                                return new FileSystemStorageFile(await File.GetNativeFileDataAsync());
                                            }
                                        default:
                                            {
                                                LogTracer.Log($"UWP storage API could not found the path: \"{Path}\"");
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception ex) when (ex is not (ArgumentException or FileNotFoundException or DirectoryNotFoundException))
                            {
                                FullTrustProcessController Controller = await LazyExclusive.GetRealControllerAsync();

                                using (SafeFileHandle Handle = await Controller.GetNativeHandleAsync(Path, AccessMode.ReadWrite, OptimizeOption.None))
                                {
                                    if (Handle.IsInvalid)
                                    {
                                        LogTracer.Log($"Could not get native handle and failed to get the storage item, path: \"{Path}\"");
                                    }
                                    else
                                    {
                                        return NativeWin32API.GetStorageItemFromHandle(Path, Handle.DangerousGetHandle());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (ArgumentException)
                {
                    //No need to handle this exception
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(OpenAsync)} failed and could not get the storage item, path:\"{Path}\"");
                }
            }

            return null;
        }

        public static async Task<Stream> CreateLocalOneTimeFileStreamAsync(string TempFilePath = null)
        {
            SafeFileHandle Handle = NativeWin32API.CreateLocalOneTimeFileHandle(TempFilePath);

            if (Handle.IsInvalid)
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    Handle = await Exclusive.Controller.CreateLocalOneTimeFileHandleAsync(TempFilePath);
                }
            }

            if (Handle.IsInvalid)
            {
                throw new UnauthorizedAccessException();
            }

            return new FileStream(Handle, FileAccess.ReadWrite, 4096, true);
        }

        public static async Task<FileSystemStorageItemBase> CreateNewAsync(string Path, CreateType ItemType, CreateOption Option)
        {
            try
            {
                if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                    {
                        MTPFileData Data = await Exclusive.Controller.MTPCreateSubItemAsync(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(Path), ItemType, Option);

                        if (Data != null)
                        {
                            switch (ItemType)
                            {
                                case CreateType.File:
                                    {
                                        return new MTPStorageFile(Data);
                                    }
                                case CreateType.Folder:
                                    {
                                        return new MTPStorageFolder(Data);
                                    }
                            }
                        }
                    }
                }
                else if (Path.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                         || Path.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
                {
                    FTPPathAnalysis Analysis = new FTPPathAnalysis(Path);

                    if (await FTPClientManager.GetClientControllerAsync(Analysis) is FTPClientController Controller)
                    {
                        if (ItemType == CreateType.Folder)
                        {
                            switch (Option)
                            {
                                case CreateOption.OpenIfExist:
                                    {
                                        await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(Analysis.RelatedPath));

                                        if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(Analysis.RelatedPath, true)) is FtpListItem Item)
                                        {
                                            return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
                                        }

                                        break;
                                    }
                                case CreateOption.GenerateUniqueName:
                                    {
                                        string UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(Analysis.RelatedPath, CreateType.Folder));

                                        if (await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(UniquePath)))
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(UniquePath, true)) is FtpListItem Item)
                                            {
                                                return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CreateOption.ReplaceExisting:
                                    {
                                        await Controller.RunCommandAsync((Client) => Client.DeleteDirectoryAsync(Analysis.RelatedPath));

                                        if (await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(Analysis.RelatedPath)))
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
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
                                case CreateOption.OpenIfExist:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), Analysis.RelatedPath, FtpRemoteExists.Skip)) != FtpStatus.Failed)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CreateOption.GenerateUniqueName:
                                    {
                                        string UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(Analysis.RelatedPath, CreateType.File));

                                        if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), UniquePath, FtpRemoteExists.NoCheck)) == FtpStatus.Success)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(UniquePath, true)) is FtpListItem Item)
                                            {
                                                return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CreateOption.ReplaceExisting:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), Analysis.RelatedPath, FtpRemoteExists.Overwrite)) == FtpStatus.Success)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name), Item));
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
                else
                {
                    switch (ItemType)
                    {
                        case CreateType.File:
                            {
                                try
                                {
                                    try
                                    {
                                        if (NativeWin32API.CreateFileFromPath(Path, Option, out string NewFilePath))
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
                                        string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                        if (!string.IsNullOrEmpty(DirectoryPath))
                                        {
                                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                                            switch (Option)
                                            {
                                                case CreateOption.GenerateUniqueName:
                                                    {
                                                        StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                                        return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                    }
                                                case CreateOption.OpenIfExist:
                                                    {
                                                        StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                                        return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                    }
                                                case CreateOption.ReplaceExisting:
                                                    {
                                                        StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                                        return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                                    }
                                            }
                                        }

                                        throw;
                                    }
                                }
                                catch (Exception)
                                {
                                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                                    {
                                        string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.File, Path);

                                        if (string.IsNullOrEmpty(NewItemPath))
                                        {
                                            LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{Path}\"");
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
                                        if (NativeWin32API.CreateDirectoryFromPath(Path, Option, out string NewPath))
                                        {
                                            return await OpenAsync(NewPath);
                                        }
                                    }
                                    catch (Exception ex) when (ex is not LocationNotAvailableException)
                                    {
                                        throw;
                                    }

                                    string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                    if (string.IsNullOrEmpty(DirectoryPath))
                                    {
                                        throw new LocationNotAvailableException();
                                    }
                                    else
                                    {
                                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                                        switch (Option)
                                        {
                                            case CreateOption.GenerateUniqueName:
                                                {
                                                    StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                                    return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
                                                }
                                            case CreateOption.OpenIfExist:
                                                {
                                                    StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                                    return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
                                                }
                                            case CreateOption.ReplaceExisting:
                                                {
                                                    StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
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
                                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                                    {
                                        string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.Folder, Path);

                                        if (string.IsNullOrEmpty(NewItemPath))
                                        {
                                            LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{Path}\"");
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CreateNewAsync)} failed and could not create the storage item, path:\"{Path}\"");
            }

            return null;
        }

        public static async Task CopyAsync(IEnumerable<string> CopyFrom, string CopyTo, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (CopyTo.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                || CopyTo.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)
                || CopyFrom.All((Item) => Item.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase) || Item.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = CopyFrom.Count();

                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(CopyFrom, CancelToken))
                    {
                        using (IDisposable Disposable = SetBulkAccessSharedController(Item, Exclusive))
                        {
                            await Item.CopyAsync(CopyTo, Option, CancelToken, (s, e) =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                            });
                        }

                        Progress += 100;
                    }
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.CopyAsync(CopyFrom, CopyTo, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task MoveAsync(IEnumerable<string> MoveFrom, string MoveTo, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (MoveTo.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                || MoveTo.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)
                || MoveFrom.All((Item) => Item.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase) || Item.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = MoveFrom.Count();

                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(MoveFrom, CancelToken))
                    {
                        using (IDisposable Disposable = SetBulkAccessSharedController(Item, Exclusive))
                        {
                            await Item.MoveAsync(MoveTo, Option, CancelToken, (s, e) =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                            });
                        }

                        Progress += 100;
                    }
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.MoveAsync(MoveFrom, MoveTo, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task DeleteAsync(IEnumerable<string> DeleteFrom, bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (DeleteFrom.All((Item) => Item.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase) || Item.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = DeleteFrom.Count();

                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(DeleteFrom, CancelToken))
                    {
                        using (IDisposable Disposable = SetBulkAccessSharedController(Item, Exclusive))
                        {
                            await Item.DeleteAsync(PermanentDelete, CancelToken, (s, e) =>
                            {
                                ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                            });
                        }

                        Progress += 100;
                    }
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.DeleteAsync(DeleteFrom, PermanentDelete, true, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task<string> RenameAsync(string Path, string DesireName, CancellationToken CancelToken = default)
        {
            if (Path.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                || Path.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
            {
                if (await OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    return await Item.RenameAsync(DesireName, CancelToken);
                }
                else
                {
                    throw new FileNotFoundException(Path);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await Exclusive.Controller.RenameAsync(Path, DesireName, true, CancelToken);
                }
            }
        }

        public async Task SetThumbnailModeAsync(ThumbnailMode Mode)
        {
            if (ThumbnailMode != Mode)
            {
                ThumbnailMode = Mode;

                if (ShouldGenerateThumbnail)
                {
                    try
                    {
                        Thumbnail = await GetThumbnailAsync(Mode, true);
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(SetThumbnailModeAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Thumbnail));
                    }
                }
            }
        }

        public async Task LoadAsync(CancellationToken CancelToken = default)
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                {
                    try
                    {
                        using (IDisposable Disposable = await SelfCreateBulkAccessSharedControllerAsync(this, CancelToken, PriorityLevel.Low))
                        {
                            await GetStorageItemAsync();
                            await LoadCoreAsync(false);

                            List<Task> ParallelLoadTasks = new List<Task>()
                            {
                                GetThumbnailOverlayAsync(),
                            };

                            if (ShouldGenerateThumbnail)
                            {
                                ParallelLoadTasks.Add(GetThumbnailAsync(ThumbnailMode));
                            }

                            if (SpecialPath.IsPathIncluded(Path, SpecialPathEnum.OneDrive)
                                || SpecialPath.IsPathIncluded(Path, SpecialPathEnum.Dropbox))
                            {
                                ParallelLoadTasks.Add(GetSyncStatusAsync());
                            }

                            CancelToken.ThrowIfCancellationRequested();

                            await Task.WhenAll(ParallelLoadTasks);
                        }
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(DisplayName));
                        OnPropertyChanged(nameof(ModifiedTime));
                        OnPropertyChanged(nameof(LastAccessTime));
                        OnPropertyChanged(nameof(ThumbnailOverlay));
                        OnPropertyChanged(nameof(SyncStatus));

                        if (ShouldGenerateThumbnail)
                        {
                            OnPropertyChanged(nameof(Thumbnail));
                        }

                        if (this is FileSystemStorageFile)
                        {
                            OnPropertyChanged(nameof(Size));
                            OnPropertyChanged(nameof(DisplayType));
                        }
                    }
                });
            }
        }

        public async Task RefreshAsync()
        {
            try
            {
                using (IDisposable Disposable = await SelfCreateBulkAccessSharedControllerAsync(this, Priority: PriorityLevel.Low))
                {
                    await GetStorageItemAsync(true);

                    if (ShouldGenerateThumbnail)
                    {
                        await Task.WhenAll(LoadCoreAsync(true), GetThumbnailAsync(ThumbnailMode, true));
                    }
                    else
                    {
                        await LoadCoreAsync(true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not refresh the {GetType().FullName}, path: {Path}");
            }
            finally
            {
                if (this is FileSystemStorageFile)
                {
                    OnPropertyChanged(nameof(Size));
                }

                if (ShouldGenerateThumbnail)
                {
                    OnPropertyChanged(nameof(Thumbnail));
                }

                OnPropertyChanged(nameof(ThumbnailStatus));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(LastAccessTime));
            }
        }

        public virtual async Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                SafeFileHandle Handle = await Task.Run(() => Item.GetSafeFileHandleAsync(Mode, Option));

                if (!Handle.IsInvalid)
                {
                    return Handle;
                }
            }

            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return await ControllerRef.Value.Controller.GetNativeHandleAsync(Path, Mode, Option);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await Exclusive.Controller.GetNativeHandleAsync(Path, Mode, Option);
                }
            }
        }

        public async Task<IStorageItem> GetStorageItemAsync(bool ForceUpdate = false)
        {
            if (!IsHiddenItem && !IsSystemItem)
            {
                if (StorageItem == null || ForceUpdate)
                {
                    StorageItem = await GetStorageItemCoreAsync();
                }
            }

            return StorageItem;
        }

        public async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (Thumbnail == null || ForceUpdate || !string.IsNullOrEmpty(Thumbnail.UriSource?.AbsoluteUri))
            {
                Thumbnail = await GetThumbnailCoreAsync(Mode, ForceUpdate);
            }

            return Thumbnail;
        }

        public async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return await GetThumbnailRawStreamCoreAsync(Mode, ForceUpdate);
        }

        public virtual async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            async Task<IReadOnlyDictionary<string, string>> GetPropertiesCoreAsync(IEnumerable<string> Properties)
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await ControllerRef.Value.Controller.GetPropertiesAsync(Path, Properties);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                    {
                        return await Exclusive.Controller.GetPropertiesAsync(Path, Properties);
                    }
                }
            }

            IEnumerable<string> DistinctProperties = Properties.Distinct();

            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                try
                {
                    Dictionary<string, string> Result = new Dictionary<string, string>();

                    BasicProperties Basic = await Item.GetBasicPropertiesAsync();
                    IDictionary<string, object> UwpResult = await Basic.RetrievePropertiesAsync(DistinctProperties);

                    List<string> MissingKeys = new List<string>(DistinctProperties.Except(UwpResult.Keys));

                    foreach (KeyValuePair<string, object> Pair in UwpResult)
                    {
                        string Value = Pair.Value switch
                        {
                            IEnumerable<string> Array => string.Join(", ", Array),
                            _ => Convert.ToString(Pair.Value)
                        };

                        if (string.IsNullOrEmpty(Value))
                        {
                            MissingKeys.Add(Pair.Key);
                        }
                        else
                        {
                            Result.Add(Pair.Key, Value);
                        }
                    }

                    if (MissingKeys.Count > 0)
                    {
                        Result.AddRange(await GetPropertiesCoreAsync(MissingKeys));
                    }

                    return Result;
                }
                catch
                {
                    return await GetPropertiesCoreAsync(DistinctProperties);
                }
            }
            else
            {
                return await GetPropertiesCoreAsync(DistinctProperties);
            }
        }

        public virtual async Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.MoveAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler); ;
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.MoveAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public virtual async Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.CopyAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.CopyAsync(Path, DirectoryPath, Option, true, CancelToken, ProgressHandler);
                }
            }
        }

        public async virtual Task<string> RenameAsync(string DesireName, CancellationToken CancelToken = default)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    string NewName = await ControllerRef.Value.Controller.RenameAsync(Path, DesireName, true, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    string NewName = await Exclusive.Controller.RenameAsync(Path, DesireName, true, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
        }

        public virtual async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.DeleteAsync(Path, PermanentDelete, true, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, true, CancelToken, ProgressHandler);
                }
            }
        }

        protected FileSystemStorageItemBase(NativeFileData Data) : this(Data?.Path)
        {
            if ((Data?.IsDataValid).GetValueOrDefault())
            {
                Size = Data.Size;
                StorageItem = Data.StorageItem;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = Data.LastAccessTime;
            }
        }

        protected FileSystemStorageItemBase(MTPFileData Data) : this(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }

        protected FileSystemStorageItemBase(FTPFileData Data) : this(Data?.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                IsHiddenItem = Data.IsHiddenItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
                LastAccessTime = DateTimeOffset.MinValue;
            }
        }

        protected FileSystemStorageItemBase(string Path)
        {
            this.Path = Path;
        }

        protected void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        protected void SetBulkAccessSharedController(RefSharedRegion<FullTrustProcessController.Exclusive> SharedRef)
        {
            if (Interlocked.Exchange(ref ControllerSharedRef, SharedRef) is RefSharedRegion<FullTrustProcessController.Exclusive> PreviousRef)
            {
                PreviousRef.Dispose();
            }
        }

        protected bool GetBulkAccessSharedController(out RefSharedRegion<FullTrustProcessController.Exclusive> SharedController)
        {
            return (SharedController = ControllerSharedRef?.CreateNew()) != null;
        }

        protected virtual async Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            async Task<BitmapImage> GetThumbnailOverlayCoreAsync(FullTrustProcessController.Exclusive Exclusive)
            {
                byte[] ThumbnailOverlayByteArray = await Exclusive.Controller.GetThumbnailOverlayAsync(Path);

                if (ThumbnailOverlayByteArray.Length > 0)
                {
                    return await Helper.CreateBitmapImageAsync(ThumbnailOverlayByteArray);
                }

                return null;
            }

            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(ControllerRef.Value);
                }
            }
            else
            {
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(Exclusive);
                }
            }
        }

        protected abstract Task LoadCoreAsync(bool ForceUpdate);

        protected abstract Task<IStorageItem> GetStorageItemCoreAsync();

        protected virtual async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            try
            {
                if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
                {
                    if (await Item.GetThumbnailBitmapAsync(Mode) is BitmapImage LocalThumbnail)
                    {
                        return LocalThumbnail;
                    }
                }

                try
                {
                    if (GetBulkAccessSharedController(out var ControllerRef))
                    {
                        using (ControllerRef)
                        using (IRandomAccessStream ThumbnailStream = await ControllerRef.Value.Controller.GetThumbnailAsync(Path))
                        {
                            return await Helper.CreateBitmapImageAsync(ThumbnailStream);
                        }
                    }
                    else
                    {
                        using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                        using (IRandomAccessStream ThumbnailStream = await Exclusive.Controller.GetThumbnailAsync(Path))
                        {
                            return await Helper.CreateBitmapImageAsync(ThumbnailStream);
                        }
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get thumbnail of path: \"{Path}\"");
            }

            return null;
        }

        protected virtual async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
            {
                return await Item.GetThumbnailRawStreamAsync(Mode);
            }
            else
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await ControllerRef.Value.Controller.GetThumbnailAsync(Path);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
                    {
                        return await Exclusive.Controller.GetThumbnailAsync(Path);
                    }
                }
            }
        }

        private async Task GetSyncStatusAsync()
        {
            IReadOnlyDictionary<string, string> Properties = await GetPropertiesAsync(new string[] { "System.FilePlaceholderStatus", "System.FileOfflineAvailabilityStatus" });

            if (string.IsNullOrEmpty(Properties["System.FilePlaceholderStatus"]) && string.IsNullOrEmpty(Properties["System.FileOfflineAvailabilityStatus"]))
            {
                SyncStatus = SyncStatus.Unknown;
            }
            else
            {
                int StatusIndex = string.IsNullOrEmpty(Properties["System.FilePlaceholderStatus"]) ? Convert.ToInt32(Properties["System.FileOfflineAvailabilityStatus"])
                                                                                                   : Convert.ToInt32(Properties["System.FilePlaceholderStatus"]);
                SyncStatus = StatusIndex switch
                {
                    0 or 1 or 8 => SyncStatus.AvailableOnline,
                    2 or 3 or 14 or 15 => SyncStatus.AvailableOffline,
                    9 => SyncStatus.Sync,
                    4 => SyncStatus.Excluded,
                    _ => SyncStatus.Unknown
                };
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            return obj is FileSystemStorageItemBase Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public bool Equals(FileSystemStorageItemBase other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                return other.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool operator ==(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator !=(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is not null;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
