using RX_Explorer.Class;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            List<Task<ProgramPickerItem>> RecommandLoadTaskList = new List<Task<ProgramPickerItem>>();
            List<Task<ProgramPickerItem>> NotRecommandLoadTaskList = new List<Task<ProgramPickerItem>>();

                                string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(OpenFile.Type);

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                try
                {
                    if (string.IsNullOrEmpty(AdminExecutablePath))
                    {
                        AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(OpenFile.Path);
                    }

                    IReadOnlyList<AssociationPackage> AssocList = await Exclusive.Controller.GetAssociationFromPathAsync(OpenFile.Path);
                    IReadOnlyList<AppInfo> AppInfoList = await Launcher.FindFileHandlersAsync(OpenFile.Type);

                    await SQLite.Current.UpdateProgramPickerRecordAsync(OpenFile.Type, AssocList.Concat(AppInfoList.Select((Info) => new AssociationPackage(OpenFile.Type, Info.PackageFamilyName, true))).ToArray());

                    foreach (AppInfo App in AppInfoList)
                    {
                        RecommandLoadTaskList.Add(ProgramPickerItem.CreateAsync(App));
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when fetching association data");
                }
            }

            foreach (AssociationPackage Package in await SQLite.Current.GetProgramPickerRecordAsync(OpenFile.Type, false))
            {
                try
                {
                    if (await FileSystemStorageItemBase.OpenAsync(Package.ExecutablePath) is FileSystemStorageFile File)
                    {
                        if (Package.IsRecommanded)
                        {
                            RecommandLoadTaskList.Add(ProgramPickerItem.CreateAsync(File));
                        }
                        else
                        {
                            NotRecommandLoadTaskList.Add(ProgramPickerItem.CreateAsync(File));
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

            List<ProgramPickerItem> RecommandList = new List<ProgramPickerItem>(await Task.WhenAll(RecommandLoadTaskList));
            List<ProgramPickerItem> NotRecommandList = new List<ProgramPickerItem>(await Task.WhenAll(NotRecommandLoadTaskList));

            CurrentUseProgramList.Tag = NotRecommandList;

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

            if (RecommandList.Count == 0)
            {
                ShowMore.Visibility = Visibility.Collapsed;
                OtherProgramList.MaxHeight = 300;

                ProgramCollection.AddRange(NotRecommandList);
            }
            else
            {
                ProgramCollection.AddRange(RecommandList);
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
                ProgramCollection.Insert(0, await ProgramPickerItem.CreateAsync(ExecuteFile));

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
            if (CurrentUseProgramList.Tag is List<ProgramPickerItem> NotRecommandList)
            {
                ShowMore.Visibility = Visibility.Collapsed;
                OtherProgramList.MaxHeight = 300;

                ProgramCollection.AddRange(NotRecommandList);
            }
        }
    }
}
