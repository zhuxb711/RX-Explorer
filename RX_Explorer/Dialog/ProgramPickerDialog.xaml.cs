using RX_Explorer.Class;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly ObservableCollection<ProgramPickerItem> DefaultProgramCollection = new ObservableCollection<ProgramPickerItem>();
        private readonly ObservableCollection<ProgramPickerItem> OtherProgramCollection = new ObservableCollection<ProgramPickerItem>();
        private readonly FileSystemStorageFile OpenFile;
        private readonly List<ProgramPickerItem> RecommandList = new List<ProgramPickerItem>();
        private readonly List<ProgramPickerItem> NotRecommandList = new List<ProgramPickerItem>();
        private readonly bool OpenedByPropertyWindow;

        public ProgramPickerItem UserPickedItem { get; private set; }

        public ProgramPickerDialog(FileSystemStorageFile OpenFile, bool OpenFromPropertiesWindow = false)
        {
            InitializeComponent();

            this.OpenFile = OpenFile ?? throw new ArgumentNullException(nameof(OpenFile), "Parameter could not be null");
            this.OpenedByPropertyWindow = OpenFromPropertiesWindow;

            if (OpenFromPropertiesWindow)
            {
                UseAsAdmin.Visibility = Visibility.Collapsed;
            }

            Loaded += ProgramPickerDialog_Loaded;
        }

        private async void ProgramPickerDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                List<ProgramPickerItem> LocalNotRecommandList = new List<ProgramPickerItem>();
                List<ProgramPickerItem> LocalRecommandList = new List<ProgramPickerItem>();

                List<AssociationPackage> AssociationList = new List<AssociationPackage>();

                string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(OpenFile.Type);

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    if (string.IsNullOrEmpty(AdminExecutablePath))
                    {
                        AdminExecutablePath = await Exclusive.Controller.GetDefaultAssociationFromPathAsync(OpenFile.Path);
                    }

                    AssociationList.AddRange(await Exclusive.Controller.GetAssociationFromPathAsync(OpenFile.Path));
                }

                SQLite.Current.UpdateProgramPickerRecord(AssociationList);

                foreach (AppInfo App in await Launcher.FindFileHandlersAsync(OpenFile.Type.ToLower()))
                {
                    LocalRecommandList.Add(await ProgramPickerItem.CreateAsync(App));
                }

                foreach (AssociationPackage Package in SQLite.Current.GetProgramPickerRecord(OpenFile.Type))
                {
                    try
                    {
                        if (Path.IsPathRooted(Package.ExecutablePath))
                        {
                            if (await FileSystemStorageItemBase.OpenAsync(Package.ExecutablePath) is FileSystemStorageFile File)
                            {
                                ProgramPickerItem Item = await ProgramPickerItem.CreateAsync(File);

                                if (Package.IsRecommanded)
                                {
                                    LocalRecommandList.Add(Item);
                                }
                                else
                                {
                                    LocalNotRecommandList.Add(Item);
                                }
                            }
                            else
                            {
                                SQLite.Current.DeleteProgramPickerRecord(Package);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when adding AssociationPackage to the list");
                    }
                }

                if (!string.IsNullOrEmpty(AdminExecutablePath))
                {
                    if (LocalRecommandList.Concat(LocalNotRecommandList).FirstOrDefault((Item) => Item.Path.Equals(AdminExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item)
                    {
                        DefaultProgramCollection.Add(Item);
                        LocalRecommandList.Remove(Item);
                        LocalNotRecommandList.Remove(Item);
                    }
                }

                RecommandList.AddRange(LocalRecommandList.Distinct());
                NotRecommandList.AddRange(LocalNotRecommandList.Distinct());

                switch (OpenFile.Type.ToLower())
                {
                    case ".jpg":
                    case ".png":
                    case ".bmp":
                    case ".heic":
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
                            if (DefaultProgramCollection.Count == 0)
                            {
                                DefaultProgramCollection.Add(ProgramPickerItem.InnerViewer);
                            }
                            else
                            {
                                OtherProgramCollection.Add(ProgramPickerItem.InnerViewer);
                            }

                            break;
                        }
                }

                if (RecommandList.Count == 0)
                {
                    VisualStateManager.GoToState(RootControl, "RemoveMoreButtonState", true);
                    OtherProgramCollection.AddRange(NotRecommandList);
                }
                else
                {
                    OtherProgramCollection.AddRange(RecommandList);
                }

                if (DefaultProgramCollection.Count == 0)
                {
                    OtherProgramList.SelectedIndex = 0;
                    CurrentUseProgramList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CurrentUseProgramList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when fetching association app data");
            }
            finally
            {
                VisualStateManager.GoToState(RootControl, "LoadComplete", true);
            }
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
                        if (await LinkFile.GetRawDataAsync() is LinkFileData Package && !string.IsNullOrEmpty(Package.LinkTargetPath))
                        {
                            ExecutablePath = Package.LinkTargetPath;
                        }
                    }
                }

                SQLite.Current.UpdateProgramPickerRecord(new AssociationPackage[] { new AssociationPackage(OpenFile.Type, ExecutablePath, true) });

                if (DefaultProgramCollection.FirstOrDefault((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item1)
                {
                    CurrentUseProgramList.SelectedItem = Item1;
                }
                else if (OtherProgramCollection.FirstOrDefault((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item2)
                {
                    OtherProgramList.SelectedItem = Item2;
                    OtherProgramList.ScrollIntoView(Item2);
                }
                else if (NotRecommandList.FirstOrDefault((Item) => Item.Path.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase)) is ProgramPickerItem Item3)
                {
                    if (ShowMore.Visibility == Visibility.Visible)
                    {
                        VisualStateManager.GoToState(RootControl, "RemoveMoreButtonState", true);
                        OtherProgramCollection.AddRange(NotRecommandList);
                    }

                    OtherProgramList.SelectedItem = Item3;
                    OtherProgramList.ScrollIntoView(Item3);
                }
                else if (await FileSystemStorageItemBase.OpenAsync(ExecutablePath) is FileSystemStorageFile File)
                {
                    ProgramPickerItem NewItem = await ProgramPickerItem.CreateAsync(File);

                    OtherProgramCollection.Add(NewItem);
                    OtherProgramList.SelectedItem = NewItem;
                    OtherProgramList.ScrollIntoView(NewItem);
                }
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                if (CurrentUseProgramList.SelectedItem is ProgramPickerItem CurrentItem)
                {
                    UserPickedItem = CurrentItem;
                }
                else if (OtherProgramList.SelectedItem is ProgramPickerItem OtherItem)
                {
                    UserPickedItem = OtherItem;
                }
                else
                {
                    args.Cancel = true;
                }

                if ((UserPickedItem != null && UseAsAdmin.IsChecked.GetValueOrDefault()) || OpenedByPropertyWindow)
                {
                    string ExecutablePath = UserPickedItem.Path;

                    if (Path.IsPathRooted(ExecutablePath) && Path.GetExtension(ExecutablePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(ExecutablePath) is LinkStorageFile LinkFile)
                        {
                            if (await LinkFile.GetRawDataAsync() is LinkFileData Package && !string.IsNullOrEmpty(Package.LinkTargetPath))
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

        private void ShowMore_Click(object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(RootControl, "RemoveMoreButtonState", true);
            OtherProgramCollection.AddRange(NotRecommandList);
        }
    }
}
