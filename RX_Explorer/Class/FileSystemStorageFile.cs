using FluentFTP;
using Microsoft.Win32.SafeHandles;
using SharedLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFile : FileSystemStorageItemBase
    {
        private string InnerDisplayType;

        public override string Type => string.IsNullOrEmpty(base.Type) ? Globalization.GetString("File_Admin_DisplayType") : base.Type;

        public override string DisplayType => ((StorageItem as StorageFile)?.DisplayType) ?? (string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType);

        public override string DisplayName => Name;

        public override BitmapImage Thumbnail => base.Thumbnail ??= new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                        ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                                                        : new Uri("ms-appx:///Assets/SingleItem_Black.png"));

        public FileSystemStorageFile(NativeFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFile(MTPFileData Data) : base(Data)
        {

        }

        public FileSystemStorageFile(FtpFileData Data) : base(Data)
        {

        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            return await base.GetThumbnailCoreAsync(Mode, ForceUpdate)
                                ?? new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                        ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                        : new Uri("ms-appx:///Assets/SingleItem_Black.png"));
        }

        public async virtual Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option = OptimizeOption.None)
        {
            try
            {
                return NativeWin32API.CreateStreamFromFile(Path, Mode, Option);
            }
            catch (Exception)
            {
                SafeFileHandle Handle = await GetNativeHandleAsync(Mode, Option);

                if ((Handle?.IsInvalid).GetValueOrDefault(true))
                {
                    throw new UnauthorizedAccessException($"Could not create a new file stream, Path: \"{Path}\"");
                }
                else
                {
                    FileAccess Access = Mode switch
                    {
                        AccessMode.Read => FileAccess.Read,
                        AccessMode.ReadWrite or AccessMode.Exclusive => FileAccess.ReadWrite,
                        AccessMode.Write => FileAccess.Write,
                        _ => throw new NotSupportedException()
                    };

                    return new FileStream(Handle, Access, 4096, true);
                }
            }
        }

        public virtual async Task<ulong> GetSizeOnDiskAsync()
        {
            async Task<ulong> GetSizeOnDiskCoreAsync()
            {
                using (SafeFileHandle Handle = await GetNativeHandleAsync(AccessMode.Read, OptimizeOption.None))
                {
                    if (!Handle.IsInvalid)
                    {
                        string PathRoot = System.IO.Path.GetPathRoot(Path);

                        if (!string.IsNullOrEmpty(PathRoot))
                        {
                            if (NativeWin32API.GetDiskFreeSpace(PathRoot.TrimEnd('\\'), out uint SectorsPerCluster, out uint BytesPerSector, out _, out _))
                            {
                                ulong ClusterSize = Convert.ToUInt64(SectorsPerCluster) * Convert.ToUInt64(BytesPerSector);
                                ulong CompressedSize = NativeWin32API.GetAllocationSizeFromHandle(Handle.DangerousGetHandle());

                                if (ClusterSize > 0)
                                {
                                    if (CompressedSize % ClusterSize > 0)
                                    {
                                        return CompressedSize + ClusterSize - CompressedSize % ClusterSize;
                                    }
                                    else
                                    {
                                        return CompressedSize;
                                    }
                                }
                            }
                        }
                    }
                }

                return 0;
            }

            try
            {
                ulong SizeOnDisk = await GetSizeOnDiskCoreAsync();

                if (SizeOnDisk > 0)
                {
                    return SizeOnDisk;
                }
                else
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exlusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        return await Exlusive.Controller.GetSizeOnDiskAsync(Path);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the size on disk");
            }

            return 0;
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            try
            {
                return await base.GetThumbnailRawStreamCoreAsync(Mode, ForceUpdate);
            }
            catch (Exception)
            {
                StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                                                                : new Uri("ms-appx:///Assets/SingleItem_Black.png"));
                return await ThumbnailFile.OpenReadAsync();
            }
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            try
            {
                if (StorageItem == null)
                {
                    if (GetBulkAccessSharedController(out var ControllerRef))
                    {
                        using (ControllerRef)
                        {
                            InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                        }
                    }
                    else
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                        {
                            InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                        }
                    }
                }

                if (ForceUpdate)
                {
                    NativeFileData Data = NativeWin32API.GetStorageItemRawData(Path);

                    if (Data.IsInvalid)
                    {
                        if (await GetStorageItemCoreAsync() is StorageFile File)
                        {
                            Size = await File.GetSizeRawDataAsync();
                            ModifiedTime = await File.GetModifiedTimeAsync();
                            LastAccessTime = await File.GetLastAccessTimeAsync();
                        }

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
                    else
                    {
                        Size = Data.Size;
                        IsReadOnly = Data.IsReadOnly;
                        IsHiddenItem = Data.IsHiddenItem;
                        IsSystemItem = Data.IsSystemItem;
                        ModifiedTime = Data.ModifiedTime;
                        LastAccessTime = Data.LastAccessTime;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(LoadCoreAsync)}");
            }
        }

        protected override async Task<IStorageItem> GetStorageItemCoreAsync()
        {
            try
            {
                return await StorageFile.GetFileFromPathAsync(Path);
            }
            catch (FileNotFoundException)
            {
                LogTracer.Log($"Could not get StorageFile because file is not found, path: {Path}");
            }
            catch (UnauthorizedAccessException)
            {
                LogTracer.Log($"Could not get StorageFile because do not have enough permission to access this file, path: {Path}");
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFile, path: {Path}");
            }

            return null;
        }

        public override async Task CopyAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Regex.IsMatch(DirectoryPath, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
            {
                FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(DirectoryPath, Name));

                if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                {
                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                    using (Stream OriginStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                    {
                        switch (Option)
                        {
                            case CollisionOptions.OverrideOnCollision:
                                {
                                    if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteFile(TargetAnalysis.RelatedPath, CancelToken));
                                    }

                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(TargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                    {
                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                                    }

                                    break;
                                }

                            case CollisionOptions.RenameOnCollision:
                                {
                                    string UniquePath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(UniquePath, FtpDataType.Binary, CancelToken)))
                                    {
                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                                    }

                                    break;
                                }
                            case CollisionOptions.Skip:
                                {
                                    if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(TargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                        {
                                            await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
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
            if (Regex.IsMatch(DirectoryPath, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
            {
                FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(System.IO.Path.Combine(DirectoryPath, Name));

                if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                {
                    using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                    using (Stream OriginStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                    {
                        switch (Option)
                        {
                            case CollisionOptions.OverrideOnCollision:
                                {
                                    if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteFile(TargetAnalysis.RelatedPath, CancelToken));
                                    }

                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(TargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                    {
                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                                    }

                                    break;
                                }

                            case CollisionOptions.RenameOnCollision:
                                {
                                    string UniquePath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                    using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(UniquePath, FtpDataType.Binary, CancelToken)))
                                    {
                                        await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                                    }

                                    break;
                                }
                            case CollisionOptions.Skip:
                                {
                                    if (!await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                    {
                                        using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(TargetAnalysis.RelatedPath, FtpDataType.Binary, CancelToken)))
                                        {
                                            await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                                        }
                                    }

                                    break;
                                }
                        }
                    }

                    await DeleteAsync(true, true);
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

        public static explicit operator StorageFile(FileSystemStorageFile File)
        {
            return File.StorageItem as StorageFile;
        }
    }
}
