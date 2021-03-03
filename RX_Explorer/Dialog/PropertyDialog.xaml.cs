using RX_Explorer.Class;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class PropertyDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string Path { get; private set; }

        public string FileSize { get; private set; }

        public string CreateTime { get; private set; }

        public string ChangeTime { get; private set; }

        public string TargetPath { get; private set; }

        public string Include { get; private set; }

        private FileSystemStorageItemBase Item;

        public event PropertyChangedEventHandler PropertyChanged;

        private CancellationTokenSource Cancellation;

        public PropertyDialog(FileSystemStorageItemBase Item)
        {
            InitializeComponent();
            this.Item = Item;
            Loading += PropertyDialog_Loading;
        }

        private async void PropertyDialog_Loading(FrameworkElement sender, object args)
        {
            if (Item.StorageType == StorageItemTypes.File)
            {
                IncludeArea.Visibility = Visibility.Collapsed;

                FileName = Item.Name;
                Path = Item.Path;
                FileType = $"{Item.DisplayType} ({Item.Type})";
                CreateTime = Item.CreationTimeRaw.ToString("F");
                ChangeTime = Item.ModifiedTimeRaw.ToString("F");
                FileSize = Item.Size + " (" + Item.SizeRaw.ToString("N0") + $" {Globalization.GetString("Device_Capacity_Unit")})";

                if (Item is HyperlinkStorageItem LinkItem)
                {
                    LinkTargetArea.Visibility = Visibility.Visible;
                    ExtraDataArea.Visibility = Visibility.Collapsed;

                    TargetPath = LinkItem.LinkTargetPath;
                }
                else
                {
                    if (await Item.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
                    {
                        if (File.ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                        {
                            VideoProperties Video = await File.Properties.GetVideoPropertiesAsync();
                            ExtraData.Text = $"{Globalization.GetString("FileProperty_Resolution")}: {((Video.Width == 0 && Video.Height == 0) ? Globalization.GetString("UnknownText") : $"{Video.Width}×{Video.Height}")}{Environment.NewLine}{Globalization.GetString("FileProperty_Bitrate")}: {(Video.Bitrate == 0 ? Globalization.GetString("UnknownText") : (Video.Bitrate / 1024f < 1024 ? Math.Round(Video.Bitrate / 1024f, 2).ToString("0.00") + " Kbps" : Math.Round(Video.Bitrate / 1048576f, 2).ToString("0.00") + " Mbps"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Duration")}: {ConvertTimsSpanToString(Video.Duration)}";
                        }
                        else if (File.ContentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
                        {
                            MusicProperties Music = await File.Properties.GetMusicPropertiesAsync();
                            ExtraData.Text = $"{Globalization.GetString("FileProperty_Bitrate")}: {(Music.Bitrate == 0 ? Globalization.GetString("UnknownText") : (Music.Bitrate / 1024f < 1024 ? Math.Round(Music.Bitrate / 1024f, 2).ToString("0.00") + " Kbps" : Math.Round(Music.Bitrate / 1048576f, 2).ToString("0.00") + " Mbps"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Duration")}: {ConvertTimsSpanToString(Music.Duration)}";
                        }
                        else if (File.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                        {
                            ImageProperties Image = await File.Properties.GetImagePropertiesAsync();
                            ExtraData.Text = $"{Globalization.GetString("FileProperty_Resolution")}: {((Image.Width == 0 && Image.Height == 0) ? Globalization.GetString("UnknownText") : $"{Image.Width}×{Image.Height}")}{Environment.NewLine}{Globalization.GetString("FileProperty_ShootingDate")}: {Image.DateTaken.ToLocalTime():F}{Environment.NewLine}{Globalization.GetString("FileProperty_Longitude")}: {(Image.Longitude.HasValue ? Image.Longitude.Value.ToString() : Globalization.GetString("UnknownText"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Latitude")}: {(Image.Latitude.HasValue ? Image.Latitude.Value.ToString() : Globalization.GetString("UnknownText"))}";
                        }
                        else
                        {
                            switch (File.FileType.ToLower())
                            {
                                case ".flv":
                                case ".rmvb":
                                case ".rm":
                                case ".mov":
                                case ".mkv":
                                case ".mp4":
                                case ".m4v":
                                case ".m2ts":
                                case ".wmv":
                                case ".f4v":
                                case ".ts":
                                case ".swf":
                                    {
                                        VideoProperties Video = await File.Properties.GetVideoPropertiesAsync();
                                        ExtraData.Text = $"{Globalization.GetString("FileProperty_Resolution")}: {((Video.Width == 0 && Video.Height == 0) ? Globalization.GetString("UnknownText") : $"{Video.Width}×{Video.Height}")}{Environment.NewLine}{Globalization.GetString("FileProperty_Bitrate")}: {(Video.Bitrate == 0 ? Globalization.GetString("UnknownText") : (Video.Bitrate / 1024f < 1024 ? Math.Round(Video.Bitrate / 1024f, 2).ToString("0.00") + " Kbps" : Math.Round(Video.Bitrate / 1048576f, 2).ToString("0.00") + " Mbps"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Duration")}: {ConvertTimsSpanToString(Video.Duration)}";
                                        break;
                                    }
                                case ".mpe":
                                case ".flac":
                                case ".mp3":
                                case ".aac":
                                case ".wma":
                                case ".ogg":
                                    {
                                        MusicProperties Music = await File.Properties.GetMusicPropertiesAsync();
                                        ExtraData.Text = $"{Globalization.GetString("FileProperty_Bitrate")}: {(Music.Bitrate == 0 ? Globalization.GetString("UnknownText") : (Music.Bitrate / 1024f < 1024 ? Math.Round(Music.Bitrate / 1024f, 2).ToString("0.00") + " Kbps" : Math.Round(Music.Bitrate / 1048576f, 2).ToString("0.00") + " Mbps"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Duration")}: {ConvertTimsSpanToString(Music.Duration)}";
                                        break;
                                    }
                                case ".raw":
                                case ".bmp":
                                case ".tiff":
                                case ".gif":
                                case ".jpg":
                                case ".jpeg":
                                case ".exif":
                                case ".png":
                                    {
                                        ImageProperties Image = await File.Properties.GetImagePropertiesAsync();
                                        ExtraData.Text = $"{Globalization.GetString("FileProperty_Resolution")}: {((Image.Width == 0 && Image.Height == 0) ? Globalization.GetString("UnknownText") : $"{Image.Width}×{Image.Height}")}{Environment.NewLine}{Globalization.GetString("FileProperty_ShootingDate")}: {Image.DateTaken.ToLocalTime():F}{Environment.NewLine}{Globalization.GetString("FileProperty_Longitude")}: {(Image.Longitude.HasValue ? Image.Longitude.Value.ToString() : Globalization.GetString("UnknownText"))}{Environment.NewLine}{Globalization.GetString("FileProperty_Latitude")}: {(Image.Latitude.HasValue ? Image.Latitude.Value.ToString() : Globalization.GetString("UnknownText"))}";
                                        break;
                                    }
                                default:
                                    {
                                        ExtraDataArea.Visibility = Visibility.Collapsed;
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        ExtraDataArea.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                ExtraDataArea.Visibility = Visibility.Collapsed;

                Include = Globalization.GetString("SizeProperty_Calculating_Text");
                FileSize = Globalization.GetString("SizeProperty_Calculating_Text");

                FileName = Item.Name;
                Path = Item.Path;
                FileType = Globalization.GetString("Folder_Admin_DisplayType");

                CreateTime = Item.CreationTimeRaw.ToString("F");
                ChangeTime = Item.ModifiedTimeRaw.ToString("F");

                OnPropertyChanged();

                try
                {
                    Cancellation = new CancellationTokenSource();

                    Task CountTask = CalculateFolderAndFileCount(Item.Path, Cancellation.Token).ContinueWith((task) =>
                    {
                        Include = task.Result;
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Include)));
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    Task SizeTask = CalculateFolderSize(Item.Path, Cancellation.Token).ContinueWith((task) =>
                    {
                        FileSize = task.Result;
                        PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    await Task.WhenAll(CountTask, SizeTask).ConfigureAwait(true);
                }
                catch (TaskCanceledException)
                {
                    LogTracer.Log($"{nameof(CalculateFolderAndFileCount)} and {nameof(CalculateFolderSize)} have been canceled");
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(CalculateFolderAndFileCount)} and { nameof(CalculateFolderSize)} threw an exception");
                }
                finally
                {
                    Cancellation.Dispose();
                    Cancellation = null;
                }
            }

            OnPropertyChanged();
        }

        private string ConvertTimsSpanToString(TimeSpan Span)
        {
            int Hour = 0;
            int Minute = 0;
            int Second = Convert.ToInt32(Span.TotalSeconds);

            if (Second >= 60)
            {
                Minute = Second / 60;
                Second %= 60;
                if (Minute >= 60)
                {
                    Hour = Minute / 60;
                    Minute %= 60;
                }
            }

            return string.Format("{0:D2}:{1:D2}:{2:D2}", Hour, Minute, Second);
        }

        private async Task<string> CalculateFolderSize(string FolderPath, CancellationToken CancelToken = default)
        {
            ulong TotalSize = await FileSystemStorageItemBase.GetSizeAsync(FolderPath, CancelToken).ConfigureAwait(false);

            if (CancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderSize)} was canceled");
            }
            else
            {
                return $"{TotalSize.ToFileSizeDescription()} ({TotalSize:N0} {Globalization.GetString("Device_Capacity_Unit")})";
            }
        }

        private async Task<string> CalculateFolderAndFileCount(string FolderPath, CancellationToken CancelToken = default)
        {
            (uint FolderCount, uint FileCount) = await FileSystemStorageItemBase.GetFolderAndFileNumAsync(FolderPath, CancelToken).ConfigureAwait(false);

            if (Cancellation.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderAndFileCount)} was canceled");
            }
            else
            {
                return $"{FileCount} {Globalization.GetString("FolderInfo_File_Count")} , {FolderCount} {Globalization.GetString("FolderInfo_Folder_Count")}";
            }
        }

        public void OnPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Path)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(TargetPath)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(CreateTime)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ChangeTime)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Include)));
            }
        }

        private void QueueContentDialog_CloseButtonClick(Windows.UI.Xaml.Controls.ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogButtonClickEventArgs args)
        {
            Cancellation?.Cancel();
        }
    }
}
