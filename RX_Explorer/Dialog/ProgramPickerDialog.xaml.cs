using RX_Explorer.Class;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class ProgramPickerDialog : QueueContentDialog
    {
        private readonly ObservableCollection<ProgramPickerItem> ProgramCollection = new ObservableCollection<ProgramPickerItem>();

        private readonly FileSystemStorageFile OpenFile;
        private readonly List<FileSystemStorageFile> NotRecommandList = new List<FileSystemStorageFile>();

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

            Dictionary<string, Task<ProgramPickerItem>> RecommandLoadTaskList = new Dictionary<string, Task<ProgramPickerItem>>();

            try
            {
                string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(OpenFile.Type);

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    if (string.IsNullOrEmpty(AdminExecutablePath))
                    {
                        AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(OpenFile.Path);
                    }

                    IReadOnlyList<AssociationPackage> SystemAssocAppList = await Exclusive.Controller.GetAssociationFromPathAsync(OpenFile.Path);
                    IReadOnlyList<AppInfo> UWPAssocAppList = await Launcher.FindFileHandlersAsync(OpenFile.Type);

                    foreach (AppInfo App in UWPAssocAppList)
                    {
                        RecommandLoadTaskList.Add(App.PackageFamilyName.ToLower(), ProgramPickerItem.CreateAsync(App));
                    }

                    SQLite.Current.UpdateProgramPickerRecord(OpenFile.Type, SystemAssocAppList.Concat(UWPAssocAppList.Select((Info) => new AssociationPackage(OpenFile.Type, Info.PackageFamilyName, true))).ToArray());
                }

                foreach (AssociationPackage Package in SQLite.Current.GetProgramPickerRecord(OpenFile.Type, false))
                {
                    try
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(Package.ExecutablePath) is FileSystemStorageFile File)
                        {
                            if (Package.IsRecommanded)
                            {
                                RecommandLoadTaskList.Add(File.Path.ToLower(), ProgramPickerItem.CreateAsync(File));
                            }
                            else
                            {
                                NotRecommandList.Add(File);
                            }
                        }
                        else
                        {
                            SQLite.Current.DeleteProgramPickerRecord(Package);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw trying add to ApplicationList");
                    }
                }

                if (!string.IsNullOrEmpty(AdminExecutablePath))
                {
                    if (RecommandLoadTaskList.TryGetValue(AdminExecutablePath.ToLower(), out Task<ProgramPickerItem> RecommandItemTask))
                    {
                        CurrentUseProgramList.Items.Add(await RecommandItemTask);
                        CurrentUseProgramList.SelectedIndex = 0;
                        RecommandLoadTaskList.Remove(AdminExecutablePath.ToLower());
                    }
                    else if (NotRecommandList.FirstOrDefault((Item) => Item.Path.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageFile NotRecommandFile)
                    {
                        CurrentUseProgramList.Items.Add(await ProgramPickerItem.CreateAsync(NotRecommandFile));
                        CurrentUseProgramList.SelectedIndex = 0;
                        NotRecommandList.Remove(NotRecommandFile);
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

                                CurrentUseProgramList.Items.Add(ProgramPickerItem.InnerViewer);
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
                                ProgramCollection.Add(ProgramPickerItem.InnerViewer);
                                break;
                            }
                    }
                }

                if (RecommandLoadTaskList.Count == 0)
                {
                    ShowMore.Visibility = Visibility.Collapsed;
                    OtherProgramList.MaxHeight = 300;

                    ProgramCollection.AddRange(await Task.WhenAll(NotRecommandList.Select((File) => ProgramPickerItem.CreateAsync(File))));
                }
                else
                {
                    ProgramCollection.AddRange(await Task.WhenAll(RecommandLoadTaskList.Values));
                }

                if (CurrentUseProgramList.SelectedIndex == -1)
                {
                    OtherProgramList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when fetching association app data");
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
                WholeArea.Visibility = Visibility.Visible;
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
                string ExecutablePath = ExecuteFile.Path;

                if (ExecuteFile.FileType.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    if (await FileSystemStorageItemBase.OpenAsync(ExecutablePath) is LinkStorageFile LinkFile)
                    {
                        if (await LinkFile.GetRawDataAsync() is LinkDataPackage Package && !string.IsNullOrEmpty(Package.LinkTargetPath))
                        {
                            ExecutablePath = Package.LinkTargetPath;
                        }
                    }
                }

                if (CurrentUseProgramList.Items.Cast<ProgramPickerItem>().Any((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    CurrentUseProgramList.SelectedIndex = 0;
                }
                else if (ProgramCollection.FirstOrDefault((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item)
                {
                    CurrentUseProgramList.SelectedItem = null;
                    OtherProgramList.SelectedItem = Item;
                    OtherProgramList.ScrollIntoViewSmoothly(Item);
                }
                else if (NotRecommandList.Any((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                {
                    if (ShowMore.Visibility == Visibility.Visible)
                    {
                        ShowMore.Visibility = Visibility.Collapsed;
                        OtherProgramList.MaxHeight = 300;

                        ProgramCollection.AddRange(await Task.WhenAll(NotRecommandList.Select((File) => ProgramPickerItem.CreateAsync(File))));
                    }

                    CurrentUseProgramList.SelectedItem = null;

                    if (ProgramCollection.FirstOrDefault((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item1)
                    {
                        OtherProgramList.SelectedItem = Item1;
                        OtherProgramList.ScrollIntoViewSmoothly(Item1);
                    }
                }
                else
                {
                    ProgramCollection.Add(await ProgramPickerItem.CreateAsync(ExecuteFile));
                    CurrentUseProgramList.SelectedItem = null;
                    OtherProgramList.SelectedItem = ProgramCollection.Last();
                    OtherProgramList.ScrollIntoViewSmoothly(ProgramCollection.Last());
                }

                SQLite.Current.SetProgramPickerRecord(new AssociationPackage(OpenFile.Type, ExecutablePath, true));
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
                    string ExecutablePath = SelectedProgram.Path;

                    if (Path.IsPathRooted(ExecutablePath) && Path.GetExtension(ExecutablePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(ExecutablePath) is LinkStorageFile LinkFile)
                        {
                            if (await LinkFile.GetRawDataAsync() is LinkDataPackage Package && !string.IsNullOrEmpty(Package.LinkTargetPath))
                            {
                                ExecutablePath = Package.LinkTargetPath;
                            }
                        }
                    }

                    SQLite.Current.SetDefaultProgramPickerRecord(OpenFile.Type, ExecutablePath);
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

        private async void ShowMore_Click(object sender, RoutedEventArgs e)
        {
            ShowMore.Visibility = Visibility.Collapsed;
            OtherProgramList.MaxHeight = 300;

            ProgramCollection.AddRange(await Task.WhenAll(NotRecommandList.Select((File) => ProgramPickerItem.CreateAsync(File))));
        }
    }
}
