using System;
using System.IO;
using System.Threading.Tasks;
using TagLib;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class MediaPlayer : Page
    {
        private StorageFile MediaFile;
        public MediaPlayer()
        {
            InitializeComponent();
            Loaded += MediaPlayer_Loaded;
        }

        private async void MediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            if (MediaFile.FileType == ".mp3" || MediaFile.FileType == ".flac" || MediaFile.FileType == ".wma" || MediaFile.FileType == ".m4a" || MediaFile.FileType == ".alac")
            {
                MusicCover.Visibility = Visibility.Visible;

                var Artist = GetMusicCoverAsync();

                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(MediaFile));
                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                //若为音乐此处必须设定为Music
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = MediaFile.DisplayName;

                try
                {
                    Props.MusicProperties.AlbumArtist = await Artist;
                }
                catch (Exception)
                {
                    Cover.Visibility = Visibility.Collapsed;
                }
                Item.ApplyDisplayProperties(Props);

                Display.Text = Globalization.Language == LanguageEnum.Chinese
                    ? "请欣赏：" + MediaFile.DisplayName
                    : "Please Enjoy: " + MediaFile.DisplayName;
                MVControl.Source = Item;
            }
            else
            {
                MusicCover.Visibility = Visibility.Collapsed;

                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromStorageFile(MediaFile));
                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                //若为视频此处必须设定为Video
                Props.Type = Windows.Media.MediaPlaybackType.Video;
                Props.VideoProperties.Title = MediaFile.DisplayName;
                Item.ApplyDisplayProperties(Props);

                MVControl.Source = Item;
            }

        }

        /// <summary>
        /// 异步获取音乐封面
        /// </summary>
        /// <returns>艺术家名称</returns>
        private async Task<string> GetMusicCoverAsync()
        {
            using (var fileStream = await MediaFile.OpenStreamForReadAsync())
            {
                using (var TagFile = TagLib.File.Create(new StreamFileAbstraction(MediaFile.Name, fileStream, fileStream)))
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
                                Cover.Source = bitmap;
                                await bitmap.SetSourceAsync(ImageStream.AsRandomAccessStream());
                            }
                            Cover.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            Cover.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        Cover.Visibility = Visibility.Collapsed;
                    }
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
                        return Globalization.Language == LanguageEnum.Chinese
                            ? "未知"
                            : "Unknown";
                    }
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            MVControl.MediaPlayer.Pause();
            MediaFile = null;
            MVControl.Source = null;
            Cover.Source = null;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            MediaFile = e.Parameter as StorageFile;
        }

        private void MVControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (MusicCover.Visibility == Visibility.Visible)
            {
                return;
            }
            else
            {
                MVControl.IsFullWindow = !MVControl.IsFullWindow;
            }
        }
    }
}
