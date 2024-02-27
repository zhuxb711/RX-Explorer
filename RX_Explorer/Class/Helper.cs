using RX_Explorer.View;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public static class Helper
    {
        public static bool IsEmail(string Email)
        {
            return !string.IsNullOrEmpty(Email) && Regex.IsMatch(Email, @"(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*"")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(?:(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9]))\.){3}(?:(2(5[0-5]|[0-4][0-9])|1[0-9][0-9]|[1-9]?[0-9])|[a-z0-9-]*[a-z0-9]:(?:[\x01-\x08\x0b\x0c\x0e-\x1f\x21-\x5a\x53-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])+)\])");
        }

        public static void AbsorbException(Action Action)
        {
            try
            {
                Action();
            }
            catch (Exception)
            {
                // No need to handle this exception
            }
        }

        public static T AbsorbException<T>(Func<T> Action, T Default = default)
        {
            if (typeof(Task).IsAssignableFrom(typeof(T)))
            {
                throw new Exception($"Call '{nameof(AbsorbExceptionAsync)}' instead");
            }

            try
            {
                return Action();
            }
            catch (Exception)
            {
                // No need to handle this exception
            }

            return Default;
        }

        public static async Task AbsorbExceptionAsync(Func<Task> Action)
        {
            try
            {
                await Action();
            }
            catch (Exception)
            {
                // No need to handle this exception
            }
        }

        public static async Task<T> AbsorbExceptionAsync<T>(Func<Task<T>> Action, T Default = default)
        {
            try
            {
                return await Action();
            }
            catch (Exception)
            {
                // No need to handle this exception
            }

            return Default;
        }

        public static async Task<string> GetExecuteableFileDisplayNameAsync(string Path)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Path) is FileSystemStorageFile File)
            {
                return await GetExecuteableFileDisplayNameAsync(File);
            }

            return string.Empty;
        }

        public static async Task<string> GetExecuteableFileDisplayNameAsync(FileSystemStorageFile File)
        {
            IReadOnlyDictionary<string, string> PropertiesDic = await File.GetPropertiesAsync(new string[] { "System.FileDescription" });

            if (PropertiesDic.TryGetValue("System.FileDescription", out string Description))
            {
                return string.IsNullOrEmpty(Description) ? (File.DisplayName.EndsWith(File.Type, StringComparison.OrdinalIgnoreCase)
                                                                            ? Path.GetFileNameWithoutExtension(File.DisplayName)
                                                                            : File.DisplayName)
                                                         : Description;
            }

            return string.Empty;
        }

        public static bool GetSuitableInnerViewerPageType(FileSystemStorageFile File, out Type PageType)
        {
            switch (File.Type.ToLower())
            {
                case ".jpg" or ".jpeg" or ".png" or ".bmp":
                    {
                        PageType = typeof(PhotoViewer);
                        return true;
                    }
                case ".mkv" or ".mp4" or ".mp3" or ".flac" or ".wma" or ".wmv" or ".m4a" or ".mov" or ".alac":
                    {
                        PageType = typeof(MediaPlayer);
                        return true;
                    }
                case ".txt":
                    {
                        PageType = typeof(TextViewer);
                        return true;
                    }
                case ".pdf":
                    {
                        PageType = typeof(PdfReader);
                        return true;
                    }
                case ".zip":
                    {
                        PageType = typeof(CompressionViewer);
                        return true;
                    }
                default:
                    {
                        PageType = null;
                        return false;
                    }
            }
        }

        public static async Task<byte[]> GetByteArrayFromRandomAccessStreamAsync(IRandomAccessStream Stream)
        {
            using (MemoryStream TempStream = new MemoryStream())
            {
                await Stream.AsStreamForRead().CopyToAsync(TempStream);
                return TempStream.ToArray();
            }
        }

        public static async Task<InMemoryRandomAccessStream> CreateRandomAccessStreamAsync(byte[] Data)
        {
            InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream();
            await Stream.WriteAsync(Data.AsBuffer());
            Stream.Seek(0);
            return Stream;
        }

        public static async Task<BitmapImage> CreateBitmapImageAsync(byte[] Data, int DecodePixelHeight = 0, int DecodePixelWidth = 0)
        {
            using (InMemoryRandomAccessStream Stream = await CreateRandomAccessStreamAsync(Data))
            {
                return await CreateBitmapImageAsync(Stream, DecodePixelHeight, DecodePixelWidth);
            }
        }

        public static async Task<BitmapImage> CreateBitmapImageAsync(IRandomAccessStream Stream, int DecodePixelHeight = 0, int DecodePixelWidth = 0)
        {
            BitmapImage Bitmap = new BitmapImage();

            if (DecodePixelHeight > 0)
            {
                Bitmap.DecodePixelHeight = DecodePixelHeight;
            }

            if (DecodePixelWidth > 0)
            {
                Bitmap.DecodePixelWidth = DecodePixelWidth;
            }

            await Bitmap.SetSourceAsync(Stream).AsTask().ConfigureAwait(false);

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
