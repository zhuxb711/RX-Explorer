using Microsoft.Win32.SafeHandles;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPStorageFolder : FileSystemStorageFolder
    {
        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            return Task.FromResult(new BitmapImage());
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            return null;
        }

        protected override Task LoadCoreAsync(bool ForceUpdate)
        {
            return base.LoadCoreAsync(ForceUpdate);
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override async Task<IReadOnlyList<FileSystemStorageItemBase>> GetChildItemsAsync(bool IncludeHiddenItems = false,
                                                                                                bool IncludeSystemItems = false,
                                                                                                bool IncludeAllSubItems = false,
                                                                                                uint MaxNumLimit = uint.MaxValue,
                                                                                                CancellationToken CancelToken = default,
                                                                                                Func<string, bool> AdvanceFilter = null,
                                                                                                BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                foreach (MTP_File_Data Data in await Exclusive.Controller.GetMTPChildItemsDataAsync(Path, IncludeHiddenItems, IncludeSystemItems, IncludeAllSubItems, MaxNumLimit, Filter, CancelToken))
                {
                    if (Data.Attributes.HasFlag(System.IO.FileAttributes.Directory))
                    {
                        Result.Add(new MTPStorageFolder(Data));
                    }
                    else
                    {

                    }
                }
            }

            return Result;
        }

        public override async Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, StorageItemTypes ItemTypes, CreateOption Option)
        {
            return null;
        }

        public override async Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            return 0;
        }

        public override async Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false,
                                                                   bool IncludeSystemItem = false,
                                                                   BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            return true;
        }

        public override Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            return Task.FromResult(new SafeFileHandle(IntPtr.Zero, true));
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        public MTPStorageFolder(MTP_File_Data Data) : base(Data)
        {

        }
    }
}
