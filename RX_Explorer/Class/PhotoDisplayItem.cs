using SharedLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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

        public bool IsErrorInLoading { get; private set; }

        public FileSystemStorageFile PhotoFile { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        private int ActualLoaded;
        private int ThumbnailLoaded;

        public PhotoDisplayItem(FileSystemStorageFile PhotoFile)
        {
            this.PhotoFile = PhotoFile;
        }

        public PhotoDisplayItem(BitmapImage Image)
        {
            ActualSource = Image;
            ThumbnailSource = Image;
            Interlocked.Exchange(ref ActualLoaded, 1);
            Interlocked.Exchange(ref ThumbnailLoaded, 1);
        }

        public async Task GenerateActualSourceAsync(bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                Interlocked.CompareExchange(ref ActualLoaded, 0, 1);
            }

            if (Interlocked.CompareExchange(ref ActualLoaded, 1, 0) == 0)
            {
                if (PhotoFile != null)
                {
                    try
                    {
                        using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                        {
                            ActualSource = await Helper.CreateBitmapImageAsync(ActualStream.AsRandomAccessStream());
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
        }

        public async Task GenerateThumbnailAsync(bool ForceUpdate = false)
        {
            if (ForceUpdate)
            {
                Interlocked.CompareExchange(ref ThumbnailLoaded, 0, 1);
            }

            if (Interlocked.CompareExchange(ref ThumbnailLoaded, 1, 0) == 0)
            {
                if (PhotoFile != null)
                {
                    try
                    {
                        try
                        {
                            using (IRandomAccessStream ThumbnailStream = await PhotoFile.GetThumbnailRawStreamAsync(ThumbnailMode.PicturesView, ForceUpdate))
                            {
                                ThumbnailSource = await Helper.CreateBitmapImageAsync(ThumbnailStream);
                            }
                        }
                        catch (Exception)
                        {
                            using (Stream ActualStream = await PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                            {
                                ThumbnailSource = await Helper.CreateBitmapImageAsync(ActualStream.AsRandomAccessStream());
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
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
