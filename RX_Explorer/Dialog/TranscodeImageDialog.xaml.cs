using RX_Explorer.Class;
using System;
using System.Linq;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class TranscodeImageDialog : QueueContentDialog
    {
        public FileSystemStorageFile TargetFile { get; private set; }

        private readonly uint PixelWidth;

        private readonly uint PixelHeight;

        private FileSavePicker Picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop
        };

        public uint ScaleWidth { get; private set; }

        public uint ScaleHeight { get; private set; }

        public bool IsEnableScale { get; private set; }

        public BitmapInterpolationMode InterpolationMode { get; private set; }

        public TranscodeImageDialog(uint PixelWidth, uint PixelHeight)
        {
            InitializeComponent();
            this.PixelWidth = PixelWidth;
            this.PixelHeight = PixelHeight;
            ScaleCombo.SelectedIndex = 0;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (TargetFile == null)
            {
                SaveErrorTip.IsOpen = true;
                args.Cancel = true;
            }
        }

        private async void SavePositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (await Picker.PickSaveFileAsync() is StorageFile SaveFile)
            {
                TargetFile = new FileSystemStorageFile(SaveFile, await SaveFile.GetSizeRawDataAsync().ConfigureAwait(true), await SaveFile.GetModifiedTimeAsync().ConfigureAwait(true));
            }
        }

        private (uint, uint) GetScalePixelData(string ComboBoxInputText)
        {
            float ScaleFactor = Convert.ToSingle(ComboBoxInputText.Remove(ComboBoxInputText.Length - 1)) / 100;
            if (ScaleFactor == 1f)
            {
                return (PixelWidth, PixelHeight);
            }

            var ScalePixelWidth = Convert.ToUInt32(Math.Round(PixelWidth * ScaleFactor, MidpointRounding.AwayFromZero));
            var ScalePixelHeight = Convert.ToUInt32(Math.Round(PixelHeight * ScaleFactor, MidpointRounding.AwayFromZero));
            return (ScalePixelWidth, ScalePixelHeight);
        }

        private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var ScalePixelData = GetScalePixelData(ScaleCombo.SelectedItem.ToString());
            ScaleWidth = ScalePixelData.Item1;
            ScaleHeight = ScalePixelData.Item2;
            PreviewText.Text = $"{Globalization.GetString("Transcode_Image_Preview_Resolution")}: {ScaleWidth} X {ScaleHeight}";
        }

        private void ScaleMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ScaleMode.SelectedItem.ToString())
            {
                case "Fant":
                    InterpolationMode = BitmapInterpolationMode.Fant;
                    break;
                case "Cubic":
                    InterpolationMode = BitmapInterpolationMode.Cubic;
                    break;
                case "Linear":
                    InterpolationMode = BitmapInterpolationMode.Linear;
                    break;
                case "NearestNeighbor":
                    InterpolationMode = BitmapInterpolationMode.NearestNeighbor;
                    break;
            }
        }

        private void Format_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TargetFile = null;

            Picker.FileTypeChoices.Clear();

            switch (Format.SelectedItem.ToString().Split(" ").FirstOrDefault())
            {
                case "PNG":
                    {
                        Picker.FileTypeChoices.Add($"PNG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new string[] { ".png" });
                        Picker.SuggestedFileName = "Image.png";
                        break;
                    }
                case "BMP":
                    {
                        Picker.FileTypeChoices.Add($"BMP {Globalization.GetString("Transcode_Dialog_Format_Text")}", new string[] { ".bmp" });
                        Picker.SuggestedFileName = "Image.bmp";
                        break;
                    }
                case "JPEG":
                    {
                        Picker.FileTypeChoices.Add($"JPEG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new string[] { ".jpg" });
                        Picker.SuggestedFileName = "Image.jpg";
                        break;
                    }
                case "HEIF":
                    {
                        Picker.FileTypeChoices.Add($"HEIF {Globalization.GetString("Transcode_Dialog_Format_Text")}", new string[] { ".heic" });
                        Picker.SuggestedFileName = "Image.heic";
                        break;
                    }
                case "TIFF":
                    {
                        Picker.FileTypeChoices.Add($"TIFF {Globalization.GetString("Transcode_Dialog_Format_Text")}", new string[] { ".tiff" });
                        Picker.SuggestedFileName = "Image.tiff";
                        break;
                    }
            }
        }
    }
}
