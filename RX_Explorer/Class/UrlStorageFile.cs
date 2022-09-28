using RX_Explorer.Interface;
using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class UrlStorageFile : FileSystemStorageFile, IUrlStorageFile
    {
        public string UrlTargetPath => (RawData?.UrlTargetPath) ?? Globalization.GetString("UnknownText");

        protected UrlFileData RawData { get; set; }

        public override string DisplayType => Globalization.GetString("Url_Admin_DisplayType");

        public async Task<UrlFileData> GetRawDataAsync()
        {
            if (GetBulkAccessSharedController(out var ControllerRef))
            {
                using (ControllerRef)
                {
                    return await ControllerRef.Value.Controller.GetUrlDataAsync(Path);
                }
            }
            else
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.Low))
                {
                    return await Exclusive.Controller.GetUrlDataAsync(Path);
                }
            }
        }

        public async Task<bool> LaunchAsync()
        {
            if (!string.IsNullOrWhiteSpace(UrlTargetPath))
            {
                if (Uri.TryCreate(UrlTargetPath, UriKind.RelativeOrAbsolute, out Uri Url))
                {
                    return await Launcher.LaunchUriAsync(Url);
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        protected override async Task<StorageFile> GetStorageItemCoreAsync()
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
            if (RawData == null || ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }

            await base.LoadCoreAsync(ForceUpdate);
        }

        protected override async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                return await Helper.CreateRandomAccessStreamAsync(RawData.IconData);
            }

            StorageFile ThumbnailFile = await StorageFile.GetFileFromApplicationUriAsync(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                                                                ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                                                                                : new Uri("ms-appx:///Assets/SingleItem_Black.png"));
            return await ThumbnailFile.OpenReadAsync();
        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode, bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                RawData = await GetRawDataAsync();
            }

            if ((RawData?.IconData.Length).GetValueOrDefault() > 0)
            {
                return await Helper.CreateBitmapImageAsync(RawData.IconData);
            }

            return new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark
                                                        ? new Uri("ms-appx:///Assets/SingleItem_White.png")
                                                        : new Uri("ms-appx:///Assets/SingleItem_Black.png"));
        }

        public UrlStorageFile(NativeFileData Data) : base(Data)
        {

        }
    }
}
