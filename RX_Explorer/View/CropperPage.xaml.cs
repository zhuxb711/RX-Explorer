using ComputerVision;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class CropperPage : Page
    {
        SoftwareBitmap OriginImage;
        SoftwareBitmap OriginBackupImage;
        SoftwareBitmap FilterImage;
        SoftwareBitmap FilterBackupImage;
        FileSystemStorageFile OriginFile;
        Rect UnchangeRegion;
        ObservableCollection<FilterItem> FilterCollection = new ObservableCollection<FilterItem>();

        public CropperPage()
        {
            InitializeComponent();

            AspList.Items.Add(Globalization.GetString("CropperPage_Custom_Text"));
            AspList.Items.Add("16:9");
            AspList.Items.Add("7:5");
            AspList.Items.Add("4:3");
            AspList.Items.Add("3:2");
            AspList.Items.Add("1:1");

            AspList.SelectedIndex = 0;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                if (e?.Parameter is PhotoDisplaySupport Item)
                {
                    OriginFile = Item.PhotoFile;
                    OriginImage = await Item.GenerateImageWithRotation().ConfigureAwait(true);
                    OriginBackupImage = SoftwareBitmap.Copy(OriginImage);

                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;
                    UnchangeRegion = Cropper.CroppedRegion;

                    await AddEffectsToPane().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when initializing CropperPage");
            }
        }

        private async Task AddEffectsToPane()
        {
            using (SoftwareBitmap ResizedImage = ComputerVisionProvider.GenenateResizedThumbnail(OriginImage, 100, 100))
            {
                SoftwareBitmapSource Source1 = new SoftwareBitmapSource();
                await Source1.SetBitmapAsync(ResizedImage);
                FilterCollection.Add(new FilterItem(Source1, Globalization.GetString("CropperPage_Filter_Type_1"), FilterType.Origin));

                using (SoftwareBitmap Bitmap2 = ComputerVisionProvider.InvertEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source2 = new SoftwareBitmapSource();
                    await Source2.SetBitmapAsync(Bitmap2);
                    FilterCollection.Add(new FilterItem(Source2, Globalization.GetString("CropperPage_Filter_Type_2"), FilterType.Invert));
                }

                using (SoftwareBitmap Bitmap3 = ComputerVisionProvider.GrayEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source3 = new SoftwareBitmapSource();
                    await Source3.SetBitmapAsync(Bitmap3);
                    FilterCollection.Add(new FilterItem(Source3, Globalization.GetString("CropperPage_Filter_Type_3"), FilterType.Gray));
                }

                using (SoftwareBitmap Bitmap4 = ComputerVisionProvider.ThresholdEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source4 = new SoftwareBitmapSource();
                    await Source4.SetBitmapAsync(Bitmap4);
                    FilterCollection.Add(new FilterItem(Source4, Globalization.GetString("CropperPage_Filter_Type_4"), FilterType.Threshold));
                }

                using (SoftwareBitmap Bitmap5 = ComputerVisionProvider.SepiaEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source5 = new SoftwareBitmapSource();
                    await Source5.SetBitmapAsync(Bitmap5);
                    FilterCollection.Add(new FilterItem(Source5, Globalization.GetString("CropperPage_Filter_Type_5"), FilterType.Sepia));
                }

                using (SoftwareBitmap Bitmap6 = ComputerVisionProvider.MosaicEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source6 = new SoftwareBitmapSource();
                    await Source6.SetBitmapAsync(Bitmap6);
                    FilterCollection.Add(new FilterItem(Source6, Globalization.GetString("CropperPage_Filter_Type_6"), FilterType.Mosaic));
                }

                using (SoftwareBitmap Bitmap7 = ComputerVisionProvider.SketchEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source7 = new SoftwareBitmapSource();
                    await Source7.SetBitmapAsync(Bitmap7);
                    FilterCollection.Add(new FilterItem(Source7, Globalization.GetString("CropperPage_Filter_Type_7"), FilterType.Sketch));
                }

                using (SoftwareBitmap Bitmap8 = ComputerVisionProvider.GaussianBlurEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source8 = new SoftwareBitmapSource();
                    await Source8.SetBitmapAsync(Bitmap8);
                    FilterCollection.Add(new FilterItem(Source8, Globalization.GetString("CropperPage_Filter_Type_8"), FilterType.GaussianBlur));
                }

                using (SoftwareBitmap Bitmap9 = ComputerVisionProvider.OilPaintingEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source9 = new SoftwareBitmapSource();
                    await Source9.SetBitmapAsync(Bitmap9);
                    FilterCollection.Add(new FilterItem(Source9, Globalization.GetString("CropperPage_Filter_Type_9"), FilterType.OilPainting));
                }
            }

            FilterGrid.SelectedIndex = 0;
            FilterGrid.SelectionChanged += FilterGrid_SelectionChanged;

            using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(OriginImage))
            {
                SoftwareBitmapSource Source = new SoftwareBitmapSource();
                await Source.SetBitmapAsync(Histogram);
                HistogramImage.Source = Source;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Cropper.Source = null;
            AspList.SelectedIndex = 0;
            FilterImage?.Dispose();
            OriginBackupImage?.Dispose();
            OriginImage?.Dispose();
            FilterBackupImage?.Dispose();
            FilterBackupImage = null;
            FilterImage = null;
            OriginBackupImage = null;
            OriginImage = null;
            OriginFile = null;
            HistogramImage.Source = null;
            FilterGrid.SelectionChanged -= FilterGrid_SelectionChanged;

            foreach (FilterItem Item in FilterCollection)
            {
                Item.Dispose();
            }
            FilterCollection.Clear();
        }

        private void OptionCancel_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void SaveAs_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            FileSavePicker Picker = new FileSavePicker
            {
                SuggestedFileName = Path.GetFileNameWithoutExtension(OriginFile.Name),
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            Picker.FileTypeChoices.Add($"PNG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".png" });
            Picker.FileTypeChoices.Add($"JPEG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".jpg" });
            Picker.FileTypeChoices.Add($"BMP {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".bmp" });
            Picker.FileTypeChoices.Add($"GIF {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".gif" });
            Picker.FileTypeChoices.Add($"TIFF {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".tiff" });

            StorageFile File = await Picker.PickSaveFileAsync();

            if (File != null)
            {
                LoadingControl.IsLoading = true;

                using (IRandomAccessStream Stream = await File.OpenAsync(FileAccessMode.ReadWrite))
                {
                    Stream.Size = 0;
                    switch (File.FileType)
                    {
                        case ".png":
                            await Cropper.SaveAsync(Stream, BitmapFileFormat.Png).ConfigureAwait(true);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            await Cropper.SaveAsync(Stream, BitmapFileFormat.Jpeg).ConfigureAwait(true);
                            break;
                        case ".bmp":
                            await Cropper.SaveAsync(Stream, BitmapFileFormat.Bmp).ConfigureAwait(true);
                            break;
                        case ".gif":
                            await Cropper.SaveAsync(Stream, BitmapFileFormat.Gif).ConfigureAwait(true);
                            break;
                        case ".tiff":
                            await Cropper.SaveAsync(Stream, BitmapFileFormat.Tiff).ConfigureAwait(true);
                            break;
                        default:
                            throw new InvalidOperationException("Unsupport image format");
                    }
                }

                await Task.Delay(1000).ConfigureAwait(true);
                LoadingControl.IsLoading = false;

                Frame.GoBack();
            }
        }

        private void ResetButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AspList.SelectedIndex = 0;
            ResetButton.IsEnabled = false;

            OriginImage.Dispose();
            OriginImage = SoftwareBitmap.Copy(OriginBackupImage);
            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
            Cropper.Source = WBitmap;

            using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(OriginImage))
            {
                WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                HistogramImage.Source = HBitmap;
            }

            FilterImage?.Dispose();
            FilterImage = null;
            FilterBackupImage?.Dispose();
            FilterBackupImage = null;

            AlphaSlider.ValueChanged -= AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged -= BetaSlider_ValueChanged;
            FilterGrid.SelectionChanged -= FilterGrid_SelectionChanged;
            FilterGrid.SelectedIndex = 0;
            FilterGrid.SelectionChanged += FilterGrid_SelectionChanged;
            AlphaSlider.Value = 1;
            BetaSlider.Value = 0;
            AlphaSlider.ValueChanged += AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged += BetaSlider_ValueChanged;

            Cropper.Reset();
        }

        private void AspList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AspText.Text = AspList.SelectedItem.ToString();
            switch (AspText.Text)
            {
                case "自定义":
                case "Custom":
                    Cropper.AspectRatio = null;
                    break;
                case "16:9":
                    Cropper.AspectRatio = 16d / 9d;
                    break;
                case "7:5":
                    Cropper.AspectRatio = 7d / 5d;
                    break;
                case "4:3":
                    Cropper.AspectRatio = 4d / 3d;
                    break;
                case "3:2":
                    Cropper.AspectRatio = 3d / 2d;
                    break;
                case "1:1":
                    Cropper.AspectRatio = 1;
                    break;
            }
            AspFlyout.Hide();
        }

        private void Cropper_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (Cropper.CroppedRegion != UnchangeRegion)
            {
                ResetButton.IsEnabled = true;
            }
            else
            {
                ResetButton.IsEnabled = false;
            }
        }

        private void AspList_ItemClick(object sender, ItemClickEventArgs e)
        {
            ResetButton.IsEnabled = true;
        }

        private async void Save_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;

            using (IRandomAccessStream Stream = await OriginFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.ReadWrite).ConfigureAwait(true))
            {
                switch (OriginFile.Type.ToLower())
                {
                    case ".png":
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Png).ConfigureAwait(true);
                        break;
                    case ".jpg":
                    case ".jpeg":
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Jpeg).ConfigureAwait(true);
                        break;
                    case ".bmp":
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Bmp).ConfigureAwait(true);
                        break;
                    case ".gif":
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Gif).ConfigureAwait(true);
                        break;
                    case ".tiff":
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Tiff).ConfigureAwait(true);
                        break;
                    default:
                        await Cropper.SaveAsync(Stream, BitmapFileFormat.Png).ConfigureAwait(true);
                        break;
                }
            }

            await Task.Delay(1000).ConfigureAwait(true);

            LoadingControl.IsLoading = false;

            Frame.GoBack();
        }

        private void RotationButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterImage == null)
            {
                SoftwareBitmap RotatedImage = ComputerVisionProvider.RotateEffect(OriginImage, 90);
                WriteableBitmap WBitmap = new WriteableBitmap(RotatedImage.PixelWidth, RotatedImage.PixelHeight);
                RotatedImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;

                OriginImage.Dispose();
                OriginImage = RotatedImage;
            }
            else
            {
                SoftwareBitmap OringinRotatedImage = ComputerVisionProvider.RotateEffect(OriginImage, 90);
                OriginImage.Dispose();
                OriginImage = OringinRotatedImage;

                SoftwareBitmap RotatedImage = ComputerVisionProvider.RotateEffect(FilterImage, 90);
                WriteableBitmap WBitmap = new WriteableBitmap(RotatedImage.PixelWidth, RotatedImage.PixelHeight);
                RotatedImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;

                FilterImage.Dispose();
                FilterImage = RotatedImage;
            }

            ResetButton.IsEnabled = true;
        }

        private void FlipButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterImage == null)
            {
                SoftwareBitmap FlipImage = ComputerVisionProvider.FlipEffect(OriginImage, false);
                OriginImage.Dispose();
                OriginImage = FlipImage;

                WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;
            }
            else
            {
                SoftwareBitmap FlipImage = ComputerVisionProvider.FlipEffect(OriginImage, false);
                OriginImage.Dispose();
                OriginImage = FlipImage;

                SoftwareBitmap FilterFlipImage = ComputerVisionProvider.FlipEffect(FilterImage, false);
                FilterImage.Dispose();
                FilterImage = FilterFlipImage;

                WriteableBitmap WBitmap = new WriteableBitmap(FilterImage.PixelWidth, FilterImage.PixelHeight);
                FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;
            }

            ResetButton.IsEnabled = true;
        }

        private void AlphaSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OriginImage != null)
            {
                ResetButton.IsEnabled = true;
                if (FilterImage == null)
                {
                    OriginImage.Dispose();
                    OriginImage = ComputerVisionProvider.AdjustBrightnessContrast(OriginBackupImage, e.NewValue, BetaSlider.Value);

                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(OriginImage))
                    {
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
                else
                {
                    FilterImage.Dispose();
                    FilterImage = ComputerVisionProvider.AdjustBrightnessContrast(FilterBackupImage, e.NewValue, BetaSlider.Value);

                    WriteableBitmap WBitmap = new WriteableBitmap(FilterImage.PixelWidth, FilterImage.PixelHeight);
                    FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(FilterImage))
                    {
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
            }
        }

        private void BetaSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OriginImage != null)
            {
                ResetButton.IsEnabled = true;
                if (FilterImage == null)
                {
                    OriginImage.Dispose();
                    OriginImage = ComputerVisionProvider.AdjustBrightnessContrast(OriginBackupImage, AlphaSlider.Value, e.NewValue);

                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(OriginImage))
                    {
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
                else
                {
                    FilterImage.Dispose();
                    FilterImage = ComputerVisionProvider.AdjustBrightnessContrast(FilterBackupImage, AlphaSlider.Value, e.NewValue);

                    WriteableBitmap WBitmap = new WriteableBitmap(FilterImage.PixelWidth, FilterImage.PixelHeight);
                    FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(FilterImage))
                    {
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
            }
        }

        private void AutoOptimizeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterImage != null)
            {
                FilterImage.Dispose();
                FilterImage = null;
                FilterBackupImage.Dispose();
                FilterBackupImage = null;
            }

            AlphaSlider.ValueChanged -= AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged -= BetaSlider_ValueChanged;
            AlphaSlider.Value = 1;
            BetaSlider.Value = 0;
            AlphaSlider.ValueChanged += AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged += BetaSlider_ValueChanged;

            FilterImage = ComputerVisionProvider.AutoColorEnhancement(OriginImage);

            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
            Cropper.Source = WBitmap;

            using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(FilterImage))
            {
                WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                HistogramImage.Source = HBitmap;
            }

            ResetButton.IsEnabled = true;
        }

        private void FilterGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterGrid.SelectedItem is FilterItem Item)
            {
                if (FilterImage != null)
                {
                    FilterImage.Dispose();
                    FilterImage = null;
                    FilterBackupImage.Dispose();
                    FilterBackupImage = null;
                }

                AlphaSlider.ValueChanged -= AlphaSlider_ValueChanged;
                BetaSlider.ValueChanged -= BetaSlider_ValueChanged;
                AlphaSlider.Value = 1;
                BetaSlider.Value = 0;
                AlphaSlider.ValueChanged += AlphaSlider_ValueChanged;
                BetaSlider.ValueChanged += BetaSlider_ValueChanged;

                switch (Item.Type)
                {
                    case FilterType.Origin:
                        {
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);

                            Cropper.Source = WBitmap;
                            break;
                        }
                    case FilterType.Invert:
                        {
                            using (SoftwareBitmap InvertImage = ComputerVisionProvider.InvertEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(InvertImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(InvertImage.PixelWidth, InvertImage.PixelHeight);
                                InvertImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.Gray:
                        {
                            using (SoftwareBitmap GrayImage = ComputerVisionProvider.GrayEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(GrayImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(GrayImage.PixelWidth, GrayImage.PixelHeight);
                                GrayImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.Threshold:
                        {
                            using (SoftwareBitmap ThresholdImage = ComputerVisionProvider.ThresholdEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(ThresholdImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(ThresholdImage.PixelWidth, ThresholdImage.PixelHeight);
                                ThresholdImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.Sketch:
                        {
                            using (SoftwareBitmap SketchImage = ComputerVisionProvider.SketchEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(SketchImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(SketchImage.PixelWidth, SketchImage.PixelHeight);
                                SketchImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.GaussianBlur:
                        {
                            using (SoftwareBitmap GaussianBlurImage = ComputerVisionProvider.GaussianBlurEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(GaussianBlurImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(GaussianBlurImage.PixelWidth, GaussianBlurImage.PixelHeight);
                                GaussianBlurImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.Sepia:
                        {
                            using (SoftwareBitmap SepiaImage = ComputerVisionProvider.SepiaEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(SepiaImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(SepiaImage.PixelWidth, SepiaImage.PixelHeight);
                                SepiaImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.OilPainting:
                        {
                            using (SoftwareBitmap OilPaintingImage = ComputerVisionProvider.OilPaintingEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(OilPaintingImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(OilPaintingImage.PixelWidth, OilPaintingImage.PixelHeight);
                                OilPaintingImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                    case FilterType.Mosaic:
                        {
                            using (SoftwareBitmap MosaicImage = ComputerVisionProvider.MosaicEffect(OriginImage))
                            {
                                FilterImage = SoftwareBitmap.Copy(MosaicImage);
                                FilterBackupImage = SoftwareBitmap.Copy(FilterImage);

                                WriteableBitmap WBitmap = new WriteableBitmap(MosaicImage.PixelWidth, MosaicImage.PixelHeight);
                                MosaicImage.CopyToBuffer(WBitmap.PixelBuffer);

                                Cropper.Source = WBitmap;
                            }
                            break;
                        }
                }

                using (SoftwareBitmap Histogram = ComputerVisionProvider.CalculateHistogram(Item.Type == FilterType.Origin ? OriginImage : FilterImage))
                {
                    WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                    Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                    HistogramImage.Source = HBitmap;
                }

                ResetButton.IsEnabled = true;
            }
        }
    }
}
