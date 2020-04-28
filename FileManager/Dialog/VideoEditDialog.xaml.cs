using FileManager.Class;
using System;
using System.Text.RegularExpressions;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager.Dialog
{
    public sealed partial class VideoEditDialog : QueueContentDialog
    {
        private readonly StorageFile VideoFile;

        public MediaComposition Composition { get; private set; }

        public MediaEncodingProfile MediaEncoding { get; private set; }

        public MediaTrimmingPreference TrimmingPreference { get; private set; }

        public string ExportFileType { get; private set; }

        private MediaSource PreviewSource;

        private MediaClip VideoClip;

        public VideoEditDialog(StorageFile VideoFile)
        {
            InitializeComponent();
            this.VideoFile = VideoFile;
            Loading += VideoEditDialog_Loading;

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                EncodingProfile.Items.Add("MP4编码(.mp4)");
                EncodingProfile.Items.Add("WMV编码(.wmv)");
                EncodingProfile.Items.Add("MKV编码(.mkv)");

                EncodingQuality.Items.Add("2160p");
                EncodingQuality.Items.Add("1080p");
                EncodingQuality.Items.Add("720p");
                EncodingQuality.Items.Add("480p");

                TrimmingProfile.Items.Add("精确裁剪 (速度较慢)");
                TrimmingProfile.Items.Add("快速裁剪 (速度较快)");

                EncodingProfile.SelectedIndex = 0;
                EncodingQuality.SelectedIndex = 0;
                TrimmingProfile.SelectedIndex = 0;
            }
            else
            {
                EncodingProfile.Items.Add("MP4 Encoding(.mp4)");
                EncodingProfile.Items.Add("WMV Encoding(.wmv)");
                EncodingProfile.Items.Add("MKV Encoding(.mkv)");

                EncodingQuality.Items.Add("2160p");
                EncodingQuality.Items.Add("1080p");
                EncodingQuality.Items.Add("720p");
                EncodingQuality.Items.Add("480p");

                TrimmingProfile.Items.Add("Precision (Slower)");
                TrimmingProfile.Items.Add("Fast (Faster)");

                EncodingProfile.SelectedIndex = 0;
                EncodingQuality.SelectedIndex = 0;
                TrimmingProfile.SelectedIndex = 0;
            }
        }

        private async void VideoEditDialog_Loading(FrameworkElement sender, object args)
        {
            Composition = new MediaComposition();
            VideoClip = await MediaClip.CreateFromFileAsync(VideoFile);
            Composition.Clips.Add(VideoClip);

            PreviewSource = MediaSource.CreateFromMediaStreamSource(Composition.GeneratePreviewMediaStreamSource(640, 360));
            MediaPlay.Source = PreviewSource;

            CutRange.Maximum = VideoClip.OriginalDuration.TotalMilliseconds;
            CutRange.RangeMax = CutRange.Maximum;
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            if (UseSameProfile.IsChecked.GetValueOrDefault())
            {
                MediaEncoding = await MediaEncodingProfile.CreateFromFileAsync(VideoFile);
                TrimmingPreference = MediaTrimmingPreference.Precise;
                ExportFileType = VideoFile.FileType;
            }
            else
            {
                VideoEncodingQuality Quality = default;
                switch (EncodingQuality.SelectedIndex)
                {
                    case 0:
                        {
                            Quality = VideoEncodingQuality.Uhd2160p;
                            break;
                        }
                    case 1:
                        {
                            Quality = VideoEncodingQuality.HD1080p;
                            break;
                        }
                    case 2:
                        {
                            Quality = VideoEncodingQuality.HD720p;
                            break;
                        }
                    case 3:
                        {
                            Quality = VideoEncodingQuality.Wvga;
                            break;
                        }
                }
                switch (EncodingProfile.SelectedIndex)
                {
                    case 0:
                        {
                            MediaEncoding = MediaEncodingProfile.CreateMp4(Quality);
                            ExportFileType = ".mp4";
                            break;
                        }
                    case 1:
                        {
                            MediaEncoding = MediaEncodingProfile.CreateWmv(Quality);
                            ExportFileType = ".wmv";
                            break;
                        }
                    case 2:
                        {
                            MediaEncoding = MediaEncodingProfile.CreateHevc(Quality);
                            ExportFileType = ".mkv";
                            break;
                        }
                }

                if (TrimmingProfile.SelectedIndex == 0)
                {
                    TrimmingPreference = MediaTrimmingPreference.Precise;
                }
                else
                {
                    TrimmingPreference = MediaTrimmingPreference.Fast;
                }
            }

            PreviewSource?.Dispose();
            MediaPlay.Source = null;

            Deferral.Complete();
        }

        private void QueueContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Composition = null;
            PreviewSource?.Dispose();
            MediaPlay.Source = null;
        }

        private void CutRange_ValueChanged(object sender, Microsoft.Toolkit.Uwp.UI.Controls.RangeChangedEventArgs e)
        {
            TrimStartTime.LostFocus -= TrimStartTime_LostFocus;
            TrimEndTime.LostFocus -= TrimEndTime_LostFocus;
            CutRange.Focus(FocusState.Programmatic);
            TrimStartTime.LostFocus += TrimStartTime_LostFocus;
            TrimEndTime.LostFocus += TrimEndTime_LostFocus;

            if (e.ChangedRangeProperty == Microsoft.Toolkit.Uwp.UI.Controls.RangeSelectorProperty.MaximumValue)
            {
                UpdatePreviewVideoOnDisplay(true, CutRange.RangeMax, null);
            }
            else
            {
                UpdatePreviewVideoOnDisplay(false, null, CutRange.RangeMin);
            }
        }

        private void UpdatePreviewVideoOnDisplay(bool IsMaxRangeChanged, double? MaxRange, double? MinRange)
        {
            if (MediaPlay.MediaPlayer.PlaybackSession.CanPause)
            {
                MediaPlay.MediaPlayer.Pause();
            }

            MediaPlay.Source = null;

            if (IsMaxRangeChanged)
            {
                if (MaxRange != null)
                {

                    if (MaxRange > VideoClip.OriginalDuration.TotalMilliseconds)
                    {
                        VideoClip.TrimTimeFromEnd = TimeSpan.FromMilliseconds(0);
                    }
                    else
                    {
                        VideoClip.TrimTimeFromEnd = TimeSpan.FromMilliseconds(VideoClip.OriginalDuration.TotalMilliseconds - MaxRange.Value);
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                if (MinRange != null)
                {
                    if (MinRange > 0)
                    {
                        VideoClip.TrimTimeFromStart = TimeSpan.FromMilliseconds(MinRange.Value);
                    }
                    else
                    {
                        VideoClip.TrimTimeFromStart = TimeSpan.FromMilliseconds(0);
                    }
                }
                else
                {
                    return;
                }
            }

            if (PreviewSource != null)
            {
                PreviewSource.Dispose();
                PreviewSource = null;
            }

            if (VideoClip.TrimmedDuration == TimeSpan.Zero)
            {
                MediaPlay.Source = null;
            }
            else
            {
                PreviewSource = MediaSource.CreateFromMediaStreamSource(Composition.GeneratePreviewMediaStreamSource(640, 360));

                MediaPlay.Source = PreviewSource;
            }
        }

        private void TrimStartTime_LostFocus(object sender, RoutedEventArgs e)
        {
            Regex Reg = new Regex(@"(?:[0-9]{1,2}\:[0-5][0-9]\:[0-5][0-9](?:\.[0-9]{1,3})?$)");
            if (Reg.IsMatch(TrimStartTime.Text))
            {
                if (TrimStartTime.Text.Contains("."))
                {
                    var FirstSplit = TrimStartTime.Text.Split('.');
                    string MilliSe = FirstSplit[1];
                    if (MilliSe.Length != 3)
                    {
                        MilliSe = MilliSe.PadRight(3, '0');
                    }
                    short MilliSecond = Convert.ToInt16(MilliSe);

                    var SecondSplit = FirstSplit[0].Split(':');
                    short Hour = Convert.ToInt16(SecondSplit[0]);
                    short Minute = Convert.ToInt16(SecondSplit[1]);
                    short Second = Convert.ToInt16(SecondSplit[2]);

                    long TotalMillisecond = MilliSecond + (Second + (Minute + (Hour * 60)) * 60) * 1000;

                    CutRange.ValueChanged -= CutRange_ValueChanged;
                    CutRange.RangeMin = TotalMillisecond;
                    CutRange.ValueChanged += CutRange_ValueChanged;

                    UpdatePreviewVideoOnDisplay(false, null, TotalMillisecond);
                }
                else
                {
                    var Split = TrimStartTime.Text.Split(':');
                    short Hour = Convert.ToInt16(Split[0]);
                    short Minute = Convert.ToInt16(Split[1]);
                    short Second = Convert.ToInt16(Split[2]);

                    long TotalMillisecond = (Second + (Minute + (Hour * 60)) * 60) * 1000;

                    CutRange.ValueChanged -= CutRange_ValueChanged;
                    CutRange.RangeMin = TotalMillisecond;
                    CutRange.ValueChanged += CutRange_ValueChanged;

                    UpdatePreviewVideoOnDisplay(false, null, TotalMillisecond);
                }
            }
            else
            {
                FormatErrorTip.Target = TrimStartTime;
                FormatErrorTip.IsOpen = true;
            }
        }

        private void TrimStartTime_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CutRange.Focus(FocusState.Programmatic);
            }
        }

        private void TrimEndTime_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CutRange.Focus(FocusState.Programmatic);
            }
        }

        private void TrimEndTime_LostFocus(object sender, RoutedEventArgs e)
        {
            Regex Reg = new Regex(@"(?:[0-9]{1,2}\:[0-5][0-9]\:[0-5][0-9](?:\.[0-9]{1,3})?$)");
            if (Reg.IsMatch(TrimEndTime.Text))
            {
                if (TrimEndTime.Text.Contains("."))
                {
                    var FirstSplit = TrimEndTime.Text.Split('.');
                    string MilliSe = FirstSplit[1];
                    if (MilliSe.Length != 3)
                    {
                        MilliSe = MilliSe.PadRight(3, '0');
                    }
                    short MilliSecond = Convert.ToInt16(MilliSe);

                    var SecondSplit = FirstSplit[0].Split(':');
                    short Hour = Convert.ToInt16(SecondSplit[0]);
                    short Minute = Convert.ToInt16(SecondSplit[1]);
                    short Second = Convert.ToInt16(SecondSplit[2]);

                    long TotalMillisecond = MilliSecond + (Second + (Minute + (Hour * 60)) * 60) * 1000;

                    CutRange.ValueChanged -= CutRange_ValueChanged;
                    CutRange.RangeMax = TotalMillisecond;
                    CutRange.ValueChanged += CutRange_ValueChanged;

                    UpdatePreviewVideoOnDisplay(true, TotalMillisecond, null);
                }
                else
                {
                    var Split = TrimEndTime.Text.Split(':');
                    short Hour = Convert.ToInt16(Split[0]);
                    short Minute = Convert.ToInt16(Split[1]);
                    short Second = Convert.ToInt16(Split[2]);

                    long TotalMillisecond = (Second + (Minute + (Hour * 60)) * 60) * 1000;

                    CutRange.ValueChanged -= CutRange_ValueChanged;
                    CutRange.RangeMax = TotalMillisecond;
                    CutRange.ValueChanged += CutRange_ValueChanged;

                    UpdatePreviewVideoOnDisplay(true, TotalMillisecond, null);
                }
            }
            else
            {
                FormatErrorTip.Target = TrimEndTime;
                FormatErrorTip.IsOpen = true;
            }
        }
    }
}
