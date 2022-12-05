using RX_Explorer.Class;
using RX_Explorer.View;
using SharedLibrary;
using System;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaWelcomeDialog : QueueContentDialog
    {
        public string Password { get; private set; }

        public string StorageLocation { get; private set; }

        public bool IsEnableWindowsHello { get; private set; }

        public SLEKeySize EncryptionKeySize { get; private set; }

        public SecureAreaWelcomeDialog()
        {
            InitializeComponent();

            Location.Text = SettingPage.SecureAreaStorageLocation;
            StorageLocation = SettingPage.SecureAreaStorageLocation;

            SecureLevel.Items.Add($"AES-128bit ({Globalization.GetString("SecureArea_AES_128Level_Description")})");
            SecureLevel.Items.Add($"AES-256bit ({Globalization.GetString("SecureArea_AES_256Level_Description")})");
            SecureLevel.SelectedIndex = 0;

            Loading += SecureAreaWelcomeDialog_Loading;
        }

        private async void SecureAreaWelcomeDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            if (await WindowsHelloAuthenticator.CheckSupportAsync())
            {
                UseWinHel.IsEnabled = true;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                if (string.IsNullOrWhiteSpace(PrimaryPassword.Password))
                {
                    args.Cancel = true;
                    EmptyTip.Target = PrimaryPassword;
                    EmptyTip.IsOpen = true;
                }
                else if (string.IsNullOrWhiteSpace(ConfirmPassword.Password))
                {
                    args.Cancel = true;
                    EmptyTip.Target = ConfirmPassword;
                    EmptyTip.IsOpen = true;
                }
                else if (PrimaryPassword.Password != ConfirmPassword.Password)
                {
                    args.Cancel = true;
                    PasswordErrorTip.IsOpen = true;
                }
                else if (await FileSystemStorageItemBase.CreateNewAsync(Location.Text, CreateType.Folder, CollisionOptions.Skip) is FileSystemStorageFolder Folder)
                {
                    StorageLocation = Folder.Path;
                    Password = PrimaryPassword.Password;
                    IsEnableWindowsHello = UseWinHel.IsChecked.GetValueOrDefault();
                    EncryptionKeySize = SecureLevel.SelectedIndex == 0 ? SLEKeySize.AES128 : SLEKeySize.AES256;
                }
                else
                {
                    throw new IOException($"Could not specific {Location.Text} for Secure Area");
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                LogTracer.Log(ex, "Could not initialize the SecureArea");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void UseWinHel_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            switch (await WindowsHelloAuthenticator.RegisterUserAsync())
            {
                case AuthenticatorState.RegisterSuccess:
                    {
                        WindowsHelloPassed.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        WindowsHelloFailed.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        break;
                    }
                default:
                    {
                        WindowsHelloPassed.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        WindowsHelloFailed.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        UseWinHel.IsChecked = false;
                        break;
                    }
            }
        }

        private async void UseWinHel_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await WindowsHelloAuthenticator.DeleteUserAsync().ConfigureAwait(false);
        }

        private async void BrowserStorageLocation_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                Location.Text = Folder.Path;
                StorageLocation = Folder.Path;
            }
        }
    }
}
