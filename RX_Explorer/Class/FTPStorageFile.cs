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

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<IRandomAccessStream>(null);
        }

        public override Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            return Task.FromResult(new SafeFileHandle(IntPtr.Zero, true));
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                }
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
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath)))
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
                            using (Stream TempFileStream = await CreateLocalOneTimeFileStreamAsync())
                            {
                                switch (Option)
                                {
                                    case CollisionOptions.OverrideOnCollision:
                                        {
                                            if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))))))
                                            {
                                                TempFileStream.Seek(0, SeekOrigin.Begin);

                                                if (await ClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))))) == FtpStatus.Failed)
                                                {
                                                    throw new Exception($"Could not upload the file to the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
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
                                            string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                            if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))))))
                                            {
                                                TempFileStream.Seek(0, SeekOrigin.Begin);

                                                if (await ClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, UniquePath, FtpRemoteExists.NoCheck, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))))) == FtpStatus.Failed)
                                                {
                                                    throw new Exception($"Could not upload the file to the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
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
                                            if (await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(TempFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress / 2)))), null))))))
                                            {
                                                TempFileStream.Seek(0, SeekOrigin.Begin);

                                                if (await ClientController.RunCommandAsync((Client) => Client.UploadAsync(TempFileStream, TargetAnalysis.RelatedPath, FtpRemoteExists.Skip, true, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(50 + Progress.Progress / 2)))), null))))) == FtpStatus.Failed)
                                                {
                                                    throw new Exception($"Could not upload the file to the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
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
                            switch (Option)
                            {
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), TargetAnalysis.RelatedPath, true, FtpRemoteExists.Overwrite, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                        }

                                        break;
                                    }

                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await TargetClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), UniquePath, true, FtpRemoteExists.NoCheck, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), TargetAnalysis.RelatedPath, true, FtpRemoteExists.Skip, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
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
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
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
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
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
                                            if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
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
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath)))
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
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Overwrite)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, UniquePath, FtpRemoteExists.NoCheck)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetAnalysis.Host + UniquePath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (!await ClientController.RunCommandAsync((Client) => Client.MoveFileAsync(RelatedPath, TargetAnalysis.RelatedPath, FtpRemoteExists.Skip)))
                                        {
                                            throw new Exception($"Could not move the file from: {Path} to: {TargetPath} on the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                        }

                                        break;
                                    }
                            }
                        }
                        else
                        {
                            switch (Option)
                            {
                                case CollisionOptions.OverrideOnCollision:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), TargetAnalysis.RelatedPath, true, FtpRemoteExists.Overwrite, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                        }

                                        break;
                                    }

                                case CollisionOptions.RenameOnCollision:
                                    {
                                        string UniquePath = await TargetClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetAnalysis.RelatedPath, CreateType.File));

                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), UniquePath, true, FtpRemoteExists.NoCheck, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                        }

                                        break;
                                    }
                                case CollisionOptions.Skip:
                                    {
                                        if (await ClientController.RunCommandAsync((Client) => Client.TransferFileAsync(RelatedPath, TargetClientController.DangerousGetFtpClient(), TargetAnalysis.RelatedPath, true, FtpRemoteExists.Skip, FtpVerify.Delete, new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))) == FtpStatus.Failed)
                                        {
                                            throw new Exception($"Could not upload the file to the ftp server: {TargetClientController.ServerHost}:{TargetClientController.ServerPort}");
                                        }

                                        break;
                                    }
                            }

                            await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath));
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
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
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
                                        if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
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
                                            if (!await ClientController.RunCommandAsync((Client) => Client.DownloadAsync(NewFileStream, RelatedPath, progress: new Progress<FtpProgress>((Progress) => ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(Progress.Progress)))), null))))))
                                            {
                                                throw new Exception($"Could not download the file from the ftp server: {ClientController.ServerHost}:{ClientController.ServerPort}");
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                    }

                    await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath));
                }
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task<string> RenameAsync(string DesireName, CancellationToken CancelToken = default)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath)))
            {
                string TargetPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RelatedPath), DesireName);

                if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(TargetPath)))
                {
                    TargetPath = await ClientController.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.File));
                }

                await ClientController.RunCommandAsync((Client) => Client.RenameAsync(RelatedPath, TargetPath));

                return TargetPath;
            }
            else
            {
                throw new FileNotFoundException(Path);
            }
        }

        public override async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await ClientController.RunCommandAsync((Client) => Client.FileExistsAsync(RelatedPath)))
            {
                await ClientController.RunCommandAsync((Client) => Client.DeleteFileAsync(RelatedPath));
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
