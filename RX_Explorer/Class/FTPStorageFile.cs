using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    internal class FTPStorageFile : FileSystemStorageFile, IFTPStorageItem
    {
        private string InnerDisplayType;
        private readonly FTPFileData Data;
        private readonly FTPClientController ClientController;

        public string RelatedPath { get => Data.RelatedPath; }

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            async Task<BitmapImage> InternalGetThumbnailAsync(FullTrustProcessController.Exclusive Exclusive)
            {
                if (await Exclusive.Controller.GetThumbnailAsync(Type) is Stream ThumbnailStream)
                {
                    BitmapImage Thumbnail = new BitmapImage();
                    await Thumbnail.SetSourceAsync(ThumbnailStream.AsRandomAccessStream());
                    return Thumbnail;
                }

                return new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                        ? new Uri("ms-appx:///Assets/Page_Solid_White.png")
                                                        : new Uri("ms-appx:///Assets/Page_Solid_Black.png"));
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
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await InternalGetThumbnailAsync(Exclusive);
                }
            }
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            try
            {
                async Task<IRandomAccessStream> GetRawStreamCoreAsync(FullTrustProcessController.Exclusive Exclusive)
                {
                    if (await Exclusive.Controller.GetThumbnailAsync(Type) is Stream ThumbnailStream)
                    {
                        return ThumbnailStream.AsRandomAccessStream();
                    }

                    return null;
                }

                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await GetRawStreamCoreAsync(ControllerRef.Value);
                    }
                }
                else
                {
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await GetRawStreamCoreAsync(Exclusive);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the raw stream of thumbnail");
            }

            return null;
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
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not create streamed file for ftp file: {Path}");
                        Request.FailAndClose(StreamedFileFailureMode.Incomplete);
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
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync(PriorityLevel.Low))
                {
                    InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                }
            }

            if (Size == 0)
            {
                Size = Convert.ToUInt64(await ClientController.RunCommandAsync((Client) => Client.GetFileSizeAsync(RelatedPath, 0)));
            }
        }

        public override async Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            Stream Stream = await CreateLocalOneTimeFileStreamAsync();

            if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(Stream, RelatedPath)))
            {
                throw new InvalidDataException();
            }

            Stream.Seek(0, SeekOrigin.Begin);

            return new FTPFileSaveOnFlushStream(Path, ClientController, Stream);
        }

        public override Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Properties.Select((Prop) => new KeyValuePair<string, string>(Prop, string.Empty))));
        }

        public override Task<ulong> GetSizeOnDiskAsync()
        {
            return Task.FromResult(Size);
        }

        public Task<FTPFileData> GetRawDataAsync()
        {
            return Task.FromResult(Data);
        }

        public override async Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath, CancelToken)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (DirectoryPath.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                    || DirectoryPath.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
                {
                    FTPPathAnalysis TargetAnalysis = new FTPPathAnalysis(TargetPath);

                    if (await FTPClientManager.GetClientControllerAsync(TargetAnalysis) is FTPClientController TargetClientController)
                    {
                        using (Stream TempFileStream = await CreateLocalOneTimeFileStreamAsync())
                        {
                            switch (Option)
                            {
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))), CancelToken)))
                                        {
                                            TempFileStream.Seek(0, SeekOrigin.Begin);

                                            if (await TargetClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))), CancelToken)) == FtpStatus.Failed)
                                            {
                                                throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                            }
                                        }
                                        else
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }

                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await TargetClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))), CancelToken)))
                                        {
                                            TempFileStream.Seek(0, SeekOrigin.Begin);

                                            if (await TargetClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, UniquePath, FtpRemoteExists.NoCheck, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))), CancelToken)) == FtpStatus.Failed)
                                            {
                                                throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                            }
                                        }
                                        else
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))), CancelToken)))
                                        {
                                            TempFileStream.Seek(0, SeekOrigin.Begin);

                                            if (await TargetClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, TargetAnalysis.RelatedPath, FtpRemoteExists.Skip, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))), CancelToken)) == FtpStatus.Failed)
                                            {
                                                throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                            }
                                        }
                                        else
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
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
                    string TargetFilePath = System.IO.Path.Combine(DirectoryPath, Name);

                    switch (Option)
                    {
                        case CollisionOptions.OverrideOnCollision:
                            {
                                if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))), CancelToken)))
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.RenameOnCollision:
                            {
                                if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))), CancelToken)))
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.Skip:
                            {
                                if (!await CheckExistsAsync(TargetFilePath))
                                {
                                    if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile NewFile)
                                    {
                                        using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                        {
                                            if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))), CancelToken)))
                                            {
                                                throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
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
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath, CancelToken)))
            {
                string TargetPath = System.IO.Path.Combine(DirectoryPath, Name);

                if (DirectoryPath.StartsWith(@"ftp:\", StringComparison.OrdinalIgnoreCase)
                    || DirectoryPath.StartsWith(@"ftps:\", StringComparison.OrdinalIgnoreCase))
                {
                    FTPPathAnalysis TargetAnalysis = new FTPPathAnalysis(TargetPath);

                    if (await FTPClientManager.GetClientControllerAsync(TargetAnalysis) is FTPClientController TargetClientController)
                    {
                        if (TargetClientController == ClientController)
                        {
                            switch (Option)
                            {
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, UniquePath, FtpRemoteExists.NoCheck, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetAnalysis.Host + UniquePath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Skip, CancelToken)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                            }
                        }
                        else
                        {
                            await CopyAsync(DirectoryPath, Option, CancelToken, ProgressHandler);
                            await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath, CancelToken));
                        }
                    }
                    else
                    {
                        throw new Exception($"Could not find the ftp server: {TargetAnalysis.Host}");
                    }
                }
                else
                {
                    string TargetFilePath = System.IO.Path.Combine(DirectoryPath, Name);

                    switch (Option)
                    {
                        case CollisionOptions.OverrideOnCollision:
                            {
                                if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))), CancelToken)))
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.RenameOnCollision:
                            {
                                if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile NewFile)
                                {
                                    using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))), CancelToken)))
                                        {
                                            throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }
                                    }
                                }

                                break;
                            }
                        case CollisionOptions.Skip:
                            {
                                if (!await CheckExistsAsync(TargetFilePath))
                                {
                                    if (await CreateNewAsync(TargetFilePath, CreateType.File, CreateOption.ReplaceExisting) is FileSystemStorageFile NewFile)
                                    {
                                        using (Stream NewFileStream = await NewFile.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                                        {
                                            if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, 0, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
                                            {
                                                throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                    }

                    await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath, CancelToken));
                }
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task<string> RenameAsync(string DesireName, CancellationToken CancelToken = default)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath, CancelToken)))
            {
                string TargetPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RelatedPath), DesireName);

                if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(TargetPath, CancelToken)))
                {
                    TargetPath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.File));
                }

                await ClientController.RunCommandAsync((Client) => Client.RenameAsync(RelatedPath, TargetPath, CancelToken));

                return TargetPath;
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath, CancelToken)))
            {
                await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath, CancelToken));
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public FTPStorageFile(FTPClientController ClientController, FTPFileData Data) : base(Data)
        {
            this.Data = Data;
            this.ClientController = ClientController;
        }
    }
}
