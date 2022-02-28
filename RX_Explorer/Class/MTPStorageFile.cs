using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPStorageFile : FileSystemStorageFile, IMTPStorageItem
    {
        private MTPFileData RawData;
        private readonly MTPStorageFolder ParentFolder;
        private string InnerDisplayType;

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        public override bool IsReadOnly => RawData.IsReadOnly;

        public override bool IsSystemItem => RawData.IsSystemItem;

        public override ulong Size
        {
            get => RawData?.Size ?? base.Size;
            protected set => base.Size = value;
        }

        public override DateTimeOffset CreationTime 
        { 
            get => RawData?.CreationTime ?? base.CreationTime; 
            protected set => base.CreationTime = value; 
        }

        public override DateTimeOffset ModifiedTime 
        {
            get => RawData?.ModifiedTime ?? base.ModifiedTime;
            protected set => base.ModifiedTime = value; 
        }

        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                if (await Item.GetThumbnailBitmapAsync(Mode) is BitmapImage Thumbnail)
                {
                    return Thumbnail;
                }
            }

            return null;
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                return await Item.GetThumbnailRawStreamAsync(Mode);
            }

            return null;
        }

        public override async Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                SafeFileHandle Handle = Item.GetSafeFileHandle(Mode, Option);

                if (!Handle.IsInvalid)
                {
                    return Handle;
                }
            }

            return null;
        }

        protected override Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            return Task.FromResult<BitmapImage>(null);
        }

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
            {
                if (ControllerRef != null)
                {
                    InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
            }

            if (RawData == null || ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }
        }

        public override async Task<FileStream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            FileAccess Access = Mode switch
            {
                AccessMode.Read => FileAccess.Read,
                AccessMode.ReadWrite or AccessMode.Exclusive => FileAccess.ReadWrite,
                AccessMode.Write => FileAccess.Write,
                _ => throw new NotSupportedException()
            };

            SafeFileHandle Handle = await GetNativeHandleAsync(Mode, Option);

            if ((Handle?.IsInvalid).GetValueOrDefault(true))
            {
                throw new UnauthorizedAccessException($"Could not create a new file stream, Path: \"{Path}\"");
            }
            else
            {
                return new FileStream(Handle, Access);
            }
        }

        public override Task<ulong> GetSizeOnDiskAsync()
        {
            return Task.FromResult<ulong>(0);
        }

        public override async Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            if (await GetStorageItemAsync() is StorageFile File)
            {
                return await File.OpenTransactedWriteAsync();
            }

            return null;
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            if (ParentFolder != null)
            {
                if (await ParentFolder.GetStorageItemAsync() is StorageFolder Folder)
                {
                    if (await Folder.TryGetItemAsync(Name) is IStorageItem Item)
                    {
                        return Item;
                    }
                }
            }
            else if (StorageDevice.FromId(DeviceId) is StorageFolder RootFolder)
            {
                return await RootFolder.GetStorageItemByTraverse<StorageFile>(new PathAnalysis(Path, DeviceId));
            }

            return null;
        }

        public async Task<MTPFileData> GetRawDataAsync()
        {
            try
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetMTPItemDataAsync(Path);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetMTPItemDataAsync(Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(GetRawDataAsync)}");
            }

            return null;
        }

        public MTPStorageFile(MTPFileData Data) : base(Data)
        {
            RawData = Data ?? throw new ArgumentNullException(nameof(Data));
        }

        public MTPStorageFile(MTPFileData Data, MTPStorageFolder Parent) : base(Data)
        {
            RawData = Data ?? throw new ArgumentNullException(nameof(Data));
            ParentFolder = Parent ?? throw new ArgumentNullException(nameof(Parent));
        }
    }
}
