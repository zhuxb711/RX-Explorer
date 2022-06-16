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
using System.Text.Json;
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
        private int IsContentLoaded;
        private double InnerThumbnailOpacity = 1;
        private RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerSharedRef;

        public string Path { get; protected set; }

        public virtual string SizeDescription { get; }

        public virtual string Name => System.IO.Path.GetFileName(Path) ?? string.Empty;

        public virtual string Type => System.IO.Path.GetExtension(Path)?.ToUpper() ?? string.Empty;

        public abstract string DisplayName { get; }

        public abstract string DisplayType { get; }

        public LabelKind Label
        {
            get
            {
                return SQLite.Current.GetLabelKindFromPath(Path);
            }
            set
            {
                SQLite.Current.SetLabelKindByPath(Path, value);
                OnPropertyChanged();
            }
        }

        public double ThumbnailOpacity
        {
            get
            {
                if (IsHiddenItem)
                {
                    return 0.5;
                }
                else
                {
                    return InnerThumbnailOpacity;
                }
            }
            private set
            {
                InnerThumbnailOpacity = value;
            }
        }

        public virtual ulong Size { get; protected set; }

        public virtual DateTimeOffset CreationTime { get; protected set; }

        public virtual DateTimeOffset ModifiedTime { get; protected set; }

        public virtual DateTimeOffset LastAccessTime { get; protected set; }

        public virtual string LastAccessTimeDescription
        {
            get
            {
                if (LastAccessTime == DateTimeOffset.MaxValue.ToLocalTime() || LastAccessTime == DateTimeOffset.MinValue.ToLocalTime())
                {
                    return string.Empty;
                }
                else
                {
                    return LastAccessTime.ToString("G");
                }
            }
        }

        public virtual string ModifiedTimeDescription
        {
            get
            {
                if (ModifiedTime == DateTimeOffset.MaxValue.ToLocalTime() || ModifiedTime == DateTimeOffset.MinValue.ToLocalTime())
                {
                    return string.Empty;
                }
                else
                {
                    return ModifiedTime.ToString("G");
                }
            }
        }

        public virtual string CreationTimeDescription
        {
            get
            {
                if (CreationTime == DateTimeOffset.MaxValue.ToLocalTime() || CreationTime == DateTimeOffset.MinValue.ToLocalTime())
                {
                    return string.Empty;
                }
                else
                {
                    return CreationTime.ToString("G");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual BitmapImage Thumbnail { get; protected set; }

        public virtual BitmapImage ThumbnailOverlay { get; protected set; }

        public virtual bool IsReadOnly { get; protected set; }

        public virtual bool IsSystemItem { get; protected set; }

        public virtual bool IsHiddenItem { get; protected set; }

        protected virtual bool ShouldGenerateThumbnail => (this is FileSystemStorageFile && SettingPage.ContentLoadMode == LoadMode.OnlyFile) || SettingPage.ContentLoadMode == LoadMode.All;

        protected ThumbnailMode ThumbnailMode { get; set; } = ThumbnailMode.ListView;

        public SyncStatus SyncStatus { get; protected set; } = SyncStatus.Unknown;

        public IStorageItem StorageItem { get; protected set; }

        public static Task<EndUsageNotification> SetBulkAccessSharedControllerAsync<T>(T Item, FullTrustProcessController.ExclusiveUsage ExistingExclusiveUsage = null) where T : FileSystemStorageItemBase
        {
            return SetBulkAccessSharedControllerAsync(new T[] { Item }, ExistingExclusiveUsage);
        }

        public static async Task<EndUsageNotification> SetBulkAccessSharedControllerAsync<T>(IEnumerable<T> Items, FullTrustProcessController.ExclusiveUsage ExistingExclusiveUsage = null) where T : FileSystemStorageItemBase
        {
            if (Items.Any())
            {
                FullTrustProcessController.ExclusiveUsage Exclusive = ExistingExclusiveUsage ?? await FullTrustProcessController.GetAvailableControllerAsync();
                RefSharedRegion<FullTrustProcessController.ExclusiveUsage> SharedRef = new RefSharedRegion<FullTrustProcessController.ExclusiveUsage>(Exclusive, ExistingExclusiveUsage == null);

                foreach (T Item in Items)
                {
                    Item.SetBulkAccessSharedControllerCore(SharedRef);
                }

                return new EndUsageNotification(() =>
                {
                    SharedRef.Dispose();
                });
            }
            else
            {
                throw new ArgumentException("Input items should not be empty", nameof(Items));
            }
        }

        private void SetBulkAccessSharedControllerCore(RefSharedRegion<FullTrustProcessController.ExclusiveUsage> SharedRef)
        {
            if (Interlocked.Exchange(ref ControllerSharedRef, SharedRef) is RefSharedRegion<FullTrustProcessController.ExclusiveUsage> PreviousRef)
            {
                PreviousRef.Dispose();
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
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                foreach (string Path in PathArray)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        yield break;
                    }

                    if (await OpenCoreAsync(Path, Exclusive.Controller) is FileSystemStorageItemBase Item)
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
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                return await OpenCoreAsync(Path, Exclusive.Controller);
            }
        }

        private static async Task<FileSystemStorageItemBase> OpenCoreAsync(string Path, FullTrustProcessController Controller)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                                        string NewPath = NativeWin32API.CreateFileFromPath(Path, Option);

                                        if (await OpenAsync(NewPath) is FileSystemStorageFile NewFile)
                                        {
                                            return NewFile;
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
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                                    if (NativeWin32API.CreateDirectoryFromPath(Path, Option, out string NewPath))
                                    {
                                        return await OpenAsync(NewPath);
                                    }
                                    else
                                    {
                                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

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
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(CopyFrom, CancelToken))
                    {
                        using (EndUsageNotification Disposable = await SetBulkAccessSharedControllerAsync(Item, Exclusive))
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.CopyAsync(CopyFrom, CopyTo, Option, false, CancelToken, ProgressHandler);
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

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(MoveFrom, CancelToken))
                    {
                        using (EndUsageNotification Disposable = await SetBulkAccessSharedControllerAsync(Item, Exclusive))
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.MoveAsync(MoveFrom, MoveTo, Option, false, CancelToken, ProgressHandler);
                }
            }
        }

        public static async Task DeleteAsync(IEnumerable<string> DeleteFrom, bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (DeleteFrom.All((Item) => Item.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase) || Item.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase)))
            {
                int Progress = 0;
                int ItemCount = DeleteFrom.Count();

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await foreach (FileSystemStorageItemBase Item in OpenInBatchAsync(DeleteFrom, CancelToken))
                    {
                        using (EndUsageNotification Disposable = await SetBulkAccessSharedControllerAsync(Item, Exclusive))
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.DeleteAsync(DeleteFrom, PermanentDelete, false, CancelToken, ProgressHandler);
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await Exclusive.Controller.RenameAsync(Path, DesireName, true, CancelToken);
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

        public void SetThumbnailStatus(ThumbnailStatus Status)
        {
            if (!IsHiddenItem)
            {
                switch (Status)
                {
                    case ThumbnailStatus.Normal:
                        {
                            ThumbnailOpacity = 1;
                            break;
                        }
                    case ThumbnailStatus.HalfOpacity:
                        {
                            ThumbnailOpacity = 0.5;
                            break;
                        }
                }

                OnPropertyChanged(nameof(ThumbnailOpacity));
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
                        LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Thumbnail));
                    }
                }
            }
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                async Task LocalLoadAsync()
                {
                    try
                    {
                        using (EndUsageNotification Disposable = await SetBulkAccessSharedControllerAsync(this))
                        {
                            if (StorageItem == null)
                            {
                                await GetStorageItemAsync();
                            }

                            await LoadCoreAsync(false);

                            List<Task> ParallelLoadTasks = new List<Task>()
                            {
                                GetThumbnailOverlayAsync(),
                            };

                            if (ShouldGenerateThumbnail)
                            {
                                ParallelLoadTasks.Add(GetThumbnailAsync(ThumbnailMode));
                            }

                            if (SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive)
                                || SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.Dropbox))
                            {
                                ParallelLoadTasks.Add(GetSyncStatusAsync());
                            }

                            await Task.WhenAll(ParallelLoadTasks);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(DisplayName));
                        OnPropertyChanged(nameof(ModifiedTimeDescription));
                        OnPropertyChanged(nameof(LastAccessTimeDescription));
                        OnPropertyChanged(nameof(ThumbnailOverlay));
                        OnPropertyChanged(nameof(SyncStatus));

                        if (ShouldGenerateThumbnail)
                        {
                            OnPropertyChanged(nameof(Thumbnail));
                        }

                        if (this is FileSystemStorageFile)
                        {
                            OnPropertyChanged(nameof(DisplayType));
                            OnPropertyChanged(nameof(SizeDescription));
                        }
                    }
                };

                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    await LocalLoadAsync();
                }
                else
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () => await LocalLoadAsync());
                }
            }
        }

        public async Task RefreshAsync()
        {
            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not refresh the {GetType().FullName}, path: {Path}");
            }
            finally
            {
                if (this is FileSystemStorageFile)
                {
                    OnPropertyChanged(nameof(SizeDescription));
                }

                if (ShouldGenerateThumbnail)
                {
                    OnPropertyChanged(nameof(Thumbnail));
                }

                OnPropertyChanged(nameof(ModifiedTimeDescription));
                OnPropertyChanged(nameof(LastAccessTimeDescription));
            }
        }

        protected bool GetBulkAccessSharedController(out RefSharedRegion<FullTrustProcessController.ExclusiveUsage> SharedController)
        {
            return (SharedController = ControllerSharedRef?.CreateNew()) != null;
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await Exclusive.Controller.GetNativeHandleAsync(Path, Mode, Option);
                }
            }
        }

        protected virtual async Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            async Task<BitmapImage> GetThumbnailOverlayCoreAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
            {
                byte[] ThumbnailOverlayByteArray = await Exclusive.Controller.GetThumbnailOverlayAsync(Path);

                if (ThumbnailOverlayByteArray.Length > 0)
                {
                    using (MemoryStream Ms = new MemoryStream(ThumbnailOverlayByteArray))
                    {
                        BitmapImage Overlay = new BitmapImage();
                        await Overlay.SetSourceAsync(Ms.AsRandomAccessStream());
                        return Overlay;
                    }
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(Exclusive);
                }
            }
        }

        protected abstract Task LoadCoreAsync(bool ForceUpdate);

        protected abstract Task<IStorageItem> GetStorageItemCoreAsync(bool ForceUpdate);

        public async Task<IStorageItem> GetStorageItemAsync(bool ForceUpdate = false)
        {
            if (!IsHiddenItem && !IsSystemItem)
            {
                return await GetStorageItemCoreAsync(ForceUpdate);
            }

            return null;
        }

        public async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (Thumbnail == null || ForceUpdate || !string.IsNullOrEmpty(Thumbnail.UriSource?.AbsoluteUri))
            {
                Thumbnail = await GetThumbnailCoreAsync(Mode, ForceUpdate);
            }

            return Thumbnail;
        }

        protected virtual async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            async Task<BitmapImage> InternalGetThumbnailAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
            {
                if (await Exclusive.Controller.GetThumbnailAsync(Path) is Stream ThumbnailStream)
                {
                    BitmapImage Thumbnail = new BitmapImage();
                    await Thumbnail.SetSourceAsync(ThumbnailStream.AsRandomAccessStream());
                    return Thumbnail;
                }

                return null;
            }

            try
            {
                if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
                {
                    if (await Item.GetThumbnailBitmapAsync(Mode) is BitmapImage LocalThumbnail)
                    {
                        return LocalThumbnail;
                    }
                }

                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await InternalGetThumbnailAsync(ControllerRef.Value);
                    }
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await InternalGetThumbnailAsync(Exclusive);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get thumbnail of path: \"{Path}\"");
            }

            return null;
        }


        public async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return await GetThumbnailRawStreamCoreAsync(Mode, ForceUpdate) ?? throw new NotSupportedException("Could not get the thumbnail stream");
        }

        protected virtual async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (await GetStorageItemAsync(ForceUpdate) is IStorageItem Item)
            {
                return await Item.GetThumbnailRawStreamAsync(Mode);
            }
            else
            {
                async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
                {
                    if (await Exclusive.Controller.GetThumbnailAsync(Path) is Stream ThumbnailStream)
                    {
                        return ThumbnailStream.AsRandomAccessStream();
                    }

                    return null;
                }

                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await GetThumbnailRawStreamCoreAsync(ControllerRef.Value);
                    }
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await GetThumbnailRawStreamCoreAsync(Exclusive);
                    }
                }
            }
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
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, true, CancelToken, ProgressHandler);
                }
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

        public static class SpecialPath
        {
            private static IReadOnlyList<string> OneDrivePathCollection { get; } = new List<string>(3)
            {
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Environment.GetEnvironmentVariable("OneDrive")
            };

            private static IReadOnlyList<string> DropboxPathCollection { get; set; } = new List<string>(0);

            public enum SpecialPathEnum
            {
                OneDrive,
                Dropbox
            }

            public static async Task InitializeAsync()
            {
                static async Task<IReadOnlyList<string>> LocalLoadJsonAsync(string JsonPath)
                {
                    List<string> DropboxPathResult = new List<string>(2);

                    try
                    {
                        if (await OpenAsync(JsonPath) is FileSystemStorageFile JsonFile)
                        {
                            using (Stream Stream = await JsonFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            using (StreamReader Reader = new StreamReader(Stream, true))
                            {
                                var JsonObject = JsonSerializer.Deserialize<IDictionary<string, IDictionary<string, object>>>(Reader.ReadToEnd());

                                if (JsonObject.TryGetValue("personal", out IDictionary<string, object> PersonalSubDic))
                                {
                                    DropboxPathResult.Add(Convert.ToString(PersonalSubDic["path"]));
                                }

                                if (JsonObject.TryGetValue("business", out IDictionary<string, object> BusinessSubDic))
                                {
                                    DropboxPathResult.Add(Convert.ToString(BusinessSubDic["path"]));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not get the configuration from Dropbox info.json");
                    }

                    return DropboxPathResult;
                }

                string JsonPath1 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%APPDATA%\Dropbox\info.json");
                string JsonPath2 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%LOCALAPPDATA%\Dropbox\info.json");

                if (await CheckExistsAsync(JsonPath1))
                {
                    DropboxPathCollection = await LocalLoadJsonAsync(JsonPath1);
                }
                else if (await CheckExistsAsync(JsonPath2))
                {
                    DropboxPathCollection = await LocalLoadJsonAsync(JsonPath2);
                }

                if (DropboxPathCollection.Count == 0)
                {
                    DropboxPathCollection = new List<string>(1)
                    {
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Dropbox")
                    };
                }
            }

            public static bool IsPathIncluded(string Path, SpecialPathEnum Enum)
            {
                switch (Enum)
                {
                    case SpecialPathEnum.OneDrive:
                        {
                            return OneDrivePathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((OneDrivePath) => Path.StartsWith(OneDrivePath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(OneDrivePath, StringComparison.OrdinalIgnoreCase));
                        }
                    case SpecialPathEnum.Dropbox:
                        {
                            return DropboxPathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((DropboxPath) => Path.StartsWith(DropboxPath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(DropboxPath, StringComparison.OrdinalIgnoreCase));
                        }
                    default:
                        {
                            return false;
                        }
                }
            }
        }
    }
}
