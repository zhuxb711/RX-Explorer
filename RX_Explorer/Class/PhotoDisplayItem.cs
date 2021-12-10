using ComputerVision;
using ShareClassLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class PhotoDisplayItem : INotifyPropertyChanged
    {
        public BitmapImage ActualSource { get; private set; }

        public BitmapImage ThumbnailSource { get; private set; }

        public string FileName => PhotoFile.Name;

        public int RotateAngle { get; set; }

        public bool IsErrorInLoading { get; private set; }

        public FileSystemStorageFile PhotoFile { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public PhotoDisplayItem(FileSystemStorageFile Item)
        {
            PhotoFile = Item;
        }

        public PhotoDisplayItem(BitmapImage Image)
        {
            ActualSource = Image;
        }

        public async Task GenerateActualSourceAsync()
        {
            if (ActualSource == null)
            {
                try
                {
                    BitmapImage TempImage = new BitmapImage();

                    using (FileStream Stream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Optimize_RandomAccess))
                    {
                        await TempImage.SetSourceAsync(Stream.AsRandomAccessStream());
                    }

                    ActualSource = TempImage;

                    OnPropertyChanged(nameof(ActualSource));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the image data from file");
                    IsErrorInLoading = true;
                    OnPropertyChanged(nameof(IsErrorInLoading));
                }
            }
        }

        public async Task GenerateThumbnailAsync()
        {
            if (ThumbnailSource == null)
            {
                try
                {
                    ThumbnailSource = await PhotoFile.GetThumbnailAsync(ThumbnailMode.PicturesView);
                    OnPropertyChanged(nameof(ThumbnailSource));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the thumbnail data from file");
                }
            }
        }

        public async Task<SoftwareBitmap> GenerateImageWithRotation()
        {
            try
            {
                using (FileStream Stream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Optimize_RandomAccess))
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
                        default:
                            {
                                return null;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not generate the image with specific rotation");
                return null;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
