using FluentFTP;
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
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    internal class FTPStorageFile : FileSystemStorageFile, IFTPStorageItem
    {
        private readonly FTPClientController Controller;
        private readonly FTPFileData Data;
        private string InnerDisplayType;

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
            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetBulkAccessSharedController())
            {
                if (ControllerRef != null)
                {
                    InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
            }
        }

        public override async Task<Stream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            Stream Stream = await CreateOneTimeFileStreamAsync();

            if (!await Controller.RunCommandAsync((Client) => Client.DownloadAsync(Stream, Data.RelatedPath)))
            {
                throw new InvalidDataException();
            }

            Stream.Seek(0, SeekOrigin.Begin);

            return new FTPFileSaveOnFlushStream(Path, Controller, Stream);
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

        public FTPStorageFile(FTPClientController Controller, FTPFileData Data) : base(Data)
        {
            this.Data = Data;
            this.Controller = Controller;
        }
    }
}
