using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class ResetDialog : QueueContentDialog
    {
        public StorageFolder ExportFolder { get; private set; }

        public bool IsClearSecureFolder { get; private set; }

        public ResetDialog()
        {
            InitializeComponent();
            ClearSecure.IsChecked = true;
            Loading += ResetDialog_Loading;
        }

        private async void ResetDialog_Loading(FrameworkElement sender, object args)
        {
            StorageFolder SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);

            QueryOptions Options = new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer
            };

            StorageItemQueryResult ItemQuery = SecureFolder.CreateItemQueryWithOptions(Options);

            uint Count = await ItemQuery.GetItemCountAsync();

            if (Count == 0)
            {
                ClearSecure.IsEnabled = false;
            }

            ClearSecure.Content += $"({Globalization.GetString("Reset_Dialog_TotalFile")}: {Count})";
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsClearSecureFolder = ClearSecure.IsChecked.GetValueOrDefault();

            if (!IsClearSecureFolder && ExportFolder == null)
            {
                ChoosePositionTip.IsOpen = true;
                args.Cancel = true;
            }
        }

        private void ClearSecure_Checked(object sender, RoutedEventArgs e)
        {
            ExportLocation.Visibility = Visibility.Collapsed;
        }

        private void ClearSecure_Unchecked(object sender, RoutedEventArgs e)
        {
            ExportLocation.Visibility = Visibility.Visible;
        }

        private async void ExportLocation_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            ExportFolder = await Picker.PickSingleFolderAsync();
        }
    }
}
