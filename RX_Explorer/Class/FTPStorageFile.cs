using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    internal class FtpStorageFile : FileSystemStorageFile, IFtpStorageItem, INotWin32StorageFile
    {
        private string InnerDisplayType;
        private readonly FtpFileData Data;
        private readonly FtpClientController ClientController;

        public string RelatedPath { get => Data.RelatedPath; }

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            async Task<BitmapImage> InternalGetThumbnailAsync(AuxiliaryTrustProcessController.Exclusive Exclusive)
            {
                try
                {
                    using (IRandomAccessStream ThumbnailStream = await Exclusive.Controller.GetThumbnailAsync(Type))
                    {
                        return await Helper.CreateBitmapImageAsync(ThumbnailStream);
                    }
                }
                catch (Exception)
                {
                    return new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                        ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                        : new Uri("ms-appx:///Assets/SingleItem_Black.png"));
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
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await InternalGetThumbnailAsync(Exclusive);
                }
            }
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
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

        public override Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            return Task.FromResult(new SafeFileHandle(IntPtr.Zero, true));
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override async Task<IStorageItem> GetStorageItemCoreAsync()
        {
            try
            {
                RandomAccessStreamReference Reference = null;

                try
                {
                    Reference = RandomAccessStreamReference.CreateFromStream(await GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }

                return await StorageFile.CreateStreamedFileAsync(Name, async (Request) =>
                {
                    try
                    {
                        using (Stream TargetFileStream = Request.AsStreamForWrite())
                        using (Stream CurrentFileStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                        {
                            if (CurrentFileStream == null)
                            {
                                throw new Exception($"Could not get the file stream from ftp file: {Path}");
                            }

                            await CurrentFileStream.CopyToAsync(TargetFileStream);
                        }

                        Request.Dispose();
                    }
                    catch (Exception)
                    {
                        Request.FailAndClose(StreamedFileFailureMode.CurrentlyUnavailable);
                    }
                }, Reference);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get the storage item for ftp file: {Path}");
            }

            return null;
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
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
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.Low))
                {
                    InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                }
            }

            if (Size == 0)
            {
                Size = Convert.ToUInt64(await ClientController.RunCommandAsync((Client) => Client.GetFileSize(RelatedPath, 0)));
            }
        }

        public override async Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option = OptimizeOption.None)
        {
            FtpClientController AuxiliaryController = await FtpClientController.DuplicateClientControllerAsync(ClientController);

            Stream OriginStream = await AuxiliaryController.RunCommandAsync((Client) => Client.GetFtpFileStreamForReadAsync(RelatedPath, FtpDataType.Binary, 0, (long)Size));
            SequentialVirtualRandomAccessStream RandomAccessStream = await SequentialVirtualRandomAccessStream.CreateAsync(OriginStream);

            if (Mode == AccessMode.Read)
            {
                return RandomAccessStream;
            }
            else
            {
                return new FtpFileSaveOnFlushStream(Path, AuxiliaryController, RandomAccessStream);
            }
        }

        public override Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Properties.Select((Prop) => new KeyValuePair<string, string>(Prop, string.Empty))));
        }

        public override Task<ulong> GetSizeOnDiskAsync()
        {
            return Task.FromResult(Size);
        }

        public Task<FtpFileData> GetRawDataAsync()
        {
            return Task.FromResult(Data);
        }

        public override async Task CopyAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExists(RelatedPath, CancelToken)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (Regex.IsMatch(DirectoryPath, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
                {
                    FtpPathAnalysis TargetAnalysis = new FtpPathAnalysis(TargetPath);

                    if (await FtpClientManager.GetClientControllerAsync(TargetAnalysis) is FtpClientController TargetClientController)
                    {
                        using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(TargetClientController))
                        {
                            string TargetRelatedPath = TargetAnalysis.RelatedPath;

                            switch (Option)
                            {
                                case CollisionOptions.None:
                                    {
                                        if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetRelatedPath, CancelToken)))
                                        {
                                            throw new Exception($"{TargetAnalysis.Path} is already exists");
                                        }

                                        break;
                                    }
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetRelatedPath, CancelToken)))
                                        {
                                            await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteFile(TargetRelatedPath, CancelToken));
                                        }

                                        break;
                                    }

                                case CollisionOptions.RenameOnCollision:
                                    {
                                        TargetRelatedPath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetRelatedPath, CreateType.File));
                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetRelatedPath, CancelToken)))
                                        {
                                            return;
                                        }

                                        break;
                                    }
                            }

                            using (Stream OriginStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            using (Stream TargetStream = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GetFtpFileStreamForWriteAsync(TargetRelatedPath, FtpDataType.Binary, CancelToken)))
                            {
                                await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
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
                    if (await CreateNewAsync(TargetPath, CreateType.File, Option) is FileSystemStorageFile NewFile)
                    {
                        using (Stream TargetStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write))
                        using (Stream OriginStream = await GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                        {
                            await OriginStream.CopyToAsync(TargetStream, OriginStream.Length, CancelToken, ProgressHandler);
                        }
                    }
                }
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task MoveAsync(string DirectoryPath, string NewName = null, CollisionOptions Option = CollisionOptions.Skip, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExists(RelatedPath, CancelToken)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (Regex.IsMatch(DirectoryPath, @"^(ftps?:\\{1,2}$)|(ftps?:\\{1,2}[^\\]+.*)", RegexOptions.IgnoreCase))
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
                                        if (await ClientController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                        {
                                            throw new Exception($"{TargetAnalysis.Path} is already exists");
                                        }

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFile(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.NoCheck, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFile(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFile(RelatedPath, UniquePath, FtpRemoteExists.NoCheck, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetAnalysis.Host + UniquePath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.FileExists(TargetAnalysis.RelatedPath, CancelToken)))
                                        {
                                            if (!await ClientController.RunCommandAsync((Client) => Client.MoveFile(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.NoCheck, CancelToken)))
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
                if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(RelatedPath, CancelToken)))
                {
                    string TargetPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RelatedPath), DesireName);

                    if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(TargetPath, CancelToken)))
                    {
                        TargetPath = await AuxiliaryWriteController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.File));
                    }

                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.Rename(RelatedPath, TargetPath, CancelToken));

                    return TargetPath;
                }
                else
                {
                    throw new FileNotFoundException(Path);
                }
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FtpClientController AuxiliaryWriteController = await FtpClientController.DuplicateClientControllerAsync(ClientController))
            {
                if (await AuxiliaryWriteController.RunCommandAsync((Client) => Client.FileExists(RelatedPath, CancelToken)))
                {
                    await AuxiliaryWriteController.RunCommandAsync((Client) => Client.DeleteFile(RelatedPath, CancelToken));
                }
                else
                {
                    throw new FileNotFoundException(Path);
                }
            }
        }

        public FtpStorageFile(FtpClientController ClientController, FtpFileData Data) : base(Data)
        {
            this.Data = Data;
            this.ClientController = ClientController;
        }
    }
}
