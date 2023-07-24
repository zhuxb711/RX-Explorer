using ComputerVision;
using Microsoft.Toolkit.Uwp.UI.Controls;
using OpenCvSharp;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
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
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Rect = Windows.Foundation.Rect;

namespace RX_Explorer.View
{
    public sealed partial class PhotoEditor : Page
    {
        private SoftwareBitmap OriginBitmap;
        private SoftwareBitmap OriginBitmapBackup;
        private SoftwareBitmap FilterBitmap;
        private SoftwareBitmap FilterBitmapBackup;
        private FileSystemStorageFile OriginFile;
        private Rect UnchangedRegion;
        private readonly ObservableCollection<ImageFilterItem> FilterCollection = new ObservableCollection<ImageFilterItem>();

        public PhotoEditor()
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
                    UnchangedRegion = Cropper.CroppedRegion;

                    using (Stream FileStream = await OriginFile.GetStreamFromFileAsync(AccessMode.Read))
                    {
                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FileStream.AsRandomAccessStream());
                        OriginBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                        OriginBitmapBackup = SoftwareBitmap.Copy(OriginBitmap);
                        Cropper.Source = OriginBitmap.ToWriteableBitmap();
                    }

                    await AddEffectsToPaneAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when initializing CropperPage");
            }
        }

        private async Task AddEffectsToPaneAsync()
        {
            using (SoftwareBitmap ResizedBitmap = ComputerVisionProvider.GenenateResizedThumbnail(OriginBitmap, 100, 100))
            {
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_1"), FilterType.Origin));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_2"), FilterType.Invert));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_3"), FilterType.Gray));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_4"), FilterType.Threshold));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_5"), FilterType.Sepia));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_6"), FilterType.Mosaic));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_7"), FilterType.Sketch));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_8"), FilterType.GaussianBlur));
                FilterCollection.Add(await ImageFilterItem.CreateAsync(ResizedBitmap, Globalization.GetString("CropperPage_Filter_Type_9"), FilterType.OilPainting));
            }

            FilterGrid.SelectedIndex = 0;
            FilterGrid.SelectionChanged += FilterGrid_SelectionChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Cropper.Source = null;
            AspList.SelectedIndex = 0;
            FilterBitmap?.Dispose();
            OriginBitmapBackup?.Dispose();
            OriginBitmap?.Dispose();
            FilterBitmapBackup?.Dispose();
            FilterBitmapBackup = null;
            FilterBitmap = null;
            OriginBitmapBackup = null;
            OriginBitmap = null;
            OriginFile = null;
            FilterGrid.SelectionChanged -= FilterGrid_SelectionChanged;
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

                    await Task.WhenAll(SaveToFileAsync(new FileSystemStorageFile(await File.GetNativeFileDataAsync())), Task.Delay(1000));

                    LoadingControl.IsLoading = false;

                    if (Frame.CanGoBack)
                    {
                        Frame.GoBack();
                    }
                }
            }
            catch (NotSupportedException)
            {
                CommonContentDialog Dialog = new CommonContentDialog
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

                CommonContentDialog Dialog = new CommonContentDialog
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
            using (Stream FileStream = await File.GetStreamFromFileAsync(AccessMode.Exclusive))
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

            OriginBitmap?.Dispose();
            OriginBitmap = SoftwareBitmap.Copy(OriginBitmapBackup);
            Cropper.Source = OriginBitmap.ToWriteableBitmap();

            FilterBitmap?.Dispose();
            FilterBitmap = null;
            FilterBitmapBackup?.Dispose();
            FilterBitmapBackup = null;

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

        private void Cropper_PointerReleased(object sender, PointerRoutedEventArgs e)
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
                CommonContentDialog Dialog = new CommonContentDialog
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

                CommonContentDialog Dialog = new CommonContentDialog
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
            if (FilterBitmap == null)
            {
                SoftwareBitmap RotatedImage = ComputerVisionProvider.RotateEffect(OriginBitmap, RotateFlags.Rotate90Clockwise);
                Cropper.Source = RotatedImage.ToWriteableBitmap();

                OriginBitmap?.Dispose();
                OriginBitmap = RotatedImage;
            }
            else
            {
                SoftwareBitmap OringinRotatedImage = ComputerVisionProvider.RotateEffect(OriginBitmap, RotateFlags.Rotate90Clockwise);
                OriginBitmap?.Dispose();
                OriginBitmap = OringinRotatedImage;

                SoftwareBitmap RotatedImage = ComputerVisionProvider.RotateEffect(FilterBitmap, RotateFlags.Rotate90Clockwise);
                Cropper.Source = RotatedImage.ToWriteableBitmap();

                FilterBitmap.Dispose();
                FilterBitmap = RotatedImage;
            }

            ResetButton.IsEnabled = true;
        }

        private void FlipButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterBitmap == null)
            {
                SoftwareBitmap FlipImage = ComputerVisionProvider.FlipEffect(OriginBitmap, FlipMode.Y);
                OriginBitmap?.Dispose();
                OriginBitmap = FlipImage;
                Cropper.Source = OriginBitmap.ToWriteableBitmap();
            }
            else
            {
                SoftwareBitmap FlipImage = ComputerVisionProvider.FlipEffect(OriginBitmap, FlipMode.Y);
                OriginBitmap?.Dispose();
                OriginBitmap = FlipImage;

                SoftwareBitmap FilterFlipImage = ComputerVisionProvider.FlipEffect(FilterBitmap, FlipMode.Y);
                FilterBitmap.Dispose();
                FilterBitmap = FilterFlipImage;
                Cropper.Source = FilterBitmap.ToWriteableBitmap();
            }

            ResetButton.IsEnabled = true;
        }

        private void AlphaSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OriginBitmap != null)
            {
                ResetButton.IsEnabled = true;
                if (FilterBitmap == null)
                {
                    OriginBitmap?.Dispose();
                    OriginBitmap = ComputerVisionProvider.AdjustBrightnessContrast(OriginBitmapBackup, e.NewValue, BetaSlider.Value);
                    Cropper.Source = OriginBitmap.ToWriteableBitmap();
                }
                else
                {
                    FilterBitmap.Dispose();
                    FilterBitmap = ComputerVisionProvider.AdjustBrightnessContrast(FilterBitmapBackup, e.NewValue, BetaSlider.Value);
                    Cropper.Source = FilterBitmap.ToWriteableBitmap();
                }
            }
        }

        private void BetaSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (OriginBitmap != null)
            {
                ResetButton.IsEnabled = true;
                if (FilterBitmap == null)
                {
                    OriginBitmap?.Dispose();
                    OriginBitmap = ComputerVisionProvider.AdjustBrightnessContrast(OriginBitmapBackup, AlphaSlider.Value, e.NewValue);
                    Cropper.Source = OriginBitmap.ToWriteableBitmap();
                }
                else
                {
                    FilterBitmap.Dispose();
                    FilterBitmap = ComputerVisionProvider.AdjustBrightnessContrast(FilterBitmapBackup, AlphaSlider.Value, e.NewValue);
                    Cropper.Source = FilterBitmap.ToWriteableBitmap();
                }
            }
        }

        private void AutoOptimizeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (FilterBitmap != null)
            {
                FilterBitmap.Dispose();
                FilterBitmap = null;
                FilterBitmapBackup.Dispose();
                FilterBitmapBackup = null;
            }

            AlphaSlider.ValueChanged -= AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged -= BetaSlider_ValueChanged;
            AlphaSlider.Value = 1;
            BetaSlider.Value = 0;
            AlphaSlider.ValueChanged += AlphaSlider_ValueChanged;
            BetaSlider.ValueChanged += BetaSlider_ValueChanged;

            FilterBitmap = ComputerVisionProvider.AutoColorEnhancement(OriginBitmap);
            FilterBitmapBackup = SoftwareBitmap.Copy(FilterBitmap);
            Cropper.Source = FilterBitmap.ToWriteableBitmap();

            ResetButton.IsEnabled = true;
        }

        private void FilterGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilterGrid.SelectedItem is ImageFilterItem Item)
            {
                if (FilterBitmap != null)
                {
                    FilterBitmap.Dispose();
                    FilterBitmap = null;
                    FilterBitmapBackup.Dispose();
                    FilterBitmapBackup = null;
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
                            Cropper.Source = OriginBitmap.ToWriteableBitmap();
                            break;
                        }
                    case FilterType.Invert:
                        {
                            using (SoftwareBitmap InvertEffectBitmap = ComputerVisionProvider.InvertEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(InvertEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(InvertEffectBitmap);
                                Cropper.Source = InvertEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.Gray:
                        {
                            using (SoftwareBitmap GrayEffectBitmap = ComputerVisionProvider.GrayEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(GrayEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(GrayEffectBitmap);
                                Cropper.Source = GrayEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.Threshold:
                        {
                            using (SoftwareBitmap ThresholdEffectBitmap = ComputerVisionProvider.ThresholdEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(ThresholdEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(ThresholdEffectBitmap);
                                Cropper.Source = ThresholdEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.Sketch:
                        {
                            using (SoftwareBitmap SketchEffectBitmap = ComputerVisionProvider.SketchEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(SketchEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(SketchEffectBitmap);
                                Cropper.Source = SketchEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.GaussianBlur:
                        {
                            using (SoftwareBitmap GaussianBlurEffectBitmap = ComputerVisionProvider.GaussianBlurEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(GaussianBlurEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(GaussianBlurEffectBitmap);
                                Cropper.Source = GaussianBlurEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.Sepia:
                        {
                            using (SoftwareBitmap SepiaEffectBitmap = ComputerVisionProvider.SepiaEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(SepiaEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(SepiaEffectBitmap);
                                Cropper.Source = SepiaEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.OilPainting:
                        {
                            using (SoftwareBitmap OilPaintingEffectBitmap = ComputerVisionProvider.OilPaintingEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(OilPaintingEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(OilPaintingEffectBitmap);
                                Cropper.Source = OilPaintingEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                    case FilterType.Mosaic:
                        {
                            using (SoftwareBitmap MosaicEffectBitmap = ComputerVisionProvider.MosaicEffect(OriginBitmap))
                            {
                                FilterBitmap = SoftwareBitmap.Copy(MosaicEffectBitmap);
                                FilterBitmapBackup = SoftwareBitmap.Copy(MosaicEffectBitmap);
                                Cropper.Source = MosaicEffectBitmap.ToWriteableBitmap();
                            }
                            break;
                        }
                }

                ResetButton.IsEnabled = true;
            }
        }
    }
}
