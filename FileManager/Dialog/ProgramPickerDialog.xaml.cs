using FileManager.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager.Dialog
{
    public sealed partial class ProgramPickerDialog : QueueContentDialog
    {
        private ObservableCollection<ProgramPickerItem> ProgramCollection = new ObservableCollection<ProgramPickerItem>();

        private StorageFile OpenFile;

        public bool ContinueUseInnerViewer { get; private set; } = false;

        public bool OpenFailed { get; private set; } = false;

        public ProgramPickerDialog(StorageFile OpenFile)
        {
            if (OpenFile == null)
            {
                throw new ArgumentNullException(nameof(OpenFile), "Parameter could not be null");
            }

            InitializeComponent();
            this.OpenFile = OpenFile;

            switch (OpenFile.FileType)
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

                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            Title1.Text = "继续使用以下应用";
                            Title2.Text = "选择其他应用";
                        }
                        else
                        {
                            Title1.Text = "Continue using";
                            Title2.Text = "Choose another application";
                        }
                        break;
                    }
                default:
                    {
                        Area1.Visibility = Visibility.Collapsed;
                        CurrentUseProgramList.Visibility = Visibility.Collapsed;
                        Title2.Text = Globalization.Language == LanguageEnum.Chinese ? "选择其他应用" : "Choose another application";
                        break;
                    }
            }

            Loading += ProgramPickerDialog_Loading;
        }

        private async void ProgramPickerDialog_Loading(FrameworkElement sender, object args)
        {
            List<ProgramPickerItem> TempList = new List<ProgramPickerItem>();

            foreach (AppInfo Info in await Launcher.FindFileHandlersAsync(OpenFile.FileType))
            {
                using (IRandomAccessStreamWithContentType LogoStream = await Info.DisplayInfo.GetLogo(new Windows.Foundation.Size(100, 100)).OpenReadAsync())
                {
                    BitmapImage Image = new BitmapImage();
                    await Image.SetSourceAsync(LogoStream);
                    TempList.Add(new ProgramPickerItem(Image, Info.DisplayInfo.DisplayName, Info.DisplayInfo.Description, Info.PackageFamilyName));
                }
            }

            List<string> PickerRecord = await SQLite.Current.GetProgramPickerRecordAsync().ConfigureAwait(true);
            foreach (var Path in PickerRecord)
            {
                try
                {
                    StorageFile ExcuteFile = await StorageFile.GetFileFromPathAsync(Path);
                    string ExtraAppName = (await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"].ToString();
                    TempList.Add(new ProgramPickerItem(await ExcuteFile.GetThumbnailBitmapAsync().ConfigureAwait(true), ExtraAppName, string.Empty, Path: ExcuteFile.Path));
                }
                catch (Exception)
                {
                    await SQLite.Current.DeleteProgramPickerRecordAsync(Path).ConfigureAwait(true);
                }
            }

            if (Area1.Visibility == Visibility.Visible)
            {
                string AdminExcuteProgram = null;
                if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                {
                    string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == OpenFile.FileType);
                    if (!string.IsNullOrEmpty(SaveUnit))
                    {
                        AdminExcuteProgram = SaveUnit.Split('|')[1];
                    }
                }

                if (!string.IsNullOrEmpty(AdminExcuteProgram))
                {
                    ProgramPickerItem AdminItem = TempList.FirstOrDefault((Item) => Item.Name == AdminExcuteProgram);
                    if (AdminItem != null)
                    {
                        CurrentUseProgramList.Items.Add(AdminItem);
                        CurrentUseProgramList.SelectedIndex = 0;
                        TempList.Remove(AdminItem);
                    }
                }

                if (CurrentUseProgramList.Items.Count == 0)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        CurrentUseProgramList.Items.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/Viewer.png")), "RX内置查看器", "RX提供基本的查看功能", Package.Current.Id.FamilyName));
                    }
                    else
                    {
                        CurrentUseProgramList.Items.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/Viewer.png")), "RX built-in viewer", "RX provides basic viewing functions", Package.Current.Id.FamilyName));
                    }
                    CurrentUseProgramList.SelectedIndex = 0;
                }
                else
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        ProgramCollection.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/Viewer.png")), "RX内置查看器", "RX提供基本的查看功能", Package.Current.Id.FamilyName));
                    }
                    else
                    {
                        ProgramCollection.Add(new ProgramPickerItem(new BitmapImage(new Uri("ms-appx:///Assets/Viewer.png")), "RX built-in viewer", "RX provides basic viewing functions", Package.Current.Id.FamilyName));
                    }
                }
            }

            foreach (var Item in TempList)
            {
                ProgramCollection.Add(Item);
            }

            if (CurrentUseProgramList.SelectedIndex == -1)
            {
                OtherProgramList.SelectedIndex = 0;
            }
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

        private async void BowserApp_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };

            Picker.FileTypeFilter.Add("*");

            if ((await Picker.PickSingleFileAsync()) is StorageFile ExtraApp)
            {
                string ExtraAppName;

                var PropertiesDictionary = await ExtraApp.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" });
                if (PropertiesDictionary.ContainsKey("System.FileDescription"))
                {
                    ExtraAppName = Convert.ToString(PropertiesDictionary["System.FileDescription"]);
                    if (string.IsNullOrWhiteSpace(ExtraAppName))
                    {
                        ExtraAppName = ExtraApp.Name;
                    }
                }
                else
                {
                    ExtraAppName = ExtraApp.Name;
                }

                ProgramCollection.Insert(0, new ProgramPickerItem(await ExtraApp.GetThumbnailBitmapAsync().ConfigureAwait(true), ExtraAppName, string.Empty, Path: ExtraApp.Path));
                OtherProgramList.SelectedIndex = 0;

                await SQLite.Current.SetProgramPickerRecordAsync(ExtraApp.Path).ConfigureAwait(false);
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            if (CurrentUseProgramList.SelectedItem is ProgramPickerItem CurrentItem)
            {
                if (UseAsAdmin.IsChecked.GetValueOrDefault())
                {
                    if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                    {
                        string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == OpenFile.FileType);
                        if (string.IsNullOrEmpty(SaveUnit))
                        {
                            ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute + $"{OpenFile.FileType}|{CurrentItem.Name};";
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace(SaveUnit, $"{OpenFile.FileType}|{CurrentItem.Name}");
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = $"{OpenFile.FileType}|{CurrentItem.Name};";
                    }
                }

                if (CurrentItem.PackageName == Package.Current.Id.FamilyName)
                {
                    ContinueUseInnerViewer = true;
                }
                else
                {
                    if (CurrentItem.IsCustomApp)
                    {
                        await FullTrustExcutorController.Run(CurrentItem.Path, OpenFile.Path).ConfigureAwait(true);
                    }
                    else
                    {
                        if (!await Launcher.LaunchFileAsync(OpenFile, new LauncherOptions { TargetApplicationPackageFamilyName = CurrentItem.PackageName, DisplayApplicationPicker = false }))
                        {
                            OpenFailed = true;
                            if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                            {
                                ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace($"{OpenFile.FileType}|{CurrentItem.Name};", string.Empty);
                            }
                        }
                    }
                }
            }
            else if (OtherProgramList.SelectedItem is ProgramPickerItem OtherItem)
            {
                if (UseAsAdmin.IsChecked.GetValueOrDefault())
                {
                    if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                    {
                        string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == OpenFile.FileType);
                        if (string.IsNullOrEmpty(SaveUnit))
                        {
                            ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute + $"{OpenFile.FileType}|{OtherItem.Name};";
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace(SaveUnit, $"{OpenFile.FileType}|{OtherItem.Name}");
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = $"{OpenFile.FileType}|{OtherItem.Name};";
                    }
                }

                if (OtherItem.PackageName == Package.Current.Id.FamilyName)
                {
                    ContinueUseInnerViewer = true;
                }
                else
                {
                    if (OtherItem.IsCustomApp)
                    {
                        await FullTrustExcutorController.Run(OtherItem.Path, OpenFile.Path).ConfigureAwait(true);
                    }
                    else
                    {
                        if (!await Launcher.LaunchFileAsync(OpenFile, new LauncherOptions { TargetApplicationPackageFamilyName = OtherItem.PackageName, DisplayApplicationPicker = false}))
                        {
                            OpenFailed = true;
                            if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                            {
                                ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace($"{OpenFile.FileType}|{OtherItem.Name};", string.Empty);
                            }
                        }
                    }
                }
            }

            Deferral.Complete();
        }
    }
}
