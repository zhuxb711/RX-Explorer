using ComputerVision;
using ShareClassLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class PhotoDisplayItem : INotifyPropertyChanged
    {
        public BitmapImage ActualSource { get; }

        public BitmapImage ThumbnailSource { get; }

        public string FileName => PhotoFile.Name;

        public int RotateAngle { get; set; }

        public bool IsErrorInLoading { get; private set; }

        public FileSystemStorageFile PhotoFile { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private int ActualLoaded;
        private int ThumbnailLoaded;

        public PhotoDisplayItem(FileSystemStorageFile Item) : this()
        {
            PhotoFile = Item;
        }

        public PhotoDisplayItem(BitmapImage Image) : this()
        {
            ActualSource = Image;
        }

        private PhotoDisplayItem()
        {
            ActualSource = new BitmapImage();
            ThumbnailSource = new BitmapImage();
        }

        public async Task GenerateActualSourceAsync()
        {
            if (Interlocked.CompareExchange(ref ActualLoaded, 1, 0) == 0)
            {
                try
                {
                    using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                    {
                        if (ActualStream != null)
                        {
                            await ActualSource.SetSourceAsync(ActualStream.AsRandomAccessStream());
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsErrorInLoading = true;
                    OnPropertyChanged(nameof(IsErrorInLoading));
                    LogTracer.Log(ex, "Could not get the image data from file");
                }
                finally
                {
                    OnPropertyChanged(nameof(ActualSource));
                }
            }
        }

        public async Task GenerateThumbnailAsync()
        {
            if (Interlocked.CompareExchange(ref ThumbnailLoaded, 1, 0) == 0)
            {
                try
                {
                    bool ThumbnailFailed = false;

                    using (IRandomAccessStream ThumbnailStream = await PhotoFile.GetThumbnailRawStreamAsync(ThumbnailMode.PicturesView))
                    {
                        if (ThumbnailStream != null)
                        {
                            await ThumbnailSource.SetSourceAsync(ThumbnailStream);
                        }
                        else
                        {
                            ThumbnailFailed = true;
                        }
                    }

                    if (ThumbnailFailed)
                    {
                        using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                        {
                            if (ActualStream != null)
                            {
                                await ThumbnailSource.SetSourceAsync(ActualStream.AsRandomAccessStream());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the thumbnail data from file");
                }
                finally
                {
                    OnPropertyChanged(nameof(ThumbnailSource));
                }
            }
        }

        public async Task<SoftwareBitmap> GenerateImageWithRotation()
        {
            try
            {
                using (Stream Stream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                {
                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(Stream.AsRandomAccessStream());

                    switch (RotateAngle % 360)
                    {
                        case 0:
                            {
                                return await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            }
                        case 90:
                            {
                                using (SoftwareBitmap Origin = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, 90);
                                }
                            }
                        case 180:
                            {
                                using (SoftwareBitmap Origin = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, 180);
                                }
                            }
                        case 270:
                            {
                                using (SoftwareBitmap Origin = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                {
                                    return ComputerVisionProvider.RotateEffect(Origin, -90);
                                }
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the image with specific rotation");
            }

            return null;
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
