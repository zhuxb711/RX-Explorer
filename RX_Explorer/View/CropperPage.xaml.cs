using ComputerVision;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using ShareClassLibrary;
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

namespace RX_Explorer.View
{
    public sealed partial class CropperPage : Page
    {
        private SoftwareBitmap OriginImage;
        private SoftwareBitmap OriginBackupImage;
        private SoftwareBitmap FilterImage;
        private SoftwareBitmap FilterBackupImage;
        private FileSystemStorageFile OriginFile;
        private Rect UnchangedRegion;
        private readonly ObservableCollection<ImageFilterItem> FilterCollection = new ObservableCollection<ImageFilterItem>();

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
                if (e.Parameter is PhotoDisplayItem Item)
                {
                    OriginFile = Item.PhotoFile;

                    using (Stream FileStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                    {
                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FileStream.AsRandomAccessStream());
                        OriginImage = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                        OriginBackupImage = SoftwareBitmap.Copy(OriginImage);
                    }

                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;
                    UnchangedRegion = Cropper.CroppedRegion;

                    await AddEffectsToPane().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when initializing CropperPage");
            }
        }

        private async Task AddEffectsToPane()
        {
            using (SoftwareBitmap ResizedImage = ComputerVisionProvider.GenenateResizedThumbnail(OriginImage, 100, 100))
            {
                SoftwareBitmapSource Source1 = new SoftwareBitmapSource();
                await Source1.SetBitmapAsync(ResizedImage);
                FilterCollection.Add(new ImageFilterItem(Source1, Globalization.GetString("CropperPage_Filter_Type_1"), FilterType.Origin));

                using (SoftwareBitmap Bitmap2 = ComputerVisionProvider.InvertEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source2 = new SoftwareBitmapSource();
                    await Source2.SetBitmapAsync(Bitmap2);
                    FilterCollection.Add(new ImageFilterItem(Source2, Globalization.GetString("CropperPage_Filter_Type_2"), FilterType.Invert));
                }

                using (SoftwareBitmap Bitmap3 = ComputerVisionProvider.GrayEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source3 = new SoftwareBitmapSource();
                    await Source3.SetBitmapAsync(Bitmap3);
                    FilterCollection.Add(new ImageFilterItem(Source3, Globalization.GetString("CropperPage_Filter_Type_3"), FilterType.Gray));
                }

                using (SoftwareBitmap Bitmap4 = ComputerVisionProvider.ThresholdEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source4 = new SoftwareBitmapSource();
                    await Source4.SetBitmapAsync(Bitmap4);
                    FilterCollection.Add(new ImageFilterItem(Source4, Globalization.GetString("CropperPage_Filter_Type_4"), FilterType.Threshold));
                }

                using (SoftwareBitmap Bitmap5 = ComputerVisionProvider.SepiaEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source5 = new SoftwareBitmapSource();
                    await Source5.SetBitmapAsync(Bitmap5);
                    FilterCollection.Add(new ImageFilterItem(Source5, Globalization.GetString("CropperPage_Filter_Type_5"), FilterType.Sepia));
                }

                using (SoftwareBitmap Bitmap6 = ComputerVisionProvider.MosaicEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source6 = new SoftwareBitmapSource();
                    await Source6.SetBitmapAsync(Bitmap6);
                    FilterCollection.Add(new ImageFilterItem(Source6, Globalization.GetString("CropperPage_Filter_Type_6"), FilterType.Mosaic));
                }

                using (SoftwareBitmap Bitmap7 = ComputerVisionProvider.SketchEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source7 = new SoftwareBitmapSource();
                    await Source7.SetBitmapAsync(Bitmap7);
                    FilterCollection.Add(new ImageFilterItem(Source7, Globalization.GetString("CropperPage_Filter_Type_7"), FilterType.Sketch));
                }

                using (SoftwareBitmap Bitmap8 = ComputerVisionProvider.GaussianBlurEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source8 = new SoftwareBitmapSource();
                    await Source8.SetBitmapAsync(Bitmap8);
                    FilterCollection.Add(new ImageFilterItem(Source8, Globalization.GetString("CropperPage_Filter_Type_8"), FilterType.GaussianBlur));
                }

                using (SoftwareBitmap Bitmap9 = ComputerVisionProvider.OilPaintingEffect(ResizedImage))
                {
                    SoftwareBitmapSource Source9 = new SoftwareBitmapSource();
                    await Source9.SetBitmapAsync(Bitmap9);
                    FilterCollection.Add(new ImageFilterItem(Source9, Globalization.GetString("CropperPage_Filter_Type_9"), FilterType.OilPainting));
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

            foreach (ImageFilterItem Item in FilterCollection)
            {
                Item.Dispose();
            }

            FilterCollection.Clear();
        }

        private void OptionCancel_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void SaveAs_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            try
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedFileName = Path.GetFileNameWithoutExtension(OriginFile.Name),
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                Picker.FileTypeChoices.Add($"PNG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".png" });
                Picker.FileTypeChoices.Add($"JPEG {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".jpg", ".jpeg" });
                Picker.FileTypeChoices.Add($"BMP {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".bmp" });
                Picker.FileTypeChoices.Add($"TIFF {Globalization.GetString("Transcode_Dialog_Format_Text")}", new List<string>() { ".tiff" });

                if (await Picker.PickSaveFileAsync() is StorageFile File)
                {
                    LoadingControl.IsLoading = true;

                    await Task.WhenAll(SaveToFileAsync(new FileSystemStorageFile(File)), Task.Delay(1000));

                    LoadingControl.IsLoading = false;

                    if (Frame.CanGoBack)
                    {
                        Frame.GoBack();
                    }
                }
            }
            catch (NotSupportedException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportedImageFormat_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not save the image data");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_SaveFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async Task SaveToFileAsync(FileSystemStorageFile File)
        {
            using (InMemoryRandomAccessStream TempStream = new InMemoryRandomAccessStream())
            using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Exclusive, OptimizeOption.RandomAccess))
            {
                switch (File.Type.ToLower())
                {
                    case ".png":
                        {
                            await Cropper.SaveAsync(TempStream, BitmapFileFormat.Png);
                            break;
                        }
                    case ".jpg":
                    case ".jpeg":
                        {
                            await Cropper.SaveAsync(TempStream, BitmapFileFormat.Jpeg);
                            break;
                        }
                    case ".bmp":
                        {
                            await Cropper.SaveAsync(TempStream, BitmapFileFormat.Bmp);
                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }

                FileStream.SetLength(0);

                await TempStream.AsStreamForRead().CopyToAsync(FileStream);
                await FileStream.FlushAsync();
            }
        }

        private void ResetButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            AspList.SelectedIndex = 0;
            ResetButton.IsEnabled = false;

            OriginImage?.Dispose();
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
            if (Cropper.CroppedRegion != UnchangedRegion)
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
            try
            {
                LoadingControl.IsLoading = true;

                await Task.WhenAll(SaveToFileAsync(OriginFile), Task.Delay(1000));

                LoadingControl.IsLoading = false;

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            catch (NotSupportedException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_NotSupportedImageFormat_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not save the image data");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_SaveFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private void RotationButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterImage == null)
            {
                SoftwareBitmap RotatedImage = ComputerVisionProvider.RotateEffect(OriginImage, 90);
                WriteableBitmap WBitmap = new WriteableBitmap(RotatedImage.PixelWidth, RotatedImage.PixelHeight);
                RotatedImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;

                OriginImage?.Dispose();
                OriginImage = RotatedImage;
            }
            else
            {
                SoftwareBitmap OringinRotatedImage = ComputerVisionProvider.RotateEffect(OriginImage, 90);
                OriginImage?.Dispose();
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
                OriginImage?.Dispose();
                OriginImage = FlipImage;

                WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;
            }
            else
            {
                SoftwareBitmap FlipImage = ComputerVisionProvider.FlipEffect(OriginImage, false);
                OriginImage?.Dispose();
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
                    OriginImage?.Dispose();
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
                    OriginImage?.Dispose();
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
            if (FilterGrid.SelectedItem is ImageFilterItem Item)
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
