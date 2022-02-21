using Microsoft.Win32.SafeHandles;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
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
    public class MTPStorageFolder : FileSystemStorageFolder
    {
        private MTP_File_Data RawData;

        public override string DisplayName => Name;

        public override bool IsReadOnly => RawData.IsReadOnly;

        public override bool IsSystemItem => RawData.IsSystemItem;

        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            if (RawData.IconData.Length > 0)
            {
                BitmapImage Thumbnail = new BitmapImage();

                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    await Thumbnail.SetSourceAsync(IconStream.AsRandomAccessStream());
                }

                return Thumbnail;
            }

            return null;
        }

        protected override Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            if (RawData.IconData.Length > 0)
            {
                using (MemoryStream IconStream = new MemoryStream(RawData.IconData))
                {
                    return Task.FromResult(IconStream.AsRandomAccessStream());
                }
            }

            return null;
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                try
                {
                    using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                    {
                        if (ControllerRef != null)
                        {
                            RawData = await ControllerRef.Value.Controller.GetMTPItemDataAsync(Path);
                        }
                        else
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                RawData = await ControllerRef.Value.Controller.GetMTPItemDataAsync(Path);
                            }
                        }
                    }

                    ModifiedTime = RawData.ModifiedTime;
                    CreationTime = RawData.CreationTime;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(LoadCoreAsync)}");
                }
            }
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
                        Result.Add(new MTPStorageFile(Data));
                    }
                }
            }

            return Result;
        }

        public override Task<FileSystemStorageItemBase> CreateNewSubItemAsync(string Name, StorageItemTypes ItemTypes, CreateOption Option)
        {
            return Task.FromResult<FileSystemStorageItemBase>(null);
        }

        public override Task<ulong> GetFolderSizeAsync(CancellationToken CancelToken = default)
        {
            return Task.FromResult<ulong>(0);
        }

        public override Task<bool> CheckContainsAnyItemAsync(bool IncludeHiddenItem = false,
                                                                   bool IncludeSystemItem = false,
                                                                   BasicFilters Filter = BasicFilters.File | BasicFilters.Folder)
        {
            return Task.FromResult<bool>(true);
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
            RawData = Data ?? throw new ArgumentNullException(nameof(Data));
        }
    }
}
