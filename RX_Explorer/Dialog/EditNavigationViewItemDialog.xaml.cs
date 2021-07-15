using RX_Explorer.Class;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class EditNavigationViewItemDialog : QueueContentDialog
    {
        public EditNavigationViewItemDialog()
        {
            InitializeComponent();

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowRecycleBinItem"] is bool ShowRecycleBin)
            {
                RecycleBinItemCheckBox.IsChecked = ShowRecycleBin;
            }
            else
            {
                RecycleBinItemCheckBox.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowQuickStartItem"] is bool ShowQuickStart)
            {
                QuickStartItemCheckBox.IsChecked = ShowQuickStart;
            }
            else
            {
                QuickStartItemCheckBox.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowSecureAreaItem"] is bool ShowSecureArea)
            {
                SecureAreaItemCheckBox.IsChecked = ShowSecureArea;
            }
            else
            {
                SecureAreaItemCheckBox.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowBluetoothAudioItem"] is bool ShowBluetoothAudio)
            {
                BluetoothAudioItemCheckBox.IsChecked = ShowBluetoothAudio;
            }
            else
            {
                BluetoothAudioItemCheckBox.IsChecked = true;
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ApplicationData.Current.LocalSettings.Values["ShouldShowRecycleBinItem"] = RecycleBinItemCheckBox.IsChecked.GetValueOrDefault();
            ApplicationData.Current.LocalSettings.Values["ShouldShowQuickStartItem"] = QuickStartItemCheckBox.IsChecked.GetValueOrDefault();
            ApplicationData.Current.LocalSettings.Values["ShouldShowSecureAreaItem"] = SecureAreaItemCheckBox.IsChecked.GetValueOrDefault();
            ApplicationData.Current.LocalSettings.Values["ShouldShowBluetoothAudioItem"] = BluetoothAudioItemCheckBox.IsChecked.GetValueOrDefault();
        }
    }
}
