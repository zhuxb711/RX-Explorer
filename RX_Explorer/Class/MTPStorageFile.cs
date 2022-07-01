using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPStorageFile : FileSystemStorageFile, IMTPStorageItem
    {
        private string InnerDisplayType;

        public override string DisplayType => string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType;

        public string DeviceId => @$"\\?\{new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()}";

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
            async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(FullTrustProcessController.Exclusive Exclusive)
            {
                if (await Exclusive.Controller.GetThumbnailAsync(Type) is Stream ThumbnailStream)
                {
                    return ThumbnailStream.AsRandomAccessStream();
                }

                throw new NotSupportedException();
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
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await GetThumbnailRawStreamCoreAsync(Exclusive);
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
                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
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
                        LogTracer.Log(ex, $"Could not create streamed file for mtp file: {Path}");
                        Request.FailAndClose(StreamedFileFailureMode.Incomplete);
                    }
                }, Reference);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get the storage item for mtp file: {Path}");
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
                    using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync(PriorityLevel.Low))
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

        public MTPStorageFile(MTPFileData Data) : base(Data)
        {

        }
    }
}
