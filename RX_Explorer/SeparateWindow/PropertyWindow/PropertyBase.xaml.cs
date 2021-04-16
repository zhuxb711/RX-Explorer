using ComputerVision;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
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
        private static readonly Dictionary<uint, string> OfflineAvailabilityMap = new Dictionary<uint, string>(3)
        {
            { 0, Globalization.GetString("OfflineAvailabilityText1") },
            { 1, Globalization.GetString("OfflineAvailabilityText2") },
            { 2, Globalization.GetString("OfflineAvailabilityText3") }
        };

        private static readonly Dictionary<uint, string> OfflineAvailabilityStatusMap = new Dictionary<uint, string>(6)
        {
            { 0, Globalization.GetString("OfflineAvailabilityStatusText1") },
            { 1, Globalization.GetString("OfflineAvailabilityStatusText2") },
            { 2, Globalization.GetString("OfflineAvailabilityStatusText3") },
            { 3, Globalization.GetString("OfflineAvailabilityStatusText4") },
            { 4, Globalization.GetString("OfflineAvailabilityStatusText5") },
            { 5, Globalization.GetString("OfflineAvailabilityStatusText6") }
        };

        private CancellationTokenSource FolderCancellation;
        private CancellationTokenSource Md5Cancellation;
        private CancellationTokenSource SHA1Cancellation;
        private CancellationTokenSource SHA256Cancellation;
        private int ConfirmButtonLockResource;

        public PropertyBase(AppWindow Window, FileSystemStorageItemBase StorageItem)
        {
            InitializeComponent();

            this.Window = Window;
            this.StorageItem = StorageItem;

            PropertiesTitleLeft.Text = StorageItem.DisplayName;
            GeneralTab.Text = Globalization.GetString("Properties_General_Tab");
            ShortcutTab.Text = Globalization.GetString("Properties_Shortcut_Tab");
            DetailsTab.Text = Globalization.GetString("Properties_Details_Tab");
            ToolsTab.Text = Globalization.GetString("Properties_Tools_Tab");

            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText1"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText2"));
            ShortcutWindowsStateContent.Items.Add(Globalization.GetString("ShortcutWindowsStateText3"));

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
                    case ".url":
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

                Unlock.IsEnabled = Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64;

                if (StorageItem is IUnsupportedStorageItem)
                {
                    PivotControl.Items.Remove(PivotControl.Items.OfType<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Tools_Tab")));
                }

                if (StorageItem is not (LinkStorageFile or UrlStorageFile))
                {
                    PivotControl.Items.Remove(PivotControl.Items.OfType<PivotItem>().FirstOrDefault((Item) => (Item.Header as TextBlock).Text == Globalization.GetString("Properties_Shortcut_Tab")));
                }
            }

            Window.Closed += Window_Closed;
            Loading += PropertyBase_Loading;
            Loaded += PropertyBase_Loaded;
        }

        private void Window_Closed(AppWindow sender, AppWindowClosedEventArgs args)
        {
            Window.Closed -= Window_Closed;

            FolderCancellation?.Cancel();
            Md5Cancellation?.Cancel();
            SHA1Cancellation?.Cancel();
            SHA256Cancellation?.Cancel();
        }

        private async Task SaveConfiguration()
        {
            List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>> AttributeDic = new List<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>>(2);

            switch (StorageItem)
            {
                case FileSystemStorageFolder:
                    {
                        if (ReadonlyAttribute.IsChecked != null)
                        {
                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(ReadonlyAttribute.IsChecked.Value ? ModifyAttributeAction.Add : ModifyAttributeAction.Remove, System.IO.FileAttributes.ReadOnly));
                        }

                        break;
                    }
                case FileSystemStorageFile File:
                    {
                        if (ReadonlyAttribute.IsChecked.GetValueOrDefault() != File.IsReadOnly)
                        {
                            AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(File.IsReadOnly ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.ReadOnly));
                        }

                        break;
                    }
            }

            if (HiddenAttribute.IsChecked.GetValueOrDefault() != (StorageItem is IHiddenStorageItem))
            {
                AttributeDic.Add(new KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>(StorageItem is IHiddenStorageItem ? ModifyAttributeAction.Remove : ModifyAttributeAction.Add, System.IO.FileAttributes.Hidden));
            }

            Task ShowLoadingTask = Task.Delay(1500).ContinueWith((_) =>
            {
                LoadingControl.IsLoading = true;
            }, TaskScheduler.FromCurrentSynchronizationContext());

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.SetFileAttribute(StorageItem.Path, AttributeDic.ToArray());

                switch (StorageItem)
                {
                    case LinkStorageFile:
                        {
                            await Exclusive.Controller.UpdateLinkAsync(StorageItem.Path, ShortcutTargetContent.Text,
                                                                       ShortcutStartInContent.Text,
                                                                       (WindowState)ShortcutWindowsStateContent.SelectedIndex,
                                                                       ShortcutKeyContent.Text == Globalization.GetString("ShortcutHotKey_None") ? (int)VirtualKey.None : (int)Enum.Parse<VirtualKey>(ShortcutKeyContent.Text.Replace("Ctrl + Alt + ", string.Empty)),
                                                                       ShortcutCommentContent.Text,
                                                                       RunAsAdmin.IsChecked.GetValueOrDefault());
                            break;
                        }
                    case UrlStorageFile:
                        {
                            await Exclusive.Controller.UpdateUrlAsync(StorageItem.Path, ShortcutUrlContent.Text);
                            break;
                        }
                }
            }

            if (StorageItemName.Text != StorageItem.DisplayName)
            {
                await StorageItem.RenameAsync(StorageItemName.Text).ConfigureAwait(false);
            }
        }

        private void PropertyBase_Loaded(object sender, RoutedEventArgs e)
        {
            Window.RequestSize(new Size(420, 600));
        }

        private async void PropertyBase_Loading(FrameworkElement sender, object args)
        {
            switch (StorageItem)
            {
                case FileSystemStorageFolder:
                    {
                        await LoadDataForGeneralPage();
                        break;
                    }
                case FileSystemStorageFile:
                    {
                        await LoadDataForGeneralPage();
                        await LoadDataForDetailPage();

                        if (StorageItem is LinkStorageFile or UrlStorageFile)
                        {
                            await LoadDataForShortCutPage();
                        }

                        break;
                    }
            }
        }

        private async Task LoadDataForShortCutPage()
        {
            switch (StorageItem)
            {
                case LinkStorageFile LinkFile:
                    {
                        UrlArea.Visibility = Visibility.Collapsed;
                        LinkArea.Visibility = Visibility.Visible;

                        ShortcutThumbnail.Source = LinkFile.Thumbnail;
                        ShortcutItemName.Text = Path.GetFileNameWithoutExtension(LinkFile.Name);
                        ShortcutCommentContent.Text = LinkFile.Comment;
                        ShortcutWindowsStateContent.SelectedIndex = (int)LinkFile.WindowState;

                        if (LinkFile.HotKey > 0)
                        {
                            if (LinkFile.HotKey >= 112 && LinkFile.HotKey <= 135)
                            {
                                ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), (VirtualKey)LinkFile.HotKey) ?? Globalization.GetString("ShortcutHotKey_None");
                            }
                            else
                            {
                                ShortcutKeyContent.Text = "Ctrl + Alt + " + Enum.GetName(typeof(VirtualKey), (VirtualKey)(LinkFile.HotKey - 393216)) ?? Globalization.GetString("ShortcutHotKey_None");
                            }
                        }
                        else
                        {
                            ShortcutKeyContent.Text = Globalization.GetString("ShortcutHotKey_None");
                        }

                        if (LinkFile.LinkType == ShellLinkType.Normal)
                        {
                            FileSystemStorageItemBase TargetItem = await FileSystemStorageItemBase.OpenAsync(LinkFile.LinkTargetPath);

                            switch (await TargetItem.GetStorageItemAsync())
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
                            ShortcutTargetLocationContent.Text = Globalization.GetString("ShortcutTargetApplicationType");
                            ShortcutTargetContent.Text = LinkFile.LinkTargetPath;
                            ShortcutTargetContent.IsEnabled = false;
                            ShortcutStartInContent.IsEnabled = false;
                            OpenLocation.IsEnabled = false;
                            RunAsAdmin.IsEnabled = false;
                        }

                        break;
                    }

                case UrlStorageFile UrlFile:
                    {
                        UrlArea.Visibility = Visibility.Visible;
                        LinkArea.Visibility = Visibility.Collapsed;

                        ShortcutThumbnail.Source = UrlFile.Thumbnail;
                        ShortcutItemName.Text = Path.GetFileNameWithoutExtension(UrlFile.Name);
                        ShortcutUrlContent.Text = UrlFile.UrlTargetPath;
                        break;
                    }
            }
        }

        private async Task LoadDataForDetailPage()
        {
            Dictionary<string, object> BasicPropertiesDictionary = new Dictionary<string, object>(10)
            {
                { Globalization.GetString("Properties_Details_Name"), StorageItem.Name },
                { Globalization.GetString("Properties_Details_ItemType"), StorageItem.DisplayType },
                { Globalization.GetString("Properties_Details_FolderPath"), Path.GetDirectoryName(StorageItem.Path) },
                { Globalization.GetString("Properties_Details_Size"), StorageItem.Size },
                { Globalization.GetString("Properties_Details_DateCreated"), StorageItem.CreationTime },
                { Globalization.GetString("Properties_Details_DateModified"), StorageItem.ModifiedTime }
            };

            if (await StorageItem.GetStorageItemAsync() is StorageFile File)
            {
                IDictionary<string, object> BasicResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.OfflineAvailability", "System.FileOfflineAvailabilityStatus", "System.FileOwner", "System.ComputerName" });

                if (BasicResult.TryGetValue("System.OfflineAvailability", out object Availability))
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Availability"), OfflineAvailabilityMap[Convert.ToUInt32(Availability)]);
                }
                else
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Availability"), string.Empty);
                }

                if (BasicResult.TryGetValue("System.FileOfflineAvailabilityStatus", out object AvailabilityStatus))
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_OfflineAvailabilityStatus"), OfflineAvailabilityStatusMap[Convert.ToUInt32(AvailabilityStatus)]);
                }
                else
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_OfflineAvailabilityStatus"), string.Empty);
                }

                if (BasicResult.TryGetValue("System.FileOwner", out object Owner))
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Owner"), Convert.ToString(Owner));
                }
                else
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Owner"), string.Empty);
                }

                if (BasicResult.TryGetValue("System.ComputerName", out object ComputerName))
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_ComputerName"), Convert.ToString(ComputerName));
                }
                else
                {
                    BasicPropertiesDictionary.Add(Globalization.GetString("Properties_Details_ComputerName"), string.Empty);
                }

                PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Basic_Label"), BasicPropertiesDictionary.ToArray()));

                string ContentType = File.ContentType;

                if (string.IsNullOrEmpty(ContentType))
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        ContentType = await Exclusive.Controller.GetMIMEContentType(File.Path);
                    }
                }

                if (ContentType.StartsWith("video", StringComparison.OrdinalIgnoreCase))
                {
                    VideoProperties VideoProperties = await File.Properties.GetVideoPropertiesAsync();

                    Dictionary<string, object> VideoPropertiesDictionary = new Dictionary<string, object>(5)
                    {
                        { Globalization.GetString("Properties_Details_Duration"), VideoProperties.Duration.ConvertTimsSpanToString() },
                        { Globalization.GetString("Properties_Details_FrameWidth"), VideoProperties.Width.ToString() },
                        { Globalization.GetString("Properties_Details_FrameHeight"), VideoProperties.Height.ToString() },
                        { Globalization.GetString("Properties_Details_Bitrate"), VideoProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(VideoProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(VideoProperties.Bitrate / 1048576f, 2):N2} Mbps" }
                    };

                    IDictionary<string, object> VideoResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Video.FrameRate" });

                    if (VideoResult.TryGetValue("System.Video.FrameRate", out object FrameRate))
                    {
                        VideoPropertiesDictionary.Add(Globalization.GetString("Properties_Details_FrameRate"), $"{Convert.ToUInt32(FrameRate) / 1000:N2} {Globalization.GetString("Properties_Details_FrameRatePerSecond")}");
                    }
                    else
                    {
                        VideoPropertiesDictionary.Add(Globalization.GetString("Properties_Details_FrameRate"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Video_Label"), VideoPropertiesDictionary.ToArray()));

                    MusicProperties AudioProperties = await File.Properties.GetMusicPropertiesAsync();

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>(3)
                    {
                        { Globalization.GetString("Properties_Details_Bitrate"), AudioProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(AudioProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(AudioProperties.Bitrate / 1048576f, 2):N2} Mbps" }
                    };

                    IDictionary<string, object> AudioResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Audio.SampleRate", "System.Audio.ChannelCount" });

                    if (AudioResult.TryGetValue("System.Audio.ChannelCount", out object ChannelCount))
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Channels"), Convert.ToString(ChannelCount));
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Channels"), string.Empty);
                    }

                    if (AudioResult.TryGetValue("System.Audio.SampleRate", out object SampleRate))
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_SampleRate"), $"{Convert.ToUInt32(SampleRate) / 1000:N3} kHz");
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_SampleRate"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Audio_Label"), AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), VideoProperties.Title },
                        { Globalization.GetString("Properties_Details_Subtitle"), VideoProperties.Subtitle },
                        { Globalization.GetString("Properties_Details_Rating"), VideoProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(6)
                    {
                        { Globalization.GetString("Properties_Details_Year"), VideoProperties.Year == 0 ? string.Empty : Convert.ToString(VideoProperties.Year) },
                        { Globalization.GetString("Properties_Details_Directors"), string.Join(", ", VideoProperties.Directors) },
                        { Globalization.GetString("Properties_Details_Producers"), string.Join(", ", VideoProperties.Producers) },
                        { Globalization.GetString("Properties_Details_Publisher"), VideoProperties.Publisher },
                        { Globalization.GetString("Properties_Details_Keywords"), string.Join(", ", VideoProperties.Keywords) }
                    };

                    IDictionary<string, object> ExtraResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Copyright" });

                    if (ExtraResult.TryGetValue("System.Copyright", out object Copyright))
                    {
                        ExtraPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Copyright"), Convert.ToString(Copyright));
                    }
                    else
                    {
                        ExtraPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Copyright"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("audio", StringComparison.OrdinalIgnoreCase))
                {
                    MusicProperties AudioProperties = await File.Properties.GetMusicPropertiesAsync();

                    Dictionary<string, object> AudioPropertiesDictionary = new Dictionary<string, object>(3)
                    {
                        { Globalization.GetString("Properties_Details_Bitrate"), AudioProperties.Bitrate / 1024f < 1024 ? $"{Math.Round(AudioProperties.Bitrate / 1024f, 2):N2} Kbps" : $"{Math.Round(AudioProperties.Bitrate / 1048576f, 2):N2} Mbps" },
                        { Globalization.GetString("Properties_Details_Duration"), AudioProperties.Duration.ConvertTimsSpanToString() }
                    };

                    IDictionary<string, object> AudioResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Audio.SampleRate", "System.Audio.ChannelCount" });

                    if (AudioResult.TryGetValue("System.Audio.ChannelCount", out object ChannelCount))
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Channels"), Convert.ToString(ChannelCount));
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Channels"), string.Empty);
                    }

                    if (AudioResult.TryGetValue("System.Audio.SampleRate", out object SampleRate))
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_SampleRate"), $"{Convert.ToUInt32(SampleRate) / 1000:N3} kHz");
                    }
                    else
                    {
                        AudioPropertiesDictionary.Add(Globalization.GetString("Properties_Details_SampleRate"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Audio_Label"), AudioPropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), AudioProperties.Title },
                        { Globalization.GetString("Properties_Details_Subtitle"), AudioProperties.Subtitle },
                        { Globalization.GetString("Properties_Details_Rating"), AudioProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(10)
                    {
                        { Globalization.GetString("Properties_Details_Year"), AudioProperties.Year == 0 ? string.Empty : Convert.ToString(AudioProperties.Year) },
                        { Globalization.GetString("Properties_Details_Genre"), string.Join(", ", AudioProperties.Genre) },
                        { Globalization.GetString("Properties_Details_Artist"), AudioProperties.Artist },
                        { Globalization.GetString("Properties_Details_AlbumArtist"), AudioProperties.AlbumArtist },
                        { Globalization.GetString("Properties_Details_Producers"), string.Join(", ", AudioProperties.Producers) },
                        { Globalization.GetString("Properties_Details_Publisher"), AudioProperties.Publisher },
                        { Globalization.GetString("Properties_Details_Conductors"), string.Join(", ", AudioProperties.Conductors) },
                        { Globalization.GetString("Properties_Details_Composers"), string.Join(", ", AudioProperties.Composers) },
                        { Globalization.GetString("Properties_Details_TrackNum"), AudioProperties.TrackNumber > 0 ? Convert.ToString(AudioProperties.TrackNumber) : string.Empty }
                    };

                    IDictionary<string, object> ExtraResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Copyright" });

                    if (ExtraResult.TryGetValue("System.Copyright", out object Copyright))
                    {
                        ExtraPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Copyright"), Convert.ToString(Copyright));
                    }
                    else
                    {
                        ExtraPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Copyright"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    ImageProperties ImageProperties = await File.Properties.GetImagePropertiesAsync();

                    Dictionary<string, object> ImagePropertiesDictionary = new Dictionary<string, object>(5)
                    {
                        { Globalization.GetString("Properties_Details_Dimensions"), $"{ImageProperties.Width} x {ImageProperties.Height}" },
                        { Globalization.GetString("Properties_Details_Width"), Convert.ToString(ImageProperties.Width) },
                        { Globalization.GetString("Properties_Details_Height"), Convert.ToString(ImageProperties.Height) }
                    };

                    IDictionary<string, object> ImageResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Image.BitDepth", "System.Image.ColorSpace" });

                    if (ImageResult.TryGetValue("System.Image.BitDepth", out object BitDepth))
                    {
                        ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_BitDepth"), Convert.ToString(BitDepth));
                    }
                    else
                    {
                        ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_BitDepth"), string.Empty);
                    }

                    if (ImageResult.TryGetValue("System.Image.ColorSpace", out object ColorSpace))
                    {
                        ushort ColorSpaceEnum = Convert.ToUInt16(ColorSpace);

                        if (ColorSpaceEnum == 1)
                        {
                            ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_ColorSpace"), Globalization.GetString("Properties_Details_ColorSpace_SRGB"));
                        }
                        else if (ColorSpaceEnum == ushort.MaxValue)
                        {
                            ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_ColorSpace"), Globalization.GetString("Properties_Details_ColorSpace_Uncalibrated"));
                        }
                        else
                        {
                            ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_ColorSpace"), string.Empty);
                        }
                    }
                    else
                    {
                        ImagePropertiesDictionary.Add(Globalization.GetString("Properties_Details_ColorSpace"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Image_Label"), ImagePropertiesDictionary.ToArray()));

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), ImageProperties.Title },
                        { Globalization.GetString("Properties_Details_DateTaken"), ImageProperties.DateTaken.ToFileTime() > 0 ? ImageProperties.DateTaken.ToString("G") : string.Empty},
                        { Globalization.GetString("Properties_Details_Rating"), ImageProperties.Rating }
                    };

                    IDictionary<string, object> DescriptionResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Comment" });

                    if (DescriptionResult.TryGetValue("System.Comment", out object Comment))
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), Convert.ToString(Comment));
                    }
                    else
                    {
                        DescriptionPropertiesDictionary.Add(Globalization.GetString("Properties_Details_Comment"), string.Empty);
                    }

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    Dictionary<string, object> ExtraPropertiesDictionary = new Dictionary<string, object>(6)
                    {
                        { Globalization.GetString("Properties_Details_CameraModel"), ImageProperties.CameraModel },
                        { Globalization.GetString("Properties_Details_CameraManufacturer"), ImageProperties.CameraManufacturer },
                        { Globalization.GetString("Properties_Details_Keywords"), string.Join(", ", ImageProperties.Keywords) },
                        { Globalization.GetString("Properties_Details_Latitude"), Convert.ToString(ImageProperties.Latitude) },
                        { Globalization.GetString("Properties_Details_Longitude"), Convert.ToString(ImageProperties.Longitude) },
                        { Globalization.GetString("Properties_Details_PeopleNames"), string.Join(", ", ImageProperties.PeopleNames) }
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), ExtraPropertiesDictionary.ToArray()));
                }
                else if (ContentType.StartsWith("application/msword", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.ms-powerpoint", StringComparison.OrdinalIgnoreCase)
                        || ContentType.StartsWith("application/vnd.openxmlformats-officedocument", StringComparison.OrdinalIgnoreCase))
                {
                    DocumentProperties DocProperties = await File.Properties.GetDocumentPropertiesAsync();

                    Dictionary<string, object> DescriptionPropertiesDictionary = new Dictionary<string, object>(4)
                    {
                        { Globalization.GetString("Properties_Details_Title"), DocProperties.Title },
                        { Globalization.GetString("Properties_Details_Comment"), DocProperties.Comment },
                        { Globalization.GetString("Properties_Details_Keywords"), string.Join(", ", DocProperties.Keywords) },
                        { Globalization.GetString("Properties_Details_Authors"), string.Join(", ", DocProperties.Author) },
                    };

                    PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Description"), DescriptionPropertiesDictionary.ToArray()));

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        Dictionary<string, string> DocExtraProperties = await Exclusive.Controller.GetDocumentProperties(StorageItem.Path);

                        string TotalEditingTime = DocExtraProperties["TotalEditingTime"];

                        if (!string.IsNullOrEmpty(TotalEditingTime))
                        {
                            DocExtraProperties["TotalEditingTime"] = TimeSpan.FromMilliseconds(Convert.ToUInt64(TotalEditingTime) / 10000).ConvertTimsSpanToString();
                        }

                        IDictionary<string, object> DocTimeResult = await File.Properties.RetrievePropertiesAsync(new string[] { "System.Document.DateCreated", "System.Document.DateSaved" });

                        if (DocTimeResult.TryGetValue("System.Document.DateCreated", out object DateCreated))
                        {
                            DocExtraProperties.Add("ContentCreated", ((DateTimeOffset)DateCreated).ToString("G"));
                        }
                        else
                        {
                            DocExtraProperties.Add("ContentCreated", string.Empty);
                        }

                        if (DocTimeResult.TryGetValue("System.Document.DateSaved", out object DateSaved))
                        {
                            DocExtraProperties.Add("DateLastSaved", ((DateTimeOffset)DateSaved).ToString("G"));
                        }
                        else
                        {
                            DocExtraProperties.Add("DateLastSaved", string.Empty);
                        }

                        Dictionary<string, string> TranslatedDocExtraProperties = new Dictionary<string, string>(11)
                        {
                            { Globalization.GetString("Properties_Details_LastAuthor"), DocExtraProperties["LastAuthor"] },
                            { Globalization.GetString("Properties_Details_Version"), DocExtraProperties["Version"] },
                            { Globalization.GetString("Properties_Details_RevisionNumber"), DocExtraProperties["RevisionNumber"] },
                            { Globalization.GetString("Properties_Details_PageCount"), DocExtraProperties["PageCount"] },
                            { Globalization.GetString("Properties_Details_WordCount"), DocExtraProperties["WordCount"] },
                            { Globalization.GetString("Properties_Details_CharacterCount"), DocExtraProperties["CharacterCount"] },
                            { Globalization.GetString("Properties_Details_LineCount"), DocExtraProperties["LineCount"] },
                            { Globalization.GetString("Properties_Details_Template"), DocExtraProperties["Template"] },
                            { Globalization.GetString("Properties_Details_TotalEditingTime"), DocExtraProperties["TotalEditingTime"] },
                            { Globalization.GetString("Properties_Details_ContentCreated"), DocExtraProperties["ContentCreated"] },
                            { Globalization.GetString("Properties_Details_DateLastSaved"), DocExtraProperties["DateLastSaved"] }
                        };

                        PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Extra_Label"), TranslatedDocExtraProperties.Select((Pair) => new KeyValuePair<string, object>(Pair.Key, Pair.Value))));
                    }
                }
            }
            else
            {
                PropertiesCollection.Add(new PropertiesGroupItem(Globalization.GetString("Properties_Details_Basic_Label"), BasicPropertiesDictionary.ToArray()));
            }
        }

        private async Task LoadDataForGeneralPage()
        {
            Thumbnail.Source = StorageItem.Thumbnail;
            StorageItemName.Text = StorageItem.DisplayName;
            TypeContent.Text = $"{StorageItem.DisplayType} ({StorageItem.Type})";
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
                    FolderCancellation = new CancellationTokenSource();

                    Task CountTask = CalculateFolderAndFileCount(Folder, FolderCancellation.Token).ContinueWith((task) =>
                    {
                        ContainsContent.Text = task.Result;
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    Task SizeTask = CalculateFolderSize(Folder, FolderCancellation.Token).ContinueWith((task) =>
                    {
                        SizeContent.Text = task.Result;
                    }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                    await Task.WhenAll(CountTask, SizeTask);
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
                    FolderCancellation.Dispose();
                    FolderCancellation = null;
                }
            }
            else if (StorageItem is FileSystemStorageFile File)
            {
                ReadonlyAttribute.IsChecked = File.IsReadOnly;

                string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(StorageItem.Type);

                if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath == Package.Current.Id.FamilyName)
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
                                OpenWithContent.Text = Globalization.GetString("AppDisplayName");

                                try
                                {
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
                                catch
                                {
                                    OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                                }

                                break;
                            }
                        default:
                            {
                                OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
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
                        OpenWithImage.Source = await OpenProgramFile.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);

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
                        OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                    }
                }
                else
                {
                    if ((await Launcher.FindFileHandlersAsync(StorageItem.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                    {
                        OpenWithContent.Text = Info.Package.DisplayName;

                        try
                        {
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
                        catch
                        {
                            OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
                        }
                    }
                    else
                    {
                        OpenWithContent.Text = Globalization.GetString("OpenWithEmptyText");
                        OpenWithImage.Source = new BitmapImage(new Uri(AppThemeController.Current.Theme == ElementTheme.Dark ? "ms-appx:///Assets/Page_Solid_White.png" : "ms-appx:///Assets/Page_Solid_Black.png"));
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
                    await SaveConfiguration();
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
            switch (e.Key)
            {
                case VirtualKey.Back:
                    {
                        ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        break;
                    }
                case VirtualKey.Shift:
                case VirtualKey.Control:
                case VirtualKey.CapitalLock:
                case VirtualKey.Menu:
                case VirtualKey.Space:
                case VirtualKey.Tab:
                    {
                        break;
                    }
                default:
                    {
                        string KeyName = Enum.GetName(typeof(VirtualKey), e.Key);

                        if (string.IsNullOrEmpty(KeyName))
                        {
                            ShortcutKeyContent.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        }
                        else
                        {
                            if ((e.Key >= VirtualKey.F1 && e.Key <= VirtualKey.F24) || (e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9))
                            {
                                ShortcutKeyContent.Text = KeyName;
                            }
                            else
                            {
                                ShortcutKeyContent.Text = $"Ctrl + Alt + {KeyName}";
                            }
                        }

                        break;
                    }
            }
        }

        private async void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is LinkStorageFile Link)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(new string[] { Path.GetDirectoryName(Link.LinkTargetPath) });

                if (TabViewContainer.ThisPage.TabCollection.LastOrDefault()?.Tag is FileControl Control)
                {
                    while (Control.CurrentPresenter == null)
                    {
                        await Task.Delay(500);
                    }

                    if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem.Path == Link.LinkTargetPath) is FileSystemStorageItemBase Target)
                    {
                        Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                        Control.CurrentPresenter.SelectedItem = Target;
                    }
                }
            }
        }

        private async void CalculateMd5_Click(object sender, RoutedEventArgs e)
        {
            if (await FileSystemStorageItemBase.CheckExistAsync(StorageItem.Path))
            {
                if (StorageItem is FileSystemStorageFile File)
                {
                    MD5TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                    Md5Cancellation = new CancellationTokenSource();

                    try
                    {
                        using (FileStream Stream = await File.GetFileStreamFromFileAsync(AccessMode.Read))
                        using (MD5 MD5Alg = MD5.Create())
                        {
                            await MD5Alg.GetHashAsync(Stream, Md5Cancellation.Token).ContinueWith((beforeTask) =>
                            {
                                MD5TextBox.Text = beforeTask.Result;
                                MD5TextBox.IsEnabled = true;
                            }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Calculate MD5 failed");
                    }
                    finally
                    {
                        Md5Cancellation?.Dispose();
                        Md5Cancellation = null;
                    }
                }
            }
        }

        private async void CalculateSHA1_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                SHA1TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                SHA1Cancellation = new CancellationTokenSource();

                try
                {
                    using (FileStream Stream = await File.GetFileStreamFromFileAsync(AccessMode.Read))
                    using (SHA1 SHA1Alg = SHA1.Create())
                    {
                        await SHA1Alg.GetHashAsync(Stream, SHA1Cancellation.Token).ContinueWith((beforeTask) =>
                        {
                            SHA1TextBox.Text = beforeTask.Result;
                            SHA1TextBox.IsEnabled = true;
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Calculate SHA1 failed");
                }
                finally
                {
                    SHA1Cancellation?.Dispose();
                    SHA1Cancellation = null;
                }
            }
        }

        private async void CalculateSHA256_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                SHA256TextBox.Text = Globalization.GetString("HashPlaceHolderText");
                SHA256Cancellation = new CancellationTokenSource();

                try
                {
                    using (FileStream Stream = await File.GetFileStreamFromFileAsync(AccessMode.Read))
                    using (SHA256 SHA256Alg = SHA256.Create())
                    {
                        await SHA256Alg.GetHashAsync(Stream, SHA256Cancellation.Token).ContinueWith((beforeTask) =>
                        {
                            SHA256TextBox.Text = beforeTask.Result;
                            SHA256TextBox.IsEnabled = true;
                        }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Calculate SHA256 failed");
                }
                finally
                {
                    SHA256Cancellation?.Dispose();
                    SHA256Cancellation = null;
                }
            }
        }

        private async void ChangeOpenWithButton_Click(object sender, RoutedEventArgs e)
        {
            if (StorageItem is FileSystemStorageFile File)
            {
                await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    ProgramPickerDialog Dialog = new ProgramPickerDialog(File, true);
                    await Dialog.ShowAsync();
                });

                await Window.CloseAsync();
            }
        }

        private async void Unlock_Click(object sender, RoutedEventArgs e)
        {
            UnlockFlyout.Hide();

            if (StorageItem is FileSystemStorageFile File)
            {
                try
                {
                    UnlockProgressRing.Visibility = Visibility.Visible;
                    UnlockText.Visibility = Visibility.Collapsed;

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        if (await Exclusive.Controller.TryUnlockFileOccupy(File.Path, ((Button)sender).Name == "CloseForce"))
                        {
                            UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_Success_Content");
                        }
                        else
                        {
                            UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_Failure_Content");
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_FileNotFound_Content");
                }
                catch (UnlockException)
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_NoLock_Content");
                }
                catch
                {
                    UnlockText.Text = Globalization.GetString("QueueDialog_Unlock_UnexpectedError_Content");
                }
                finally
                {
                    UnlockProgressRing.Visibility = Visibility.Collapsed;
                    UnlockText.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
