using RX_Explorer.Class;
using RX_Explorer.Interface;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TagLib;
using Windows.ApplicationModel.Core;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class MediaPlayer : Page
    {
        private CancellationTokenSource Cancellation;

        private readonly IReadOnlyDictionary<string, string> MIMEMapping = new Dictionary<string, string>
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
            Loaded += MediaPlayer_Loaded;
            Unloaded += MediaPlayer_Unloaded;
        }

        private void MediaPlayer_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= MediaPlayer_KeyDown;
        }

        private void MediaPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += MediaPlayer_KeyDown;
        }

        private async Task InitializeAsync(FileSystemStorageFile MediaFile, CancellationToken CancelToken = default)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                MVControl.Source = await GenerateMediaPlaybackList(MediaFile, CancelToken);
            }
            catch (OperationCanceledException)
            {
                //No need to handle this exception
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load media because an exception was threw");

                await new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldNotLoadMedia_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            finally
            {
                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        private async Task<MediaPlaybackList> GenerateMediaPlaybackList(FileSystemStorageFile TargetMediaFile, CancellationToken CancelToken = default)
        {
            MediaPlaybackList PlaybackList = new MediaPlaybackList
            {
                MaxPrefetchTime = TimeSpan.FromSeconds(10),
                MaxPlayedItemsToKeepOpen = 0
            };

            try
            {
                if (TargetMediaFile.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream Stream = await TargetMediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                    {
                        SLEHeader Header = SLEHeader.GetHeader(Stream);

                        if (Header.Version >= SLEVersion.Version_1_5_0)
                        {
                            switch (Path.GetExtension(Header.FileName).ToLower())
                            {
                                case ".mp3":
                                case ".flac":
                                case ".wma":
                                case ".m4a":
                                    {
                                        MediaBinder Binder = new MediaBinder
                                        {
                                            Token = TargetMediaFile.Path
                                        };
                                        Binder.Binding += Binder_Binding;

                                        MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                        MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                        Props.Type = MediaPlaybackType.Music;
                                        Props.MusicProperties.Title = Header.FileName;

                                        try
                                        {
                                            using (SLEInputStream MediaStream = new SLEInputStream(Stream, SecureArea.AESKey))
                                            {
                                                byte[] CoverData = GetMusicCoverFromStream(Header.FileName, MediaStream);

                                                if (CoverData.Length > 0)
                                                {
                                                    Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await Helper.CreateRandomAccessStreamAsync(CoverData));
                                                }
                                                else
                                                {
                                                    Props.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/LockFile.png"));
                                                }

                                                Props.MusicProperties.AlbumArtist = GetArtistFromStream(Header.FileName, MediaStream);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            //No need to handle this exception
                                        }

                                        Item.ApplyDisplayProperties(Props);

                                        PlaybackList.Items.Add(Item);
                                        PlaybackList.StartingItem = Item;
                                        break;
                                    }
                                case ".mkv":
                                case ".wmv":
                                case ".mp4":
                                case ".mov":
                                    {
                                        MediaBinder Binder = new MediaBinder
                                        {
                                            Token = TargetMediaFile.Path
                                        };
                                        Binder.Binding += Binder_Binding;

                                        MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                        MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                        Props.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/LockFile.png"));
                                        Props.Type = MediaPlaybackType.Video;
                                        Props.VideoProperties.Title = Header.FileName;

                                        Item.ApplyDisplayProperties(Props);

                                        PlaybackList.Items.Add(Item);
                                        PlaybackList.StartingItem = Item;
                                        break;
                                    }
                                default:
                                    {
                                        throw new NotSupportedException();
                                    }
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                else
                {
                    if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(TargetMediaFile.Path)) is FileSystemStorageFolder BaseFolder)
                    {
                        switch (TargetMediaFile.Type.ToLower())
                        {
                            case ".mp3":
                            case ".flac":
                            case ".wma":
                            case ".m4a":
                                {
                                    IReadOnlyList<FileSystemStorageFile> FileList = await BaseFolder.GetChildItemsAsync(CancelToken: CancelToken, Filter: BasicFilters.File, AdvanceFilter: (ItemPath) =>
                                    {
                                        switch (Path.GetExtension(ItemPath).ToLower())
                                        {
                                            case ".mp3":
                                            case ".flac":
                                            case ".wma":
                                            case ".m4a":
                                                {
                                                    return true;
                                                }
                                        }

                                        return false;
                                    }).Cast<FileSystemStorageFile>().ToListAsync();

                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(BaseFolder.Path);

                                    foreach (FileSystemStorageFile MediaFile in await SortCollectionGenerator.GetSortedCollectionAsync(FileList, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault()))
                                    {
                                        MediaBinder Binder = new MediaBinder
                                        {
                                            Token = MediaFile.Path
                                        };
                                        Binder.Binding += Binder_Binding;

                                        MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                        MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                        Props.Type = MediaPlaybackType.Music;
                                        Props.MusicProperties.Title = MediaFile.Name;

                                        try
                                        {
                                            using (Stream MediaStream = await MediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                                            {
                                                byte[] CoverData = GetMusicCoverFromStream(MediaFile.Name, MediaStream);

                                                if (CoverData.Length > 0)
                                                {
                                                    Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await Helper.CreateRandomAccessStreamAsync(CoverData));
                                                }
                                                else
                                                {
                                                    Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await MediaFile.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                                                }

                                                Props.MusicProperties.AlbumArtist = GetArtistFromStream(MediaFile.Name, MediaStream);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            //No need to handle this exception
                                        }

                                        Item.ApplyDisplayProperties(Props);

                                        PlaybackList.Items.Add(Item);

                                        if (TargetMediaFile.Path.Equals(MediaFile.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            PlaybackList.StartingItem = Item;
                                        }
                                    }

                                    break;
                                }
                            case ".mkv":
                            case ".wmv":
                            case ".mp4":
                            case ".mov":
                                {
                                    IReadOnlyList<FileSystemStorageFile> FileList = await BaseFolder.GetChildItemsAsync(CancelToken: CancelToken, Filter: BasicFilters.File, AdvanceFilter: (ItemPath) =>
                                    {
                                        switch (Path.GetExtension(ItemPath).ToLower())
                                        {
                                            case ".mkv":
                                            case ".wmv":
                                            case ".mp4":
                                            case ".mov":
                                                {
                                                    return true;
                                                }
                                        }

                                        return false;
                                    }).Cast<FileSystemStorageFile>().ToListAsync();

                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(BaseFolder.Path);

                                    foreach (FileSystemStorageFile MediaFile in await SortCollectionGenerator.GetSortedCollectionAsync(FileList, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault()))
                                    {
                                        MediaBinder Binder = new MediaBinder
                                        {
                                            Token = MediaFile.Path
                                        };
                                        Binder.Binding += Binder_Binding;

                                        MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                        MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                        Props.Type = MediaPlaybackType.Video;
                                        Props.VideoProperties.Title = MediaFile.Name;

                                        try
                                        {
                                            Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await MediaFile.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                                        }
                                        catch (Exception)
                                        {
                                            //No need to handle this exception
                                        }

                                        Item.ApplyDisplayProperties(Props);

                                        PlaybackList.Items.Add(Item);

                                        if (TargetMediaFile.Path.Equals(MediaFile.Path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            PlaybackList.StartingItem = Item;
                                        }
                                    }

                                    break;
                                }
                        }
                    }
                    else
                    {
                        switch (TargetMediaFile.Type.ToLower())
                        {
                            case ".mp3":
                            case ".flac":
                            case ".wma":
                            case ".m4a":
                                {
                                    MediaBinder Binder = new MediaBinder
                                    {
                                        Token = TargetMediaFile.Path
                                    };
                                    Binder.Binding += Binder_Binding;

                                    MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                    MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                    Props.Type = MediaPlaybackType.Music;
                                    Props.MusicProperties.Title = TargetMediaFile.Name;

                                    try
                                    {
                                        using (Stream MediaStream = await TargetMediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                                        {
                                            byte[] CoverData = GetMusicCoverFromStream(TargetMediaFile.Name, MediaStream);

                                            if (CoverData.Length > 0)
                                            {
                                                Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await Helper.CreateRandomAccessStreamAsync(CoverData));
                                            }
                                            else
                                            {
                                                Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await TargetMediaFile.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                                            }

                                            Props.MusicProperties.AlbumArtist = GetArtistFromStream(TargetMediaFile.Name, MediaStream);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }

                                    Item.ApplyDisplayProperties(Props);

                                    PlaybackList.Items.Add(Item);
                                    PlaybackList.StartingItem = Item;
                                    break;
                                }
                            case ".mkv":
                            case ".wmv":
                            case ".mp4":
                            case ".mov":
                                {
                                    MediaBinder Binder = new MediaBinder
                                    {
                                        Token = TargetMediaFile.Path
                                    };
                                    Binder.Binding += Binder_Binding;

                                    MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(Binder));

                                    MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                                    Props.Type = MediaPlaybackType.Video;
                                    Props.VideoProperties.Title = TargetMediaFile.Name;

                                    try
                                    {
                                        Props.Thumbnail = RandomAccessStreamReference.CreateFromStream(await TargetMediaFile.GetThumbnailRawStreamAsync(ThumbnailMode.SingleItem));
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }

                                    Item.ApplyDisplayProperties(Props);

                                    PlaybackList.Items.Add(Item);
                                    PlaybackList.StartingItem = Item;
                                    break;
                                }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                foreach (MediaSource Source in PlaybackList.Items.Select((Item) => Item.Source))
                {
                    Source.Dispose();
                }

                throw;
            }

            return PlaybackList;
        }

        private async void Binder_Binding(MediaBinder sender, MediaBindingEventArgs args)
        {
            Deferral Deferral = args.GetDeferral();

            try
            {
                if (await FileSystemStorageItemBase.OpenAsync(args.MediaBinder.Token) is FileSystemStorageFile MediaFile)
                {
                    if (Path.GetExtension(args.MediaBinder.Token).Equals(".sle", StringComparison.OrdinalIgnoreCase))
                    {
                        Stream Stream = await MediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess);

                        SLEHeader Header = SLEHeader.GetHeader(Stream);

                        if (Header.Version >= SLEVersion.Version_1_5_0)
                        {
                            args.SetStream(new SLEInputStream(Stream, SecureArea.AESKey).AsRandomAccessStream(), MIMEMapping[Path.GetExtension(Header.FileName).ToLower()]);

                            try
                            {
                                switch (Path.GetExtension(Header.FileName).ToLower())
                                {
                                    case ".mp3":
                                    case ".flac":
                                    case ".wma":
                                    case ".m4a":
                                        {
                                            await CoreApplication.MainView.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                                            {
                                                MusicName.Text = Header.FileName;

                                                using (Stream FileStream = await MediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                                                using (SLEInputStream MediaStream = new SLEInputStream(FileStream, SecureArea.AESKey))
                                                {
                                                    byte[] CoverData = GetMusicCoverFromStream(Header.FileName, MediaStream);

                                                    if (CoverData.Length > 0)
                                                    {
                                                        MusicCover.Source = await Helper.CreateBitmapImageAsync(CoverData);
                                                    }
                                                    else
                                                    {
                                                        MusicCover.Source = new BitmapImage(new Uri("ms-appx:///Assets/LockFile.png"));
                                                    }
                                                }
                                            });

                                            break;
                                        }
                                }
                            }
                            catch (Exception)
                            {
                                //No need to handle this exception
                            }
                        }
                    }
                    else
                    {

                        if (await MediaFile.GetStorageItemAsync() is StorageFile CoreFile)
                        {
                            args.SetStorageFile(CoreFile);
                        }
                        else
                        {
                            args.SetStream((await MediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess)).AsRandomAccessStream(), MIMEMapping[MediaFile.Type.ToLower()]);
                        }

                        try
                        {
                            switch (MediaFile.Type.ToLower())
                            {
                                case ".mp3":
                                case ".flac":
                                case ".wma":
                                case ".m4a":
                                    {
                                        await CoreApplication.MainView.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                                        {
                                            MusicName.Text = MediaFile.Name;

                                            using (Stream FileStream = await MediaFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                                            {
                                                byte[] CoverData = GetMusicCoverFromStream(MediaFile.Name, FileStream);

                                                if (CoverData.Length > 0)
                                                {
                                                    MusicCover.Source = await Helper.CreateBitmapImageAsync(CoverData);
                                                }
                                                else
                                                {
                                                    MusicCover.Source = await MediaFile.GetThumbnailAsync(ThumbnailMode.SingleItem);
                                                }
                                            }
                                        });

                                        break;
                                    }
                            }
                        }
                        catch (Exception)
                        {
                            //No need to handle this exception
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load the video or audio content");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        /// <summary>
        /// 异步获取音乐封面
        /// </summary>
        /// <returns>艺术家名称</returns>
        private byte[] GetMusicCoverFromStream(string FileName, Stream MediaFileStream)
        {
            try
            {
                if (MediaFileStream != null)
                {
                    MediaFileStream.Seek(0, SeekOrigin.Begin);

                    using (TagLib.File TagFile = TagLib.File.Create(new StreamFileAbstraction(FileName, MediaFileStream, MediaFileStream)))
                    {
                        if ((TagFile.Tag.Pictures?.Length).GetValueOrDefault() > 0)
                        {
                            foreach (IPicture Picture in TagFile.Tag.Pictures)
                            {
                                byte[] ImageData = Picture.Data.Data;

                                if ((ImageData?.Length).GetValueOrDefault() > 0)
                                {
                                    return ImageData;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get music cover from stream");
            }

            return Array.Empty<byte>();
        }

        private string GetArtistFromStream(string FileName, Stream MediaFileStream)
        {
            try
            {
                if (MediaFileStream != null)
                {
                    MediaFileStream.Seek(0, SeekOrigin.Begin);

                    using (TagLib.File TagFile = TagLib.File.Create(new StreamFileAbstraction(FileName, MediaFileStream, MediaFileStream)))
                    {
                        switch ((TagFile.Tag.AlbumArtists?.Length).GetValueOrDefault())
                        {
                            case 1:
                                {
                                    return TagFile.Tag.AlbumArtists.Single();
                                }
                            case > 1:
                                {
                                    return string.Join("/", TagFile.Tag.AlbumArtists);
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get artist from stream");
            }

            return Globalization.GetString("UnknownText");
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);

            Cancellation?.Cancel();
            Cancellation?.Dispose();
            MVControl.MediaPlayer.Pause();

            if (MVControl.Source is MediaPlaybackList PlaybackList)
            {
                foreach (MediaSource Source in PlaybackList.Items.Select((Item) => Item.Source))
                {
                    Source.Dispose();
                }
            }

            MVControl.Source = null;
            MusicCover.Source = null;
            MusicName.Text = string.Empty;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            Cancellation = new CancellationTokenSource();

            if (e.Parameter is FileSystemStorageFile MediaFile)
            {
                await InitializeAsync(MediaFile, Cancellation.Token);
            }
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
            if (MVControl.ActualHeight - e.GetPosition(MVControl).Y > 100)
            {
                if (MusicCover.Visibility != Visibility.Visible)
                {
                    MVControl.IsFullWindow = !MVControl.IsFullWindow;
                }
            }
        }

        private void MVControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse
                && e.GetCurrentPoint(null).Properties.IsLeftButtonPressed
                && (e.OriginalSource as FrameworkElement).FindParentOfType<MediaTransportControls>() is null)
            {
                e.Handled = true;

                if (MVControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                {
                    MVControl.MediaPlayer.Play();
                }
                else if (MVControl.MediaPlayer.PlaybackSession.CanPause)
                {
                    MVControl.MediaPlayer.Pause();
                }
            }
        }
    }
}
