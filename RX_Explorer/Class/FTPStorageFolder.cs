using FluentFTP;
using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
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
    public class FTPStorageFolder : FileSystemStorageFolder, IFTPStorageItem
    {
        private readonly FTPClientController Controller;
        private readonly FTPFileData Data;

        public override string DisplayName => Data.RelatedPath == "\\" ? Path : base.DisplayName;

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult<IRandomAccessStream>(null);
        }

        public override Task<IStorageItem> GetStorageItemAsync()
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
            foreach (FtpListItem Item in await Controller.RunCommandAsync((Client) => Client.GetListingAsync(Data.RelatedPath, IncludeAllSubItems ? FtpListOption.Recursive : FtpListOption.Auto, CancelToken)))
            {
                if ((AdvanceFilter?.Invoke(Item.Name)).GetValueOrDefault(true))
                {
                    if (Item.Type.HasFlag(FtpFileSystemObjectType.Directory))
                    {
                        if (Filter.HasFlag(BasicFilters.Folder))
                        {
                            yield return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, 0, Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                        }
                    }
                    else
                    {
                        if (Filter.HasFlag(BasicFilters.File))
                        {
                            yield return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, Convert.ToUInt64(Item.Size), Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                        }
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

        public override async Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, CreateType ItemType, CreateOption Option)
        {
            string TargetPath = System.IO.Path.Combine(Data.RelatedPath, Name);

            try
            {
                if (ItemType == CreateType.Folder)
                {
                    switch (Option)
                    {
                        case CreateOption.OpenIfExist:
                            {
                                if (!await Controller.RunCommandAsync((Client) => Client.DirectoryExistsAsync(TargetPath)))
                                {
                                    if (!await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(TargetPath)))
                                    {
                                        throw new Exception("Could not create the directory on ftp server");
                                    }
                                }

                                if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(TargetPath, true)) is FtpListItem Item)
                                {
                                    return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, 0, Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                                }

                                break;
                            }
                        case CreateOption.GenerateUniqueName:
                            {
                                string UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.Folder));

                                if (await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(UniquePath)))
                                {
                                    if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(UniquePath, true)) is FtpListItem Item)
                                    {
                                        return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, 0, Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                                    }
                                }

                                break;
                            }
                        case CreateOption.ReplaceExisting:
                            {
                                await Controller.RunCommandAsync((Client) => Client.DeleteDirectoryAsync(TargetPath));

                                if (await Controller.RunCommandAsync((Client) => Client.CreateDirectoryAsync(TargetPath)))
                                {
                                    if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FTPStorageFolder(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, 0, Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
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
                                if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), TargetPath, FtpRemoteExists.Skip)) != FtpStatus.Failed)
                                {
                                    if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, Convert.ToUInt64(Item.Size), Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                                    }
                                }

                                break;
                            }
                        case CreateOption.GenerateUniqueName:
                            {
                                string UniquePath = await Controller.RunCommandAsync((Client) => Client.GenerateUniquePathAsync(TargetPath, CreateType.File));

                                if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), UniquePath, FtpRemoteExists.NoCheck)) == FtpStatus.Success)
                                {
                                    if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(UniquePath, true)) is FtpListItem Item)
                                    {
                                        return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, Convert.ToUInt64(Item.Size), Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
                                    }
                                }

                                break;
                            }
                        case CreateOption.ReplaceExisting:
                            {
                                if (await Controller.RunCommandAsync((Client) => Client.UploadAsync(Array.Empty<byte>(), TargetPath, FtpRemoteExists.Overwrite)) == FtpStatus.Success)
                                {
                                    if (await Controller.RunCommandAsync((Client) => Client.GetObjectInfoAsync(TargetPath, true)) is FtpListItem Item)
                                    {
                                        return new FTPStorageFile(Controller, new FTPFileData(System.IO.Path.Combine(Path, Item.Name), Item.FullName, Convert.ToUInt64(Item.Size), Item.OwnerPermissions, Item.Modified.ToLocalTime(), Item.Created.ToLocalTime()));
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

        public override async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItems = false, bool IncludeSystemItems = false, BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            foreach (FtpListItem Item in await Controller.RunCommandAsync((Client) => Client.GetListingAsync(Data.RelatedPath)))
            {
                if (Item.Type.HasFlag(FtpFileSystemObjectType.Directory))
                {
                    if (Filter.HasFlag(BasicFilters.Folder))
                    {
                        return true;
                    }
                }
                else
                {
                    if (Filter.HasFlag(BasicFilters.File))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public Task<FTPFileData> GetRawDataAsync()
        {
            return Task.FromResult(Data);
        }

        public FTPStorageFolder(FTPClientController Controller, FTPFileData Data) : base(Data)
        {
            this.Data = Data;
            this.Controller = Controller;
        }
    }
}
