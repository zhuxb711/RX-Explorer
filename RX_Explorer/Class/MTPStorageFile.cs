using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
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
        private string InnerDisplayType;
        private readonly MTPStorageFolder Parent;

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

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
            FileAccess Access = Mode switch
            {
                AccessMode.Read => FileAccess.Read,
                AccessMode.ReadWrite or AccessMode.Exclusive => FileAccess.ReadWrite,
                AccessMode.Write => FileAccess.Write,
                _ => throw new NotSupportedException()
            };

            SafeFileHandle Handle;

            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    Handle = await ControllerRef.Value.Controller.MTPDownloadAndGetHandleAsync(Path, Mode, Option);
                }
            }
            else
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    Handle = await Exclusive.Controller.MTPDownloadAndGetHandleAsync(Path, Mode, Option);
                }
            }

            if ((Handle?.IsInvalid).GetValueOrDefault(true))
            {
                throw new UnauthorizedAccessException($"Could not create a new file stream, Path: \"{Path}\"");
            }
            else
            {
                return new MTPFileSaveOnFlushStream(Path, new FileStream(Handle, Access, 4096, true));
            }
        }

        public override Task<ulong> GetSizeOnDiskAsync()
        {
            return Task.FromResult(Size);
        }

        public override Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Properties.Select((Prop) => new KeyValuePair<string, string>(Prop, string.Empty))));
        }

        public override async Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                if (StorageItem != null)
                {
                    return StorageItem;
                }
                else
                {
                    if (Parent != null)
                    {
                        if (await Parent.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            if (await Folder.TryGetItemAsync(Name) is StorageFile Item)
                            {
                                return StorageItem = Item;
                            }
                        }
                    }
                    else if (await Task.Run(() => StorageDevice.FromId(DeviceId)) is StorageFolder RootFolder)
                    {
                        return StorageItem = await RootFolder.GetStorageItemByTraverse<StorageFile>(new PathAnalysis(Path, DeviceId));
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFile, Path: {Path}");
            }

            return null;
        }

        public async Task<MTPFileData> GetRawDataAsync()
        {
            try
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        return await ControllerRef.Value.Controller.GetMTPItemDataAsync(Path);
                    }
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.GetMTPItemDataAsync(Path);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(GetRawDataAsync)}");
            }

            return null;
        }

        public MTPStorageFile(MTPFileData Data) : this(Data, null)
        {

        }

        public MTPStorageFile(MTPFileData Data, MTPStorageFolder Parent) : base(Data)
        {
            this.Parent = Parent;
        }
    }
}
