using ComputerVision;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.SeparateWindow.PropertyWindow
{
    public sealed partial class PropertyBase : Page
    {
        private readonly AppWindow Window;
        private readonly FileSystemStorageItemBase StorageItem;
        private readonly ObservableCollection<PropertiesGroupItem> PropertiesCollection = new ObservableCollection<PropertiesGroupItem>();
        private static readonly Dictionary<uint, string> OfflineAvailabilityMap = new Dictionary<uint, string>
        {
            { 0, "Online-only" },
            { 1, "Available" },
            { 2, "Available offline" }
        };

        private static readonly Dictionary<uint, string> OfflineAvailabilityStatusMap = new Dictionary<uint, string>
        {
            { 0, "Not Available Offline" },
            { 1, "Partially Available Offline" },
            { 2, "Complete" },
            { 3, "Complete Pinned" },
            { 4, "Excluded" },
            { 5, "Empty" }
        };

        private CancellationTokenSource Cancellation;
        private int ConfirmButtonLockResource;

        public PropertyBase(AppWindow Window, FileSystemStorageItemBase StorageItem)
        {
            InitializeComponent();
            this.Window = Window;
            this.StorageItem = StorageItem;

            ShortcutWindowsStateContent.Items.Add("Normal");
            ShortcutWindowsStateContent.Items.Add("Minimized");
            ShortcutWindowsStateContent.Items.Add("Maximized");

            ShortcutWindowsStateContent.SelectedIndex = 0;


            if (StorageItem is FileSystemStorageFolder)
            {
                GeneralSubGrid.RowDefinitions[2].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[3].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[6].Height = new GridLength(35);
                GeneralSubGrid.RowDefinitions[9].Height = new GridLength(0);

                while (PivotControl.Items.Count > 1)
                {
                    PivotControl.Items.RemoveAt(PivotControl.Items.Count - 1);
                }
            }
            else if (StorageItem is FileSystemStorageFile)
            {
                switch (StorageItem.Type.ToLower())
                {
                    case ".exe":
                    case ".bat":
                    case ".lnk":
                        {
                            GeneralSubGrid.RowDefinitions[2].Height = new GridLength(0);
                            break;
                        }
                    default:
                        {
                            GeneralSubGrid.RowDefinitions[2].Height = new GridLength(35);
                            break;
                        }
                }

                GeneralSubGrid.RowDefinitions[3].Height = new GridLength(10);
                GeneralSubGrid.RowDefinitions[6].Height = new GridLength(0);
                GeneralSubGrid.RowDefinitions[9].Height = new GridLength(35);

                if (StorageItem is IUnsupportedStorageItem)
                {
                    PivotControl.Items.Remove(PivotControl.Items.OfType<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == "Tools"));
                }

                if (StorageItem is not LinkStorageFile)
                {
                    PivotControl.Items.Remove(PivotControl.Items.OfType<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == "Shortcut"));
                }
            }

            this.Window.Closed += Window_Closed;
            Loading += PropertyBase_Loading;
            Loaded += PropertyBase_Loaded;
        }

        private async Task SaveConfiguration()
        {
            List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>> AttributeDic = new List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>>(2);

            if (StorageItem is FileSystemStorageFolder)
            {
                if (ReadonlyAttribute.IsChecked != null)
                {
                    AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(ReadonlyAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.ReadOnly));
                }
            }
            else if (StorageItem is FileSystemStorageFile File)
            {
                if (ReadonlyAttribute.IsChecked.GetValueOrDefault() != File.IsReadOnly)
                {
                    AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(File.IsReadOnly ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.ReadOnly));
                }
            }

            if (HiddenAttribute.IsChecked.GetValueOrDefault() != StorageItem is IHiddenStorageItem)
            {
                AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(StorageItem is IHiddenStorageItem ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.Hidden));
            }

            Task ShowLoadingTask = Task.Delay(1500).ContinueWith((_) =>
            {
                LoadingControl.IsLoading = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.SetFileAttribute(StorageItem.Path, AttributeDic.ToArray()).ConfigureAwait(true);

                if (StorageItem is LinkStorageFile)
                {
                    await Exclusive.Controller.UpdateLinkAsync(StorageItem.Path, ShortcutTargetContent.Text,
                                                               ShortcutStartInContent.Text,
                                                               (WindowState)ShortcutWindowsStateContent.SelectedIndex,
                                                               (int)Enum.Parse<VirtualKey>(ShortcutKeyContent.Text.Replace("Ctrl + Alt + ", string.Empty)),
                                                               ShortcutCommentContent.Text,
                                                               RunAsAdmin.IsChecked.GetValueOrDefault()).ConfigureAwait(true);
                }
            }

            if (StorageItemName.Text != Path.GetFileNameWithoutExtension(StorageItem.Name))
            {
                await StorageItem.RenameAsync(StorageItemName.Text + StorageItem.Type).ConfigureAwait(false);
            }
        }

        private void Window_Closed(AppWindow sender, AppWindowClosedEventArgs args)
        {
            Window.Closed -= Window_Closed;
            Cancellation?.Cancel();
        }

        private void PropertyBase_Loaded(object sender, RoutedEventArgs e)
        {
            Window.RequestSize(new Size(420, 650));
        }

        private async void PropertyBase_Loading(FrameworkElement sender, object args)
        {
            switch (StorageItem)
            {
                case FileSystemStorageFolder:
                    {
                        await LoadDataForGeneralPage().ConfigureAwait(true);
                        break;
                    }
                case FileSystemStorageFile:
                    {
                        await LoadDataForGeneralPage().ConfigureAwait(true);
                        await LoadDataForDetailPage().ConfigureAwait(true);

                        if (StorageItem is LinkStorageFile)
                        {
                            await LoadDataForShortCutPage().ConfigureAwait(true);
                        }

                        break;
                    }
            }
        }

        private async Task LoadDataForShortCutPage()
        {
            if (StorageItem is LinkStorageFile LinkFile)
            {
                ShortcutThumbnail.Source = LinkFile.Thumbnail;
                ShortcutItemName.Text = Path.GetFileNameWithoutExtension(LinkFile.Name);
                ShortcutCommentContent.Text = LinkFile.Comment;
                ShortcutWindowsStateContent.SelectedIndex = (int)LinkFile.WindowState;

                if (LinkFile.HotKey > 0)
                {
                    if (LinkFile.HotKey >= 112 && LinkFile.HotKey <= 135)
                    {
                        ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), (VirtualKey)LinkFile.HotKey) ?? "None";
                    }
                    else
                    {
                        ShortcutKeyContent.Text = "Ctrl + Alt + " + Enum.GetName(typeof(VirtualKey), (VirtualKey)(LinkFile.HotKey - 393216)) ?? "None";
                    }
                }
                else
                {
                    ShortcutKeyContent.Text = "None";
                }

                if (LinkFile.LinkType == ShellLinkType.Normal)
                {
                    FileSystemStorageItemBase TargetItem = await FileSystemStorageItemBase.OpenAsync(LinkFile.LinkTargetPath).ConfigureAwait(true);

                    switch (await TargetItem.GetStorageItemAsync().ConfigureAwait(true))
                    {
                        case StorageFile File:
                            {
                                ShortcutTargetTypeContent.Text = File.DisplayType;
                                break;
                            }
                        case StorageFolder Folder:
                            {
                                ShortcutTargetTypeContent.Text = Folder.DisplayType;
                                break;
                            }
                        default:
                            {
                                ShortcutTargetTypeContent.Text = TargetItem.DisplayType;
                                break;
                            }
                    }

                    ShortcutTargetLocationContent.Text = Path.GetDirectoryName(TargetItem.Path).Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    ShortcutTargetContent.Text = TargetItem.Path;
                    ShortcutStartInContent.Text = LinkFile.WorkDirectory;
                    RunAsAdmin.IsChecked = LinkFile.NeedRunAsAdmin;
                }
                else
                {
                    ShortcutTargetTypeContent.Text = LinkFile.LinkTargetPath;
                    ShortcutTargetLocationContent.Text = "Application";
                    ShortcutTargetContent.Text = LinkFile.LinkTargetPath;
                    ShortcutTargetContent.IsEnabled = false;
                    ShortcutStartInContent.IsEnabled = false;
                    OpenLocation.IsEnabled = false;
                    RunAsAdmin.IsEnabled = false;
                }
            }
        }

        private async Task LoadDataForDetailPage()
        {
            if (await StorageItem.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
            {
                Dictionary<string, object> BasicPropertiesDictionary = new Dictionary<string, object>
                {
                    { "Name", StorageItem.Name },
                    { "Item type", StorageItem.DisplayType },
                    { "Folder path", Path.GetDirectoryName(StorageItem.Path) },
                    { "Size", StorageItem.Size },
                    { "Date created", StorageItem.CreationTime },
                    { "Date modified", StorageItem.ModifiedTime }
                };

                IDictionary<string, object> BasicResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.OfflineAvailability", "System.FileOfflineAvailabilityStatus", "System.FileOwner", "System.ComputerName" });

                if (BasicResult.TryGetValue("System.OfflineAvailability", out object Availability))
                {
                    BasicPropertiesDictionary.Add("Availability", OfflineAvailabilityMap[Convert.ToUInt32(Availability)]);
                }
                else
                {
                    BasicPropertiesDictionary.Add("Availability", string.Empty);
                }

                if (BasicResult.TryGetValue("System.FileOfflineAvailabilityStatus", out object AvailabilityStatus))
                {
                    BasicPropertiesDictionary.Add("Offline status", OfflineAvailabilityStatusMap[Convert.ToUInt32(AvailabilityStatus)]);
                }
                else
                {
                    BasicPropertiesDictionary.Add("Offline status", string.Empty);
                }

                if (BasicResult.TryGetValue("System.FileOwner", out object Owner))
                {
                    BasicPropertiesDictionary.Add("Owner", Convert.ToString(Owner));
                }
                else
                {
                    BasicPropertiesDictionary.Add("Owner", string.Empty);
                }

                if (BasicResult.TryGetValue("System.ComputerName", out object ComputerName))
                {
                    BasicPropertiesDictionary.Add("Computer name", Convert.ToString(ComputerName));
                }
                else
                {
                    BasicPropertiesDictionary.Add("Computer name", string.Empty);
                }

                PropertiesCollection.Add(new PropertiesGroupItem("Basic", BasicPropertiesDictionary.ToArray()));

                string ContentType = File.ContentType;

                if (string.IsNullOrEmpty(ContentType))
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        ContentType = await Exclusive.Controller.GetMIMEContentType(File.Path).ConfigureAwait(true);
                    }
                }

                if (ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoProperties VideoProperties = await File.Properties.GetVideoPropertiesAsync();

                    Dictionary<string, object> VideoPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Duration", VideoProperties.Duration.ConvertTimsSpanToString() },
                        { "Frame width", VideoProperties.Width.ToString() },
                        { "Frame height", VideoProperties.Height.ToString() },
                        { "Bitrate", VideoProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(VideoProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(VideoProperties.Bitrate / 1048576f, 2):N2} Mbps" }
                    };

                    IDictionary<string, object> VideoResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Video.FrameRate" });

                    if (VideoResult.TryGetValue("System.Video.FrameRate", out object FrameRate))
                    {
                        VideoPropertiesDictionary.Add("Frame rate", $"{Convert.ToUInt32(FrameRate) / 1000:N2} frames/second");
                    }
                    else
                    {
                        VideoPropertiesDictionary.Add("Frame rate", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Video", VideoPropertiesDictionary.ToArray()));

                    MusicProperties AudioProperties = await File.Properties.GetMusicPropertiesAsync();

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Bitrate", AudioProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(AudioProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(AudioProperties.Bitrate / 1048576f, 2):N2} Mbps" }
                    };

                    IDictionary<string, object> AudioResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Audio.SampleRate", "System.Audio.ChannelCount" });

                    if (AudioResult.TryGetValue("System.Audio.ChannelCount", out object ChannelCount))
                    {
                        AudioPropertiesDictionary.Add("Channels", Convert.ToString(ChannelCount));
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add("Channels", string.Empty);
                    }

                    if (AudioResult.TryGetValue("System.Audio.SampleRate", out object SampleRate))
                    {
                        AudioPropertiesDictionary.Add("Sample rate", $"{Convert.ToUInt32(SampleRate) / 1000:N3} kHz");
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add("Sample rate", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Audio", AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Title", VideoProperties.Title },
                        { "Subtitle", VideoProperties.Subtitle },
                        { "Rating", VideoProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add("Comment", Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add("Comment", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Description", DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Year", VideoProperties.Year == 0 ? string.Empty : Convert.ToString(VideoProperties.Year) },
                        { "Directors", string.Join(", ", VideoProperties.Directors) },
                        { "Producers", string.Join(", ", VideoProperties.Producers) },
                        { "Publisher", VideoProperties.Publisher },
                        { "Keywords", string.Join(", ", VideoProperties.Keywords) }
                    };

                    IDictionary<string, object> ExtraResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Copyright" });

                    if (ExtraResult.TryGetValue("System.Copyright", out object Copyright))
                    {
                        ExtraPropertiesDictionary.Add("Copyright", Convert.ToString(Copyright));
                    }
                    else
                    {
                        ExtraPropertiesDictionary.Add("Copyright", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Extra", ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
                {
                    MusicProperties AudioProperties = await File.Properties.GetMusicPropertiesAsync();

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Bitrate", AudioProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(AudioProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(AudioProperties.Bitrate / 1048576f, 2):N2} Mbps" }
                    };

                    IDictionary<string, object> AudioResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Audio.SampleRate", "System.Audio.ChannelCount" });

                    if (AudioResult.TryGetValue("System.Audio.ChannelCount", out object ChannelCount))
                    {
                        AudioPropertiesDictionary.Add("Channels", Convert.ToString(ChannelCount));
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add("Channels", string.Empty);
                    }

                    if (AudioResult.TryGetValue("System.Audio.SampleRate", out object SampleRate))
                    {
                        AudioPropertiesDictionary.Add("Sample rate", $"{Convert.ToUInt32(SampleRate) / 1000:N3} kHz");
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add("Sample rate", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Audio", AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Title", AudioProperties.Title },
                        { "Subtitle", AudioProperties.Subtitle },
                        { "Rating", AudioProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add("Comment", Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add("Comment", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Description", DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Year", AudioProperties.Year == 0 ? string.Empty : Convert.ToString(AudioProperties.Year) },
                        { "Genre", string.Join(", ", AudioProperties.Genre) },
                        { "Artist", AudioProperties.Artist },
                        { "AlbumArtist", AudioProperties.AlbumArtist },
                        { "Producers", string.Join(", ", AudioProperties.Producers) },
                        { "Publisher", AudioProperties.Publisher },
                        { "Conductors", string.Join(", ", AudioProperties.Conductors) },
                        { "Composers", string.Join(", ", AudioProperties.Composers) },
                        { "Track number", AudioProperties.TrackNumber > 0 ? Convert.ToString(AudioProperties.TrackNumber) : string.Empty }
                    };

                    IDictionary<string, object> ExtraResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Copyright" });

                    if (ExtraResult.TryGetValue("System.Copyright", out object Copyright))
                    {
                        ExtraPropertiesDictionary.Add("Copyright", Convert.ToString(Copyright));
                    }
                    else
                    {
                        ExtraPropertiesDictionary.Add("Copyright", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Extra", ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    ImageProperties ImageProperties = await File.Properties.GetImagePropertiesAsync();

                    Dictionary<string, object> ImagePropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Dimensions", $"{ImageProperties.Width} x {ImageProperties.Height}" },
                        { "Width", Convert.ToString(ImageProperties.Width) },
                        { "Height", Convert.ToString(ImageProperties.Height) }
                    };

                    IDictionary<string, object> ImageResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Image.BitDepth", "System.Image.ColorSpace" });

                    if (ImageResult.TryGetValue("System.Image.BitDepth", out object BitDepth))
                    {
                        ImagePropertiesDictionary.Add("Bit depth", Convert.ToString(BitDepth));
                    }
                    else
                    {
                        ImagePropertiesDictionary.Add("Bit depth", string.Empty);
                    }

                    if (ImageResult.TryGetValue("System.Image.ColorSpace", out object ColorSpace))
                    {
                        ushort ColorSpaceEnum = Convert.ToUInt16(ColorSpace);

                        if (ColorSpaceEnum == 1)
                        {
                            ImagePropertiesDictionary.Add("Color space", "SRGB");
                        }
                        else if (ColorSpaceEnum == ushort.MaxValue)
                        {
                            ImagePropertiesDictionary.Add("Color space", "Uncalibrated");
                        }
                        else
                        {
                            ImagePropertiesDictionary.Add("Color space", string.Empty);
                        }
                    }
                    else
                    {
                        ImagePropertiesDictionary.Add("Color space", string.Empty);
                    }

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Title", ImageProperties.Title },
                        { "Date taken", ImageProperties.DateTaken.ToString("G") },
                        { "Rating", ImageProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add("Comment", Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add("Comment", string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem("Description", DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>
                    {
                        { "Camera model", ImageProperties.CameraModel },
                        { "Camera manufacturer", ImageProperties.CameraManufacturer },
                        { "Keywords", string.Join(", ", ImageProperties.Keywords) },
                        { "Latitude", Convert.ToString(ImageProperties.Latitude) },
                        { "Longitude", Convert.ToString(ImageProperties.Longitude) },
                        { "People names", string.Join(", ", ImageProperties.PeopleNames) }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem("Extra", ExtraPropertiesDictionary.ToArray()));
                }
            }
            else
            {
                Dictionary<string, object> BasicPropertiesDictionary = new Dictionary<string, object>
                {
                    { "Name", StorageItem.Name },
                    { "Item type", StorageItem.DisplayType },
                    { "Folder path", Path.GetDirectoryName(StorageItem.Path) },
                    { "Size", StorageItem.Size },
                    { "Date created", StorageItem.CreationTime },
                    { "Date modified", StorageItem.ModifiedTime }
                };

                PropertiesCollection.Add(new PropertiesGroupItem("Basic", BasicPropertiesDictionary.ToArray()));
            }
        }

        private async Task LoadDataForGeneralPage()
        {
            Thumbnail.Source = StorageItem.Thumbnail;
            StorageItemName.Text = Path.GetFileNameWithoutExtension(StorageItem.Name);
            TypeContent.Text = StorageItem.DisplayType;
            LocationContent.Text = StorageItem.Path;
            SizeContent.Text = $"{StorageItem.Size} ({StorageItem.SizeRaw:N0} {Globalization.GetString("Device_Capacity_Unit")})";
            CreatedContent.Text = StorageItem.CreationTimeRaw.ToString("F");
            ModifiedContent.Text = StorageItem.ModifiedTimeRaw.ToString("F");
            HiddenAttribute.IsChecked = StorageItem is IHiddenStorageItem;

            if (StorageItem is FileSystemStorageFolder Folder)
            {
                SizeContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                ContainsContent.Text = Globalization.GetString("SizeProperty_Calculating_Text");
                ReadonlyAttribute.IsThreeState = true;
                ReadonlyAttribute.IsChecked = null;

                try
                {
                    Cancellation = new CancellationTokenSource();

                    Task CountTask = CalculateFolderAndFileCount(Folder, Cancellation.Token).ContinueWith((task) =>
                    {
                        ContainsContent.Text = task.Result;
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    Task SizeTask = CalculateFolderSize(Folder, Cancellation.Token).ContinueWith((task) =>
                    {
                        SizeContent.Text = task.Result;
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
            else if (StorageItem is FileSystemStorageFile File)
            {
                ReadonlyAttribute.IsChecked = File.IsReadOnly;

                string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(StorageItem.Type).ConfigureAwait(true);

                if (string.IsNullOrEmpty(AdminExecutablePath))
                {
                    switch (StorageItem.Type.ToLower())
                    {
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                        case ".mkv":
                        case ".mp4":
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".wmv":
                        case ".m4a":
                        case ".mov":
                        case ".txt":
                        case ".pdf":
                            {
                                OpenWithContent.Text = Package.Current.DisplayName;

                                RandomAccessStreamReference Reference = Package.Current.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                                using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                                {
                                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                                    using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                    using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                    using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                                    {
                                        BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                        Encoder.SetSoftwareBitmap(ResizeBitmap);
                                        await Encoder.FlushAsync();

                                        BitmapImage Image = new BitmapImage();
                                        OpenWithImage.Source = Image;
                                        await Image.SetSourceAsync(Stream);
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                OpenWithContent.Text = "选择一个应用";
                                OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                                break;
                            }
                    }
                }
                else if (Path.IsPathRooted(AdminExecutablePath))
                {
                    try
                    {
                        StorageFile OpenProgramFile = await StorageFile.GetFileFromPathAsync(AdminExecutablePath);
                        OpenWithImage.Source = await OpenProgramFile.GetThumbnailBitmapAsync().ConfigureAwait(true);

                        IDictionary<string, object> PropertiesDictionary = await OpenProgramFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                        if (PropertiesDictionary.TryGetValue("System.FileDescription", out object DescriptionRaw))
                        {
                            OpenWithContent.Text = Convert.ToString(DescriptionRaw);
                        }
                        else
                        {
                            OpenWithContent.Text = OpenProgramFile.DisplayName;
                        }
                    }
                    catch
                    {
                        OpenWithContent.Text = "选择一个应用";
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                    }
                }
                else
                {
                    if (AdminExecutablePath == Package.Current.Id.FamilyName)
                    {
                        OpenWithContent.Text = Package.Current.DisplayName;

                        RandomAccessStreamReference Reference = Package.Current.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                        using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                            using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                            using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                            {
                                BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                Encoder.SetSoftwareBitmap(ResizeBitmap);
                                await Encoder.FlushAsync();

                                BitmapImage Image = new BitmapImage();
                                OpenWithImage.Source = Image;
                                await Image.SetSourceAsync(Stream);
                            }
                        }
                    }
                    else
                    {
                        if ((await Launcher.FindFileHandlersAsync(StorageItem.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                        {
                            OpenWithContent.Text = Info.Package.DisplayName;

                            RandomAccessStreamReference Reference = Info.Package.GetLogoAsRandomAccessStreamReference(new Size(50, 50));

                            using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(LogoStream);

                                using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                                using (InMemoryRandomAccessStream Stream = new InMemoryRandomAccessStream())
                                {
                                    BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, Stream);

                                    Encoder.SetSoftwareBitmap(ResizeBitmap);
                                    await Encoder.FlushAsync();

                                    BitmapImage Image = new BitmapImage();
                                    OpenWithImage.Source = Image;
                                    await Image.SetSourceAsync(Stream);
                                }
                            }
                        }
                        else
                        {
                            OpenWithContent.Text = "选择一个应用";
                            OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                        }
                    }
                }
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref ConfirmButtonLockResource, 1) == 0)
            {
                try
                {
                    await SaveConfiguration().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not save configuration");
                }
                finally
                {
                    Interlocked.Exchange(ref ConfirmButtonLockResource, 0);
                    await Window.CloseAsync();
                }
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            await Window.CloseAsync();
        }

        private async Task<string> CalculateFolderSize(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            ulong TotalSize = await Folder.GetFolderSizeAsync(CancelToken).ConfigureAwait(false);

            if (CancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderSize)} was canceled");
            }
            else
            {
                return $"{TotalSize.ToFileSizeDescription()} ({TotalSize:N0} {Globalization.GetString("Device_Capacity_Unit")})";
            }
        }

        private async Task<string> CalculateFolderAndFileCount(FileSystemStorageFolder Folder, CancellationToken CancelToken = default)
        {
            (uint FolderCount, uint FileCount) = await Folder.GetFolderAndFileNumAsync(CancelToken).ConfigureAwait(false);

            if (CancelToken.IsCancellationRequested)
            {
                throw new TaskCanceledException($"{nameof(CalculateFolderAndFileCount)} was canceled");
            }
            else
            {
                return $"{FileCount} {Globalization.GetString("FolderInfo_File_Count")} , {FolderCount} {Globalization.GetString("FolderInfo_Folder_Count")}";
            }
        }

        private void ShortcutKeyContent_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back)
            {
                ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
            }
            else if (e.Key != VirtualKey.Shift && e.Key != VirtualKey.Control && e.Key != VirtualKey.CapitalLock && e.Key != VirtualKey.Menu)
            {
                string KeyName = Enum.GetName(typeof(VirtualKey), e.Key);

                if (string.IsNullOrEmpty(KeyName))
                {
                    ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                }
                else
                {
                    if (e.Key >= VirtualKey.F1 && e.Key <= VirtualKey.F24)
                    {
                        ShortcutKeyContent.Text = KeyName;
                    }
                    else
                    {
                        ShortcutKeyContent.Text = $"Ctrl + Alt + {KeyName}";
                    }
                }
            }
        }

        private async void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is LinkStorageFile Link)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(new string[] { Path.GetDirectoryName(Link.LinkTargetPath) }).ConfigureAwait(true);

                if (TabViewContainer.ThisPage.TabViewControl.TabItems.OfType<TabViewItem>().LastOrDefault()?.Tag is FileControl Control)
                {
                    while (Control.CurrentPresenter == null)
                    {
                        await Task.Delay(500).ConfigureAwait(true);
                    }

                    if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem.Path == Link.LinkTargetPath) is FileSystemStorageItemBase Target)
                    {
                        Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                        Control.CurrentPresenter.SelectedItem = Target;
                    }
                }
            }
        }
    }
}
