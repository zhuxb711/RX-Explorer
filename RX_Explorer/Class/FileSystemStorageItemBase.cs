using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using RX_Explorer.View;
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
    public abstract class FileSystemStorageItemBase : IStorageItemBaseProperties, IStorageItemOperation, IEquatable<FileSystemStorageItemBase>
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int IsContentLoaded;
        private ThumbnailStatus thumbnailStatus;
        private RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> ControllerSharedRef;

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
                if (value == LabelKind.None)
                {
                    SQLite.Current.DeleteLabelKindByPath(Path);
                }
                else
                {
                    SQLite.Current.SetLabelKindByPath(Path, value);
                }

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
                AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(CancelToken, Priority);
                RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> SharedRef = new RefSharedRegion<AuxiliaryTrustProcessController.Exclusive>(Exclusive, true);

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

        public static IDisposable SetBulkAccessSharedController<T>(T Item, AuxiliaryTrustProcessController.Exclusive Exclusive) where T : FileSystemStorageItemBase
        {
            return SetBulkAccessSharedController(new T[] { Item }, Exclusive);
        }

        public static IDisposable SetBulkAccessSharedController<T>(IEnumerable<T> Items, AuxiliaryTrustProcessController.Exclusive Exclusive) where T : FileSystemStorageItemBase
        {
            if (Items.Any())
            {
                RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> SharedRef = new RefSharedRegion<AuxiliaryTrustProcessController.Exclusive>(Exclusive, false);

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
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        {
                            return await Exclusive.Controller.MTPCheckExistsAsync(Path);
                        }
                    }
                    else if (Regex.IsMatch(Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
                    {
                        FtpPathAnalysis Analysis = new FtpPathAnalysis(Path);

                        if (await FtpClientManager.GetClientControllerAsync(Analysis) is FtpClientController Controller)
                        {
                            if (Analysis.IsRootDirectory
                                || await Controller.RunCommandAsync((Client) => Client.DirectoryExists(Analysis.RelatedPath))
                                || await Controller.RunCommandAsync((Client) => Client.FileExists(Analysis.RelatedPath))
                                || await Controller.RunCommandAsync((Client) => Client.GetNameListing(Analysis.RelatedPath)
                                                                                      .ContinueWith((PreviousTask) =>
                                                                                      {
                                                                                          if (PreviousTask.Exception != null)
                                                                                          {
                                                                                              return false;
                                                                                          }
                                                                                          else
                                                                                          {
                                                                                              return PreviousTask.Result.Length > 0;
                                                                                          }
                                                                                      }, TaskContinuationOptions.ExecuteSynchronously)))
                            {
                                return true;
                            }
                        }
                    }
                    else if (RootVirtualFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)
                             || LabelCollectionVirtualFolder.TryGetFolderFromPath(Path, out _))
                    {
                        return true;
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
            if (PathArray.Any())
            {
                using (AuxiliaryTrustProcessController.LazyExclusive LazyExclusive = AuxiliaryTrustProcessController.GetLazyControllerExclusive())
                {
                    foreach (string Path in PathArray)
                    {
                        CancelToken.ThrowIfCancellationRequested();
                        yield return await OpenCoreAsync(Path, LazyExclusive);
                    }
                }
            }
        }

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            try
            {
                using (AuxiliaryTrustProcessController.LazyExclusive LazyExclusive = AuxiliaryTrustProcessController.GetLazyControllerExclusive())
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

        private static async Task<FileSystemStorageItemBase> OpenCoreAsync(string Path, AuxiliaryTrustProcessController.LazyExclusive LazyExclusive)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        AuxiliaryTrustProcessController Controller = await LazyExclusive.GetRealControllerAsync();

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
                    else if (Regex.IsMatch(Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
                    {
                        FtpPathAnalysis Analysis = new FtpPathAnalysis(Path);

                        if ((await FtpClientManager.GetClientControllerAsync(Analysis)
                             ?? await FtpClientManager.CreateClientControllerAsync(Analysis)) is FtpClientController Controller)
                        {
                            if (Analysis.IsRootDirectory)
                            {
                                return new FtpStorageFolder(Controller, new FtpFileData(Analysis));
                            }
                            else if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                            {
                                if (Item.Type.HasFlag(FtpObjectType.Directory))
                                {
                                    return new FtpStorageFolder(Controller, new FtpFileData(Analysis, Item));
                                }
                                else
                                {
                                    return new FtpStorageFile(Controller, new FtpFileData(Analysis, Item));
                                }
                            }
                            else if (await Controller.RunCommandAsync((Client) => Client.GetNameListing(Analysis.RelatedPath)
                                                                                        .ContinueWith((PreviousTask) =>
                                                                                        {
                                                                                            if (PreviousTask.Exception != null)
                                                                                            {
                                                                                                return false;
                                                                                            }
                                                                                            else
                                                                                            {
                                                                                                return PreviousTask.Result.Length > 0;
                                                                                            }
                                                                                        }, TaskContinuationOptions.ExecuteSynchronously)))
                            {
                                return new FtpStorageFolder(Controller, new FtpFileData(Analysis));
                            }
                        }
                    }
                    else if (RootVirtualFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
                    {
                        return RootVirtualFolder.Current;
                    }
                    else if (LabelCollectionVirtualFolder.TryGetFolderFromPath(Path, out LabelCollectionVirtualFolder LabelFolder))
                    {
                        return LabelFolder;
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
                                AuxiliaryTrustProcessController Controller = await LazyExclusive.GetRealControllerAsync();

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
                    LogTracer.Log(ex, $"{nameof(OpenCoreAsync)} failed and could not get the storage item, path:\"{Path}\"");
                }
            }

            return null;
        }

        public static async Task<Stream> CreateTemporaryFileStreamAsync(string TempFilePath = null, IOPreference Preference = IOPreference.NoPreference)
        {
            SafeFileHandle Handle = NativeWin32API.CreateTemporaryFileHandle(TempFilePath, Preference);

            if (Handle.IsInvalid)
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    Handle = await Exclusive.Controller.CreateTemporaryFileHandleAsync(TempFilePath, Preference);
                }
            }

            if (Handle.IsInvalid)
            {
                throw new UnauthorizedAccessException();
            }

            return new FileStream(Handle, FileAccess.ReadWrite, 4096, true);
        }

        public static async Task<FileSystemStorageItemBase> CreateNewAsync(string Path, CreateType ItemType, CollisionOptions Option = CollisionOptions.None)
        {
            try
            {
                if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                else if (Regex.IsMatch(Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
                {
                    FtpPathAnalysis Analysis = new FtpPathAnalysis(Path);

                    if (await FtpClientManager.GetClientControllerAsync(Analysis) is FtpClientController Controller)
                    {
                        if (ItemType == CreateType.Folder)
                        {
                            switch (Option)
                            {
                                case CollisionOptions.None:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.DirectoryExists(Analysis.RelatedPath)))
                                        {
                                            throw new Exception($"{Analysis.Path} is already exists");
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.CreateDirectory(Analysis.RelatedPath)))
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFolder(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (!await Controller.RunCommandAsync((Client) => Client.DirectoryExists(Analysis.RelatedPath)))
                                        {
                                            await Controller.RunCommandAsync((Client) => Client.CreateDirectory(Analysis.RelatedPath));
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                        {
                                            return new FtpStorageFolder(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = Analysis.RelatedPath;

                                        if (await Controller.RunCommandAsync((Client) => Client.DirectoryExists(UniquePath)))
                                        {
                                            UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(UniquePath, CreateType.Folder));
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.CreateDirectory(UniquePath)))
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(UniquePath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFolder(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.DirectoryExists(Analysis.RelatedPath)))
                                        {
                                            await Controller.RunCommandAsync((Client) => Client.DeleteDirectory(Analysis.RelatedPath, FtpListOption.Recursive));
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.CreateDirectory(Analysis.RelatedPath)))
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFolder(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
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
                                case CollisionOptions.None:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.FileExists(Analysis.RelatedPath)))
                                        {
                                            throw new Exception($"{Analysis.Path} is already exists");
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), Analysis.RelatedPath, FtpRemoteExists.NoCheck)) == FtpStatus.Success)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFile(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), Analysis.RelatedPath, FtpRemoteExists.Skip)) != FtpStatus.Failed)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFile(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = Analysis.RelatedPath;

                                        if (await Controller.RunCommandAsync((Client) => Client.FileExists(UniquePath)))
                                        {
                                            UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(UniquePath, CreateType.File));
                                        }

                                        if (await Controller.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), UniquePath, FtpRemoteExists.NoCheck)) == FtpStatus.Success)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(UniquePath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFile(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
                                            }
                                        }

                                        break;
                                    }
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await Controller.RunCommandAsync((Client) => Client.UploadBytes(Array.Empty<byte>(), Analysis.RelatedPath, FtpRemoteExists.Overwrite)) == FtpStatus.Success)
                                        {
                                            if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfo(Analysis.RelatedPath, true)) is FtpListItem Item)
                                            {
                                                return new FtpStorageFile(Controller, new FtpFileData(new FtpPathAnalysis(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), Item.Name)), Item));
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

                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), Option switch
                                            {
                                                CollisionOptions.None => CreationCollisionOption.FailIfExists,
                                                CollisionOptions.RenameOnCollision => CreationCollisionOption.GenerateUniqueName,
                                                CollisionOptions.Skip => CreationCollisionOption.OpenIfExists,
                                                CollisionOptions.OverrideOnCollision => CreationCollisionOption.ReplaceExisting,
                                                _ => throw new NotSupportedException()
                                            });

                                            return new FileSystemStorageFile(await NewFile.GetNativeFileDataAsync());
                                        }

                                        throw;
                                    }
                                }
                                catch (Exception)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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

                                        StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), Option switch
                                        {
                                            CollisionOptions.None => CreationCollisionOption.FailIfExists,
                                            CollisionOptions.RenameOnCollision => CreationCollisionOption.GenerateUniqueName,
                                            CollisionOptions.Skip => CreationCollisionOption.OpenIfExists,
                                            CollisionOptions.OverrideOnCollision => CreationCollisionOption.ReplaceExisting,
                                            _ => throw new NotSupportedException()
                                        });

                                        return new FileSystemStorageFolder(await NewFolder.GetNativeFileDataAsync());
                                    }
                                }
                                catch (Exception)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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

        public static async Task CopyAsync(IReadOnlyDictionary<string, string> CopyFrom, string CopyTo, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Regex.IsMatch(CopyTo, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase)
                || CopyFrom.Keys.All((Item) => Regex.IsMatch(Item, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = CopyFrom.Count();

                await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(CopyFrom.Keys, CancelToken))
                {
                    if (Item == null)
                    {
                        throw new FileNotFoundException();
                    }

                    await Item.CopyAsync(CopyTo, CopyFrom[Item.Path], Option, SkipOperationRecord, CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                    });

                    Progress += 100;
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.CopyAsync(CopyFrom, CopyTo, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task MoveAsync(IReadOnlyDictionary<string, string> MoveFrom, string MoveTo, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Regex.IsMatch(MoveTo, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase)
                || MoveFrom.Keys.All((Item) => Regex.IsMatch(Item, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = MoveFrom.Count();

                await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(MoveFrom.Keys, CancelToken))
                {
                    if (Item == null)
                    {
                        throw new FileNotFoundException();
                    }

                    await Item.MoveAsync(MoveTo, MoveFrom[Item.Path], Option, SkipOperationRecord, CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                    });

                    Progress += 100;
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.MoveAsync(MoveFrom, MoveTo, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task DeleteAsync(IEnumerable<string> DeleteFrom, bool PermanentDelete, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (DeleteFrom.All((Item) => Regex.IsMatch(Item, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = DeleteFrom.Count();

                await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(DeleteFrom, CancelToken))
                {
                    if (Item == null)
                    {
                        throw new FileNotFoundException();
                    }

                    await Item.DeleteAsync(PermanentDelete, SkipOperationRecord, CancelToken, (s, e) =>
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Math.Ceiling((Progress + e.ProgressPercentage) / Convert.ToDouble(ItemCount))), null));
                    });

                    Progress += 100;
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.DeleteAsync(DeleteFrom, PermanentDelete, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task<string> RenameAsync(string Path, string DesireName, bool SkipOperationRecord = false, CancellationToken CancelToken = default)
        {
            if (Regex.IsMatch(Path, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
            {
                if (await OpenAsync(Path) is FileSystemStorageItemBase Item)
                {
                    return await Item.RenameAsync(DesireName, SkipOperationRecord, CancelToken);
                }
                else
                {
                    throw new FileNotFoundException(Path);
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await Exclusive.Controller.RenameAsync(Path, DesireName, SkipOperationRecord, CancelToken);
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
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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

        public virtual async Task MoveAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.MoveAsync(Path, DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler); ;
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.MoveAsync(Path, DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        public virtual async Task CopyAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.CopyAsync(Path, DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.CopyAsync(Path, DirectoryPath, NewName, Option, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        public async virtual Task<string> RenameAsync(string DesireName, bool SkipOperationRecord = false, CancellationToken CancelToken = default)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    string NewName = await ControllerRef.Value.Controller.RenameAsync(Path, DesireName, SkipOperationRecord, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    string NewName = await Exclusive.Controller.RenameAsync(Path, DesireName, SkipOperationRecord, CancelToken);
                    Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                    return NewName;
                }
            }
        }

        public virtual async Task DeleteAsync(bool PermanentDelete, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    await ControllerRef.Value.Controller.DeleteAsync(Path, PermanentDelete, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, SkipOperationRecord, CancelToken, ProgressHandler);
                }
            }
        }

        protected FileSystemStorageItemBase(NativeFileData Data) : this(Data?.Path)
        {
            if (!(Data?.IsInvalid).GetValueOrDefault(true))
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

        protected FileSystemStorageItemBase(FtpFileData Data) : this(Data?.Path)
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

        protected void SetBulkAccessSharedController(RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> SharedRef)
        {
            if (Interlocked.Exchange(ref ControllerSharedRef, SharedRef) is RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> PreviousRef)
            {
                PreviousRef.Dispose();
            }
        }

        protected bool GetBulkAccessSharedController(out RefSharedRegion<AuxiliaryTrustProcessController.Exclusive> SharedController)
        {
            return (SharedController = ControllerSharedRef?.CreateNew()) != null;
        }

        protected virtual async Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            async Task<BitmapImage> GetThumbnailOverlayCoreAsync(AuxiliaryTrustProcessController.Exclusive Exclusive)
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
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                        using (IRandomAccessStream ThumbnailStream = await ControllerRef.Value.Controller.GetThumbnailAsync(Type))
                        {
                            return await Helper.CreateBitmapImageAsync(ThumbnailStream);
                        }
                    }
                    else
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        using (IRandomAccessStream ThumbnailStream = await Exclusive.Controller.GetThumbnailAsync(Type))
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
                        return await ControllerRef.Value.Controller.GetThumbnailAsync(Type);
                    }
                }
                else
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        return await Exclusive.Controller.GetThumbnailAsync(Type);
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
