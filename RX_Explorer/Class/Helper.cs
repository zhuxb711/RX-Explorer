using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public static class Helper
    {
        public static async Task<InMemoryRandomAccessStream> CreateRandomAccessStreamAsync(byte[] Data)
        {
            InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream();
            await Stream.WriteAsync(Data.AsBuffer());
            Stream.Seek(0);
            return Stream;
        }

        public static async Task<BitmapImage> CreateBitmapImageAsync(byte[] Data)
        {
            using (InMemoryRandomAccessStream Stream = await CreateRandomAccessStreamAsync(Data))
            {
                return await CreateBitmapImageAsync(Stream);
            }
        }

        public static async Task<BitmapImage> CreateBitmapImageAsync(IRandomAccessStream Stream)
        {
            BitmapImage Bitmap = new BitmapImage();
            await Bitmap.SetSourceAsync(Stream);
            return Bitmap;
        }

        public static async Task<IRandomAccessStream> GetThumbnailFromStreamAsync(IRandomAccessStream InputStream, uint RequestedSize)
        {
            if (InputStream == null)
            {
                return null;
            }

            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(InputStream);

            if (Decoder.PixelWidth < RequestedSize || Decoder.PixelHeight < RequestedSize)
            {
                return InputStream;
            }

            InMemoryRandomAccessStream OutputStream = new InMemoryRandomAccessStream();

            try
            {
                BitmapEncoder Encoder = await BitmapEncoder.CreateForTranscodingAsync(OutputStream, Decoder);

                Encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Linear;

                if (Decoder.PixelWidth > Decoder.PixelHeight)
                {
                    Encoder.BitmapTransform.ScaledHeight = RequestedSize;
                    Encoder.BitmapTransform.ScaledWidth = Convert.ToUInt32((double)RequestedSize / Decoder.PixelHeight * Decoder.PixelWidth);
                }
                else
                {
                    Encoder.BitmapTransform.ScaledWidth = RequestedSize;
                    Encoder.BitmapTransform.ScaledHeight = Convert.ToUInt32((double)RequestedSize / Decoder.PixelWidth * Decoder.PixelHeight);
                }

                await Encoder.FlushAsync();
            }
            catch (Exception)
            {
                OutputStream.Dispose();
                throw new NotSupportedException("Could not generate the thumbnail for the image");
            }

            return OutputStream;
        }
    }
}
