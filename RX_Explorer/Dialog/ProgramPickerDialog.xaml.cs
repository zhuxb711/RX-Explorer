using ComputerVision;
using RX_Explorer.Class;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Dialog
{
    public sealed partial class ProgramPickerDialog : QueueContentDialog
    {
        private readonly ObservableCollection<ProgramPickerItem> ProgramCollection = new ObservableCollection<ProgramPickerItem>();
        private readonly List<ProgramPickerItem> NotRecommandList = new List<ProgramPickerItem>();

        private readonly FileSystemStorageFile OpenFile;

        public ProgramPickerItem SelectedProgram { get; private set; }

        private bool OpenFromPropertiesWindow;

        public ProgramPickerDialog(FileSystemStorageFile OpenFile, bool OpenFromPropertiesWindow = false)
        {
            InitializeComponent();

            this.OpenFile = OpenFile ?? throw new ArgumentNullException(nameof(OpenFile), "Parameter could not be null");
            this.OpenFromPropertiesWindow = OpenFromPropertiesWindow;

            if (OpenFromPropertiesWindow)
            {
                UseAsAdmin.Visibility = Visibility.Collapsed;
            }

            Loading += ProgramPickerDialog_Loading;
        }

        private async void ProgramPickerDialog_Loading(FrameworkElement sender, object args)
        {
            LoadingText.Visibility = Visibility.Visible;
            WholeArea.Visibility = Visibility.Collapsed;

            List<ProgramPickerItem> RecommandList = new List<ProgramPickerItem>();

            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    IReadOnlyList<AssociationPackage> AssocList = await Exclusive.Controller.GetAssociationFromPathAsync(OpenFile.Path);
                    IReadOnlyList<AppInfo> AppInfoList = await Launcher.FindFileHandlersAsync(OpenFile.Type);

                    await SQLite.Current.UpdateProgramPickerRecordAsync(OpenFile.Type, AssocList.Concat(AppInfoList.Select((Info) => new AssociationPackage(OpenFile.Type, Info.PackageFamilyName, true))).ToArray());

                    foreach (AppInfo Info in AppInfoList)
                    {
                        RecommandList.Add(new ProgramPickerItem(async () => await Info.DisplayInfo.GetLogo(new Windows.Foundation.Size(128, 128)).OpenReadAsync(),
                            Info.DisplayInfo.DisplayName,
                            Info.DisplayInfo.Description,
                            Info.PackageFamilyName));
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when fetching association data");
            }

            foreach (AssociationPackage Package in await SQLite.Current.GetProgramPickerRecordAsync(OpenFile.Type, false))
            {
                try
                {
                    if (await FileSystemStorageItemBase.CheckExistAsync(Package.ExecutablePath))
                    {
                        StorageFile ExecuteFile = await StorageFile.GetFileFromPathAsync(Package.ExecutablePath);

                        IDictionary<string, object> PropertiesDictionary = await ExecuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                        string ExtraAppName = string.Empty;

                        if (PropertiesDictionary.TryGetValue("System.FileDescription", out object DescriptionRaw))
                        {
                            ExtraAppName = Convert.ToString(DescriptionRaw);
                        }

                        var programItem = new ProgramPickerItem(() => ExecuteFile.GetThumbnailRawStreamAsync(), string.IsNullOrEmpty(ExtraAppName) ? ExecuteFile.DisplayName : ExtraAppName, Globalization.GetString("Application_Admin_Name"), ExecuteFile.Path);
                        if (Package.IsRecommanded)
                        {
                            RecommandList.Add(programItem);
                        }
                        else
                        {
                            NotRecommandList.Add(programItem);
                        }
                    }
                    else
                    {
                        await SQLite.Current.DeleteProgramPickerRecordAsync(Package);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw trying add to ApplicationList");
                }
            }


            string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(OpenFile.Type);

            if (string.IsNullOrEmpty(AdminExecutablePath))
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(OpenFile.Path);
                }
            }

            if (!string.IsNullOrEmpty(AdminExecutablePath))
            {
                if (RecommandList.FirstOrDefault((Item) => Item.Path.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem RecommandItem)
                {
                    CurrentUseProgramList.Items.Add(RecommandItem);
                    CurrentUseProgramList.SelectedIndex = 0;
                    RecommandList.Remove(RecommandItem);
                }
                else if (NotRecommandList.FirstOrDefault((Item) => Item.Path.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem NotRecommandItem)
                {
                    CurrentUseProgramList.Items.Add(NotRecommandItem);
                    CurrentUseProgramList.SelectedIndex = 0;
                    NotRecommandList.Remove(NotRecommandItem);
                }
            }

            if (CurrentUseProgramList.Items.Count == 0)
            {
                switch (OpenFile.Type.ToLower())
                {
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                    case ".heic":
                    case ".gif":
                    case ".tiff":
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".alac":
                    case ".txt":
                    case ".pdf":
                    case ".exe":
                        {
                            Area1.Visibility = Visibility.Visible;
                            CurrentUseProgramList.Visibility = Visibility.Visible;

                            Title1.Text = Globalization.GetString("ProgramPicker_Dialog_Title_1");
                            Title2.Text = Globalization.GetString("ProgramPicker_Dialog_Title_2");

                            CurrentUseProgramList.Items.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/RX-icon.png")), Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer"), Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer_Description"), Package.Current.Id.FamilyName));
                            CurrentUseProgramList.SelectedIndex = 0;
                            break;
                        }
                    default:
                        {
                            Area1.Visibility = Visibility.Collapsed;
                            CurrentUseProgramList.Visibility = Visibility.Collapsed;
                            Title2.Text = Globalization.GetString("ProgramPicker_Dialog_Title_2");
                            break;
                        }
                }
            }
            else
            {
                Area1.Visibility = Visibility.Visible;
                CurrentUseProgramList.Visibility = Visibility.Visible;

                Title1.Text = Globalization.GetString("ProgramPicker_Dialog_Title_1");
                Title2.Text = Globalization.GetString("ProgramPicker_Dialog_Title_2");

                switch (OpenFile.Type.ToLower())
                {
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                    case ".heic":
                    case ".gif":
                    case ".tiff":
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".alac":
                    case ".txt":
                    case ".pdf":
                    case ".exe":
                        {
                            ProgramCollection.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/RX-icon.png")), Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer"), Globalization.GetString("ProgramPicker_Dialog_BuiltInViewer_Description"), Package.Current.Id.FamilyName));
                            break;
                        }
                }
            }

            if (RecommandList.Count == 0)
            {
                ShowMore.Visibility = Visibility.Collapsed;

                foreach (ProgramPickerItem Item in NotRecommandList)
                {
                    ProgramCollection.Add(Item);
                }

                OtherProgramList.MaxHeight = 300;
            }
            else
            {
                foreach (ProgramPickerItem Item in RecommandList)
                {
                    ProgramCollection.Add(Item);
                }
            }

            if (CurrentUseProgramList.SelectedIndex == -1)
            {
                OtherProgramList.SelectedIndex = 0;
            }

            LoadingText.Visibility = Visibility.Collapsed;
            WholeArea.Visibility = Visibility.Visible;
        }

        private void CurrentUseProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OtherProgramList.SelectionChanged -= OtherProgramList_SelectionChanged;
            OtherProgramList.SelectedIndex = -1;
            OtherProgramList.SelectionChanged += OtherProgramList_SelectionChanged;
        }

        private void OtherProgramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CurrentUseProgramList.SelectionChanged -= CurrentUseProgramList_SelectionChanged;
            CurrentUseProgramList.SelectedIndex = -1;
            CurrentUseProgramList.SelectionChanged += CurrentUseProgramList_SelectionChanged;
        }

        private async void BrowserApp_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };

            Picker.FileTypeFilter.Add(".exe");
            Picker.FileTypeFilter.Add(".lnk");

            if ((await Picker.PickSingleFileAsync()) is StorageFile ExecuteFile)
            {
                IDictionary<string, object> PropertiesDictionary = await ExecuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });

                string ExtraAppName = string.Empty;

                if (PropertiesDictionary.TryGetValue("System.FileDescription", out object Description))
                {
                    ExtraAppName = Convert.ToString(Description);
                }

                if (await ExecuteFile.GetThumbnailRawStreamAsync() is IRandomAccessStream ThumbnailStream)
                {
                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(ThumbnailStream);
                    using (SoftwareBitmap SBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                    using (SoftwareBitmap ResizeBitmap = ComputerVisionProvider.ResizeToActual(SBitmap))
                    using (InMemoryRandomAccessStream ResizeBitmapStream = new InMemoryRandomAccessStream())
                    {
                        BitmapEncoder Encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ResizeBitmapStream);
                        Encoder.SetSoftwareBitmap(ResizeBitmap);
                        await Encoder.FlushAsync();

                        BitmapImage ThumbnailBitmap = new BitmapImage();
                        await ThumbnailBitmap.SetSourceAsync(ResizeBitmapStream);

                        ProgramCollection.Insert(0, new ProgramPickerItem(ThumbnailBitmap, string.IsNullOrEmpty(ExtraAppName) ? ExecuteFile.DisplayName : ExtraAppName, Globalization.GetString("Application_Admin_Name"), ExecuteFile.Path));
                    }
                }
                else
                {
                    ProgramCollection.Insert(0, new ProgramPickerItem(new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? new Uri("ms-appx:///Assets/Page_Solid_White.png") : new Uri("ms-appx:///Assets/Page_Solid_Black.png")), string.IsNullOrEmpty(ExtraAppName) ? ExecuteFile.DisplayName : ExtraAppName, Globalization.GetString("Application_Admin_Name"), ExecuteFile.Path));
                }

                OtherProgramList.SelectedIndex = 0;

                await SQLite.Current.SetProgramPickerRecordAsync(new AssociationPackage(OpenFile.Type, ExecuteFile.Path, true)).ConfigureAwait(false);
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                if (CurrentUseProgramList.SelectedItem is ProgramPickerItem CurrentItem)
                {
                    SelectedProgram = CurrentItem;
                }
                else if (OtherProgramList.SelectedItem is ProgramPickerItem OtherItem)
                {
                    SelectedProgram = OtherItem;
                }
                else
                {
                    args.Cancel = true;
                }

                if (SelectedProgram != null && UseAsAdmin.IsChecked.GetValueOrDefault() || OpenFromPropertiesWindow)
                {
                    await SQLite.Current.SetDefaultProgramPickerRecordAsync(OpenFile.Type, SelectedProgram.Path);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ShowMore_Click(object sender, RoutedEventArgs e)
        {
            ShowMore.Visibility = Visibility.Collapsed;

            foreach (ProgramPickerItem Item in NotRecommandList)
            {
                ProgramCollection.Add(Item);
            }

            OtherProgramList.MaxHeight = 300;
        }
    }
}
