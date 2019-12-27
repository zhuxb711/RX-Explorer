using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace FileManager
{
    public sealed partial class TranscodeImageDialog : QueueContentDialog
    {
        public StorageFile TargetFile { get; private set; }

        private readonly uint PixelWidth;

        private readonly uint PixelHeight;

        private FileSavePicker Picker;

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
            TargetFile = await Picker.PickSaveFileAsync();
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
            PreviewText.Text = Globalization.Language == LanguageEnum.Chinese ? ("预览分辨率: " + ScaleWidth + " X " + ScaleHeight) : ("Preview resolution: " + ScaleWidth + " X " + ScaleHeight);
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
            Picker = new FileSavePicker
            {
                SuggestedFileName = "TranscodeImage",
                SuggestedStartLocation = PickerLocationId.Desktop
            };

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                switch (Format.SelectedItem.ToString().Split(" ").FirstOrDefault())
                {
                    case "PNG":
                        {
                            Picker.FileTypeChoices.Add("PNG格式", new List<string>() { ".png" });
                            break;
                        }
                    case "BMP":
                        {
                            Picker.FileTypeChoices.Add("BMP格式", new List<string>() { ".bmp" });
                            break;
                        }
                    case "JPEG":
                        {
                            Picker.FileTypeChoices.Add("JPEG格式", new List<string>() { ".jpg" });
                            break;
                        }
                    case "HEIF":
                        {
                            Picker.FileTypeChoices.Add("HEIF格式", new List<string>() { ".heic" });
                            break;
                        }
                    case "TIFF":
                        {
                            Picker.FileTypeChoices.Add("TIFF格式", new List<string>() { ".tiff" });
                            break;
                        }
                }
            }
            else
            {
                switch (Format.SelectedItem.ToString().Split(" ").FirstOrDefault())
                {
                    case "PNG":
                        {
                            Picker.FileTypeChoices.Add("PNG format", new List<string>() { ".png" });
                            break;
                        }
                    case "BMP":
                        {
                            Picker.FileTypeChoices.Add("BMP format", new List<string>() { ".bmp" });
                            break;
                        }
                    case "JPEG":
                        {
                            Picker.FileTypeChoices.Add("JPEG format", new List<string>() { ".jpg" });
                            break;
                        }
                    case "HEIF":
                        {
                            Picker.FileTypeChoices.Add("HEIF format", new List<string>() { ".heic" });
                            break;
                        }
                    case "TIFF":
                        {
                            Picker.FileTypeChoices.Add("TIFF format", new List<string>() { ".tiff" });
                            break;
                        }
                }
            }
        }
    }
}
