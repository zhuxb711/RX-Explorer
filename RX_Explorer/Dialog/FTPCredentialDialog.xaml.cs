using FluentFTP;
using RX_Explorer.Class;
using System;
using System.Linq;
using System.Threading;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class FTPCredentialDialog : QueueContentDialog
    {
        private readonly FtpPathAnalysis Analysis;
        private readonly CredentialProtector Protector;

        public FtpClientController FtpController { get; private set; }

        public FTPCredentialDialog(FtpPathAnalysis Analysis) : this()
        {
            this.Analysis = Analysis;

            Protector = new CredentialProtector("RX_FTP_Vault");
            FtpHost.Text = $"{Globalization.GetString("FTPCredentialDialogFTPHost")}: {Analysis.Host}";

            AccountBox.ItemsSource = Protector.GetAccountList();

            if (!string.IsNullOrEmpty(Analysis.UserName))
            {
                AccountBox.Text = Analysis.UserName;
            }

            if (!string.IsNullOrEmpty(Analysis.Password))
            {
                PasswordBox.Password = Analysis.Password;
            }
        }

        private FTPCredentialDialog()
        {
            InitializeComponent();
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                IsPrimaryButtonEnabled = false;
                IsSecondaryButtonEnabled = false;

                try
                {
                    if (string.IsNullOrEmpty(AccountBox.Text))
                    {
                        args.Cancel = true;
                    }
                    else
                    {
                        Message.Text = $"{Globalization.GetString("FTPCredentialDialogStatus1")}...";
                        Message.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                        Message.Visibility = Visibility.Visible;
                        ProgressControl.Visibility = Visibility.Visible;
                        AnonymousLogin.IsEnabled = false;
                        SavePassword.IsEnabled = false;
                        AccountBox.IsEnabled = false;
                        PasswordBox.IsEnabled = false;

                        if (AnonymousLogin.IsChecked.GetValueOrDefault())
                        {
                            FtpController = await FtpClientController.CreateAsync(Analysis.Host,
                                                                                  Analysis.Port,
                                                                                  "anonymous",
                                                                                  "anonymous",
                                                                                  Analysis.Path.StartsWith("ftps", StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            FtpController = await FtpClientController.CreateAsync(Analysis.Host,
                                                                                  Analysis.Port,
                                                                                  AccountBox.Text,
                                                                                  PasswordBox.Password,
                                                                                  Analysis.Path.StartsWith("ftps", StringComparison.OrdinalIgnoreCase));

                            if (SavePassword.IsChecked.GetValueOrDefault())
                            {
                                Protector.RequestProtection(AccountBox.Text, PasswordBox.Password);
                            }
                            else if (Protector.CheckExists(AccountBox.Text))
                            {
                                Protector.RemoveProtection(AccountBox.Text);
                            }
                        }
                    }
                }
                finally
                {
                    IsPrimaryButtonEnabled = true;
                    IsSecondaryButtonEnabled = true;
                    DefaultButton = ContentDialogButton.None;
                    DefaultButton = ContentDialogButton.Primary;
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;

                AnonymousLogin.IsEnabled = true;
                SavePassword.IsEnabled = true;

                if (!AnonymousLogin.IsChecked.GetValueOrDefault())
                {
                    AccountBox.IsEnabled = true;
                    PasswordBox.IsEnabled = true;
                }

                Message.Foreground = new SolidColorBrush(Colors.Red);
                ProgressControl.Visibility = Visibility.Collapsed;

                if (ex is FtpAuthenticationException)
                {
                    if (AnonymousLogin.IsChecked.GetValueOrDefault())
                    {
                        Message.Text = Globalization.GetString("FTPCredentialDialogStatus2");
                    }
                    else
                    {
                        Message.Text = Globalization.GetString("FTPCredentialDialogStatus3");
                    }
                }
                else
                {
                    Message.Text = Globalization.GetString("FTPCredentialDialogStatus4");
                }

                if (ex is not OperationCanceledException)
                {
                    LogTracer.Log(ex, $"Could not connect to the ftp server \"{Analysis.Host}\"");
                }
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void UserNameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Protector.CheckExists(AccountBox.Text))
            {
                SavePassword.IsChecked = true;
                PasswordBox.Password = Protector.GetPassword(e.AddedItems.Cast<string>().Single());
            }
        }

        private void AnonymousLogin_Checked(object sender, RoutedEventArgs e)
        {
            AccountBox.Text = "anonymous";
            PasswordBox.Password = "anonymous";
        }

        private void AnonymousLogin_Unchecked(object sender, RoutedEventArgs e)
        {
            AccountBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
        }
    }
}
