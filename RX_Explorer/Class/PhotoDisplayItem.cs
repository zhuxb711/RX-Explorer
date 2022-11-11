using PropertyChanged;
using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class PhotoDisplayItem
    {
        public string FileName => PhotoFile.Name;

        public BitmapImage ActualSource { get; private set; }

        public BitmapImage ThumbnailSource { get; private set; }

        public bool DidErrorThrewOnLoading { get; private set; }

        public FileSystemStorageFile PhotoFile { get; }

        public async Task GenerateActualSourceAsync(bool ForceUpdate = false)
        {
            async Task<BitmapImage> GenerateActualSourceCoreAsync()
            {
                try
                {
                    using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                    {
                        return await Helper.CreateBitmapImageAsync(ActualStream.AsRandomAccessStream());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the image data from file");
                }

                return null;
            }

            if (PhotoFile != null)
            {
                if (ForceUpdate)
                {
                    if (await GenerateActualSourceCoreAsync() is BitmapImage Thumbnail)
                    {
                        ActualSource = Thumbnail;
                    }
                    else
                    {
                        DidErrorThrewOnLoading = true;
                    }
                }
                else if (ActualSource is null)
                {
                    if (await Execution.ExecuteOnceAsync(this, GenerateActualSourceCoreAsync) is BitmapImage Thumbnail)
                    {
                        ActualSource = Thumbnail;
                    }
                    else
                    {
                        DidErrorThrewOnLoading = true;
                    }
                }
            }
        }

        public async Task GenerateThumbnailAsync(bool ForceUpdate = false)
        {
            async Task<BitmapImage> GenerateThumbnailCoreAsync()
            {
                try
                {
                    try
                    {
                        using (IRandomAccessStream ThumbnailStream = await PhotoFile.GetThumbnailRawStreamAsync(ThumbnailMode.PicturesView, ForceUpdate))
                        {
                            return await Helper.CreateBitmapImageAsync(ThumbnailStream);
                        }
                    }
                    catch (Exception)
                    {
                        using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                        {
                            return await Helper.CreateBitmapImageAsync(ActualStream.AsRandomAccessStream());
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the thumbnail data from file");
                }

                return null;
            }

            if (PhotoFile != null)
            {
                if (ForceUpdate)
                {
                    if (await GenerateThumbnailCoreAsync() is BitmapImage Thumbnail)
                    {
                        ThumbnailSource = Thumbnail;
                    }
                }
                else if (ThumbnailSource == null)
                {
                    if (await Execution.ExecuteOnceAsync(this, GenerateThumbnailCoreAsync) is BitmapImage Thumbnail)
                    {
                        ThumbnailSource = Thumbnail;
                    }
                }
            }
        }

        public PhotoDisplayItem(FileSystemStorageFile PhotoFile)
        {
            this.PhotoFile = PhotoFile;
        }

        public PhotoDisplayItem(BitmapImage Image)
        {
            ActualSource = Image;
            ThumbnailSource = Image;
        }
    }
}
