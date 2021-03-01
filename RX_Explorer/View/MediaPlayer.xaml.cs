using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TagLib;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class MediaPlayer : Page
    {
        private FileSystemStorageItemBase MediaFile;
        private MediaSource Source;

        private readonly Dictionary<string, string> MIMEDictionary = new Dictionary<string, string>
        {
            {".mp3","audio/mpeg" },
            {".flac", "audio/x-flac" },
            {".wma", "audio/x-ms-wma" },
            {".m4a", "audio/mp4a-latm" },
            {".mkv", "video/x-matroska" },
            {".mp4", "video/mp4" },
            {".wmv", "video/x-ms-wmv" },
            {".mov", "video/quicktime" }
        };

        public MediaPlayer()
        {
            InitializeComponent();
        }

        private async Task Initialize()
        {
            using (IRandomAccessStream Stream = await MediaFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
            {
                Source = MediaSource.CreateFromStream(Stream, MIMEDictionary[MediaFile.Type.ToLower()]);
                MediaPlaybackItem Item = new MediaPlaybackItem(Source);

                switch (MediaFile.Type.ToLower())
                {
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".m4a":
                        {
                            MusicCover.Visibility = Visibility.Visible;

                            MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                            Props.Type = Windows.Media.MediaPlaybackType.Music;
                            Props.MusicProperties.Title = MediaFile.DisplayName;
                            Props.MusicProperties.AlbumArtist = await GetArtistAsync().ConfigureAwait(true);

                            Item.ApplyDisplayProperties(Props);

                            if (await GetMusicCoverAsync().ConfigureAwait(true) is BitmapImage Thumbnail)
                            {
                                Cover.Source = Thumbnail;
                                Cover.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Cover.Visibility = Visibility.Collapsed;
                            }

                            Display.Text = $"{Globalization.GetString("Media_Tip_Text")} {MediaFile.DisplayName}";
                            MVControl.Source = Item;
                            break;
                        }
                    default:
                        {
                            MusicCover.Visibility = Visibility.Collapsed;

                            MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                            Props.Type = Windows.Media.MediaPlaybackType.Video;
                            Props.VideoProperties.Title = MediaFile.DisplayName;
                            Item.ApplyDisplayProperties(Props);

                            MVControl.Source = Item;
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// 异步获取音乐封面
        /// </summary>
        /// <returns>艺术家名称</returns>
        private async Task<BitmapImage> GetMusicCoverAsync()
        {
            try
            {
                using (FileStream FileStream = await MediaFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                using (var TagFile = TagLib.File.Create(new StreamFileAbstraction(MediaFile.Name, FileStream, FileStream)))
                {
                    if (TagFile.Tag.Pictures != null && TagFile.Tag.Pictures.Length != 0)
                    {
                        var ImageData = TagFile.Tag.Pictures[0].Data.Data;

                        if (ImageData != null && ImageData.Length != 0)
                        {
                            using (MemoryStream ImageStream = new MemoryStream(ImageData))
                            {
                                BitmapImage bitmap = new BitmapImage
                                {
                                    DecodePixelHeight = 250,
                                    DecodePixelWidth = 250
                                };
                                await bitmap.SetSourceAsync(ImageStream.AsRandomAccessStream());

                                return bitmap;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> GetArtistAsync()
        {
            try
            {
                using (FileStream FileStream = await MediaFile.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(false))
                using (var TagFile = TagLib.File.Create(new StreamFileAbstraction(MediaFile.Name, FileStream, FileStream)))
                {
                    if (TagFile.Tag.AlbumArtists != null && TagFile.Tag.AlbumArtists.Length != 0)
                    {
                        string Artist = "";

                        if (TagFile.Tag.AlbumArtists.Length == 1)
                        {
                            return TagFile.Tag.AlbumArtists[0];
                        }
                        else
                        {
                            Artist = TagFile.Tag.AlbumArtists[0];
                        }

                        foreach (var item in TagFile.Tag.AlbumArtists)
                        {
                            Artist = Artist + "/" + item;
                        }

                        return Artist;
                    }
                    else
                    {
                        return Globalization.GetString("UnknownText");
                    }
                }
            }
            catch
            {
                return Globalization.GetString("UnknownText");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= MediaPlayer_KeyDown; ;
            MVControl.MediaPlayer.Pause();
            MediaFile = null;
            MVControl.Source = null;
            Cover.Source = null;
            Source.Dispose();
            Source = null;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            MediaFile = e.Parameter as FileSystemStorageItemBase;
            CoreWindow.GetForCurrentThread().KeyDown += MediaPlayer_KeyDown; ;
            await Initialize().ConfigureAwait(false);
        }

        private void MediaPlayer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.Escape && MVControl.IsFullWindow)
            {
                MVControl.IsFullWindow = false;
            }
        }

        private void MVControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (MusicCover.Visibility != Visibility.Visible)
            {
                MVControl.IsFullWindow = !MVControl.IsFullWindow;
            }
        }

        private void MVControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (MVControl.MediaPlayer.PlaybackSession.CanPause && (MVControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing || MVControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Buffering))
            {
                MVControl.MediaPlayer.Pause();
            }
            else if(MVControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
            {
                MVControl.MediaPlayer.Play();
            }
        }
    }
}
