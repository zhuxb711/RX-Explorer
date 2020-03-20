using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class CropperPage : Page
    {
        SoftwareBitmap OriginImage;
        SoftwareBitmap OriginBackupImage;
        SoftwareBitmap FilterImage;
        SoftwareBitmap FilterBackupImage;
        StorageFile OriginFile;
        Rect UnchangeRegion;
        ObservableCollection<FilterItem> FilterCollection = new ObservableCollection<FilterItem>();
        Frame FileControlNav;

        public CropperPage()
        {
            InitializeComponent();
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                AspList.Items.Add("自定义");
            }
            else
            {
                AspList.Items.Add("Custom");
            }
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
                if (e.Parameter is Tuple<Frame, object> Parameters)
                {
                    FileControlNav = Parameters.Item1;

                    PhotoDisplaySupport Item = Parameters.Item2 as PhotoDisplaySupport;
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
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async Task AddEffectsToPane()
        {
            SoftwareBitmapSource Source1 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source2 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source3 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source4 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source5 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source6 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source7 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source8 = new SoftwareBitmapSource();
            SoftwareBitmapSource Source9 = new SoftwareBitmapSource();

            using (SoftwareBitmap Bitmap1 = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 100, 100, BitmapAlphaMode.Premultiplied))
            {
                OpenCV.OpenCVLibrary.GenenateResizedThumbnail(OriginImage, Bitmap1, 100, 100);
                await Source1.SetBitmapAsync(Bitmap1);

                using (SoftwareBitmap Bitmap2 = SoftwareBitmap.Copy(Bitmap1))
                {
                    OpenCV.OpenCVLibrary.InvertEffect(Bitmap2, Bitmap2);
                    await Source2.SetBitmapAsync(Bitmap2);
                }

                using (SoftwareBitmap Bitmap3 = SoftwareBitmap.Copy(Bitmap1))
                {
                    WriteableBitmap WBitmap = new WriteableBitmap(Bitmap3.PixelWidth, Bitmap3.PixelHeight);
                    Bitmap3.CopyToBuffer(WBitmap.PixelBuffer);
                    Bitmap3.CopyFromBuffer(WBitmap.Gray().PixelBuffer);
                    await Source3.SetBitmapAsync(Bitmap3);
                }

                using (SoftwareBitmap Bitmap4 = SoftwareBitmap.Copy(Bitmap1))
                {
                    WriteableBitmap WBitmap1 = new WriteableBitmap(Bitmap4.PixelWidth, Bitmap4.PixelHeight);
                    Bitmap4.CopyToBuffer(WBitmap1.PixelBuffer);
                    Bitmap4.CopyFromBuffer(WBitmap1.Gray().PixelBuffer);
                    OpenCV.OpenCVLibrary.ThresholdEffect(Bitmap4, Bitmap4);
                    await Source4.SetBitmapAsync(Bitmap4);
                }

                using (SoftwareBitmap Bitmap8 = SoftwareBitmap.Copy(Bitmap1))
                {
                    OpenCV.OpenCVLibrary.SepiaEffect(Bitmap8, Bitmap8);
                    await Source8.SetBitmapAsync(Bitmap8);
                }

                using (SoftwareBitmap Bitmap9 = SoftwareBitmap.Copy(Bitmap1))
                {
                    OpenCV.OpenCVLibrary.MosaicEffect(Bitmap9, Bitmap9);
                    await Source9.SetBitmapAsync(Bitmap9);
                }

                using (SoftwareBitmap Bitmap5 = SoftwareBitmap.Copy(Bitmap1))
                {
                    WriteableBitmap WBitmap2 = new WriteableBitmap(Bitmap5.PixelWidth, Bitmap5.PixelHeight);
                    Bitmap5.CopyToBuffer(WBitmap2.PixelBuffer);
                    Bitmap5.CopyFromBuffer(WBitmap2.Gray().PixelBuffer);
                    OpenCV.OpenCVLibrary.SketchEffect(Bitmap5, Bitmap5);
                    await Source5.SetBitmapAsync(Bitmap5);
                }

                using (SoftwareBitmap Bitmap6 = SoftwareBitmap.Copy(Bitmap1))
                {
                    OpenCV.OpenCVLibrary.GaussianBlurEffect(Bitmap6, Bitmap6);
                    await Source6.SetBitmapAsync(Bitmap6);
                }

                using (SoftwareBitmap Bitmap7 = SoftwareBitmap.Copy(Bitmap1))
                {
                    OpenCV.OpenCVLibrary.OilPaintingEffect(Bitmap7, Bitmap7);
                    await Source7.SetBitmapAsync(Bitmap7);
                }
            }

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                FilterCollection.Add(new FilterItem(Source1, "原图", FilterType.Origin));
                FilterCollection.Add(new FilterItem(Source2, "反色", FilterType.Invert));
                FilterCollection.Add(new FilterItem(Source3, "灰度", FilterType.Gray));
                FilterCollection.Add(new FilterItem(Source4, "黑白", FilterType.Threshold));
                FilterCollection.Add(new FilterItem(Source8, "怀旧", FilterType.Sepia));
                FilterCollection.Add(new FilterItem(Source9, "马赛克", FilterType.Mosaic));
                FilterCollection.Add(new FilterItem(Source5, "素描", FilterType.Sketch));
                FilterCollection.Add(new FilterItem(Source6, "模糊", FilterType.GaussianBlur));
                FilterCollection.Add(new FilterItem(Source7, "油画", FilterType.OilPainting));
            }
            else
            {
                FilterCollection.Add(new FilterItem(Source1, "Origin", FilterType.Origin));
                FilterCollection.Add(new FilterItem(Source2, "Invert", FilterType.Invert));
                FilterCollection.Add(new FilterItem(Source3, "Gray", FilterType.Gray));
                FilterCollection.Add(new FilterItem(Source4, "Binary", FilterType.Threshold));
                FilterCollection.Add(new FilterItem(Source8, "Sepia", FilterType.Sepia));
                FilterCollection.Add(new FilterItem(Source9, "Mosaic", FilterType.Mosaic));
                FilterCollection.Add(new FilterItem(Source5, "Sketch", FilterType.Sketch));
                FilterCollection.Add(new FilterItem(Source6, "Blurry", FilterType.GaussianBlur));
                FilterCollection.Add(new FilterItem(Source7, "OilPainting", FilterType.OilPainting));
            }

            FilterGrid.SelectedIndex = 0;
            FilterGrid.SelectionChanged += FilterGrid_SelectionChanged;

            using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
            {
                OpenCV.OpenCVLibrary.CalculateHistogram(OriginImage, Histogram);
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
            FileControlNav.GoBack();
        }

        private async void SaveAs_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            FileSavePicker Picker;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                Picker = new FileSavePicker
                {
                    SuggestedFileName = Path.GetFileNameWithoutExtension(OriginFile.Name),
                    CommitButtonText = "保存",
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                Picker.FileTypeChoices.Add("PNG格式", new List<string>() { ".png" });
                Picker.FileTypeChoices.Add("JPEG格式", new List<string>() { ".jpg" });
                Picker.FileTypeChoices.Add("BMP格式", new List<string>() { ".bmp" });
                Picker.FileTypeChoices.Add("GIF格式", new List<string>() { ".gif" });
                Picker.FileTypeChoices.Add("TIFF格式", new List<string>() { ".tiff" });
            }
            else
            {
                Picker = new FileSavePicker
                {
                    SuggestedFileName = Path.GetFileNameWithoutExtension(OriginFile.Name),
                    CommitButtonText = "Save",
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                Picker.FileTypeChoices.Add("PNG format", new List<string>() { ".png" });
                Picker.FileTypeChoices.Add("JPEG format", new List<string>() { ".jpg" });
                Picker.FileTypeChoices.Add("BMP format", new List<string>() { ".bmp" });
                Picker.FileTypeChoices.Add("GIF format", new List<string>() { ".gif" });
                Picker.FileTypeChoices.Add("TIFF format", new List<string>() { ".tiff" });
            }

            StorageFile File = await Picker.PickSaveFileAsync();

            if (File != null)
            {
                LoadingControl.IsLoading = true;
                MainPage.ThisPage.IsAnyTaskRunning = true;


                using (var Stream = await File.OpenAsync(FileAccessMode.ReadWrite))
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
                MainPage.ThisPage.IsAnyTaskRunning = false;
                FileControlNav.GoBack();
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

            using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
            {
                OpenCV.OpenCVLibrary.CalculateHistogram(OriginImage, Histogram);
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
            MainPage.ThisPage.IsAnyTaskRunning = true;

            using (var Stream = await OriginFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                Stream.Size = 0;
                switch (OriginFile.FileType)
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
                        await OriginFile.RenameAsync(Path.GetFileNameWithoutExtension(OriginFile.Name) + ".png", NameCollisionOption.GenerateUniqueName);
                        break;
                }
            }

            await Task.Delay(1000).ConfigureAwait(true);

            LoadingControl.IsLoading = false;
            MainPage.ThisPage.IsAnyTaskRunning = false;

            FileControlNav.GoBack();
        }

        private void RotationButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterImage == null)
            {
                SoftwareBitmap RotatedImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelHeight, OriginImage.PixelWidth, BitmapAlphaMode.Premultiplied);
                OpenCV.OpenCVLibrary.RotateEffect(OriginImage, RotatedImage, 90);
                WriteableBitmap WBitmap = new WriteableBitmap(RotatedImage.PixelWidth, RotatedImage.PixelHeight);
                RotatedImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;

                OriginImage.Dispose();
                OriginImage = RotatedImage;
            }
            else
            {
                SoftwareBitmap OringinRotatedImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelHeight, OriginImage.PixelWidth, BitmapAlphaMode.Premultiplied);
                OpenCV.OpenCVLibrary.RotateEffect(OriginImage, OringinRotatedImage, 90);
                OriginImage.Dispose();
                OriginImage = OringinRotatedImage;

                SoftwareBitmap RotatedImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, FilterImage.PixelHeight, FilterImage.PixelWidth, BitmapAlphaMode.Premultiplied);
                OpenCV.OpenCVLibrary.RotateEffect(FilterImage, RotatedImage, 90);
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
                OpenCV.OpenCVLibrary.FlipEffect(OriginImage, OriginImage, false);
                WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                Cropper.Source = WBitmap;
            }
            else
            {
                OpenCV.OpenCVLibrary.FlipEffect(OriginImage, OriginImage, false);

                OpenCV.OpenCVLibrary.FlipEffect(FilterImage, FilterImage, false);
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
                    OpenCV.OpenCVLibrary.AdjustBrightnessContrast(OriginBackupImage, OriginImage, e.NewValue, BetaSlider.Value);
                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                    {
                        OpenCV.OpenCVLibrary.CalculateHistogram(OriginImage, Histogram);
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
                else
                {
                    OpenCV.OpenCVLibrary.AdjustBrightnessContrast(FilterBackupImage, FilterImage, e.NewValue, BetaSlider.Value);
                    WriteableBitmap WBitmap = new WriteableBitmap(FilterImage.PixelWidth, FilterImage.PixelHeight);
                    FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                    {
                        OpenCV.OpenCVLibrary.CalculateHistogram(FilterImage, Histogram);
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
                    OpenCV.OpenCVLibrary.AdjustBrightnessContrast(OriginBackupImage, OriginImage, AlphaSlider.Value, e.NewValue);
                    WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                    OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                    {
                        OpenCV.OpenCVLibrary.CalculateHistogram(OriginImage, Histogram);
                        WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                        Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                        HistogramImage.Source = HBitmap;
                    }
                }
                else
                {
                    OpenCV.OpenCVLibrary.AdjustBrightnessContrast(FilterBackupImage, FilterImage, AlphaSlider.Value, e.NewValue);
                    WriteableBitmap WBitmap = new WriteableBitmap(FilterImage.PixelWidth, FilterImage.PixelHeight);
                    FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                    Cropper.Source = WBitmap;

                    using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                    {
                        OpenCV.OpenCVLibrary.CalculateHistogram(FilterImage, Histogram);
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

            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);

            OpenCV.OpenCVLibrary.AutoColorLevel(OriginImage, FilterImage);
            OpenCV.OpenCVLibrary.AutoWhiteBalance(FilterImage, FilterImage);

            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
            Cropper.Source = WBitmap;

            using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
            {
                OpenCV.OpenCVLibrary.CalculateHistogram(FilterImage, Histogram);
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

                            using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                            {
                                OpenCV.OpenCVLibrary.CalculateHistogram(OriginImage, Histogram);
                                WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                                Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                                HistogramImage.Source = HBitmap;
                            }

                            ResetButton.IsEnabled = true;
                            return;
                        }
                    case FilterType.Invert:
                        {
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);

                            var InvertBitmap = WBitmap.Invert();

                            FilterImage = SoftwareBitmap.CreateCopyFromBuffer(InvertBitmap.PixelBuffer, BitmapPixelFormat.Bgra8, InvertBitmap.PixelWidth, InvertBitmap.PixelHeight);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            Cropper.Source = InvertBitmap;

                            break;
                        }
                    case FilterType.Gray:
                        {
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);

                            var InvertBitmap = WBitmap.Gray();

                            FilterImage = SoftwareBitmap.CreateCopyFromBuffer(InvertBitmap.PixelBuffer, BitmapPixelFormat.Bgra8, InvertBitmap.PixelWidth, InvertBitmap.PixelHeight);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            Cropper.Source = InvertBitmap;

                            break;
                        }
                    case FilterType.Threshold:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                            FilterImage.CopyFromBuffer(WBitmap.Gray().PixelBuffer);
                            OpenCV.OpenCVLibrary.ThresholdEffect(FilterImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                    case FilterType.Sketch:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            OriginImage.CopyToBuffer(WBitmap.PixelBuffer);
                            FilterImage.CopyFromBuffer(WBitmap.Gray().PixelBuffer);
                            OpenCV.OpenCVLibrary.SketchEffect(FilterImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                    case FilterType.GaussianBlur:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.GaussianBlurEffect(OriginImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                    case FilterType.Sepia:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.SepiaEffect(OriginImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                    case FilterType.OilPainting:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.OilPaintingEffect(OriginImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                    case FilterType.Mosaic:
                        {
                            FilterImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, OriginImage.PixelWidth, OriginImage.PixelHeight, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.MosaicEffect(OriginImage, FilterImage);
                            FilterBackupImage = SoftwareBitmap.Copy(FilterImage);
                            WriteableBitmap WBitmap = new WriteableBitmap(OriginImage.PixelWidth, OriginImage.PixelHeight);
                            FilterImage.CopyToBuffer(WBitmap.PixelBuffer);
                            Cropper.Source = WBitmap;

                            break;
                        }
                }

                using (SoftwareBitmap Histogram = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 512, 512, BitmapAlphaMode.Premultiplied))
                {
                    OpenCV.OpenCVLibrary.CalculateHistogram(FilterImage, Histogram);
                    WriteableBitmap HBitmap = new WriteableBitmap(Histogram.PixelWidth, Histogram.PixelHeight);
                    Histogram.CopyToBuffer(HBitmap.PixelBuffer);
                    HistogramImage.Source = HBitmap;
                }

                ResetButton.IsEnabled = true;
            }
        }
    }
}
