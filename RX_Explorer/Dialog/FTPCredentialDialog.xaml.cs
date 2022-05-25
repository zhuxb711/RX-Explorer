using FluentFTP;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class FTPCredentialDialog : QueueContentDialog
    {
        private readonly FTPPathAnalysis Analysis;

        private readonly CredentialProtector Protector;

        public FTPClientController FtpController { get; private set; }

        public FTPCredentialDialog(FTPPathAnalysis Analysis) : this()
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
                if (AnonymousLogin.IsChecked.GetValueOrDefault())
                {
                    Message.Text = $"{Globalization.GetString("FTPCredentialDialogStatus1")}...";
                    Message.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
                    Message.Visibility = Visibility.Visible;
                    ProgressControl.Visibility = Visibility.Visible;
                    AnonymousLogin.IsEnabled = false;

                    FTPClientController Controller = new FTPClientController(Analysis.Host, Analysis.Port, "anonymous", "anonymous");

                    if (await Controller.ConnectAsync())
                    {
                        FtpController = Controller;
                    }
                    else
                    {
                        throw new TimeoutException("Ftp server do not response in time");
                    }
                }
                else
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

                        FTPClientController Controller = new FTPClientController(Analysis.Host, Analysis.Port, AccountBox.Text, PasswordBox.Password);

                        if (await Controller.ConnectAsync())
                        {
                            FtpController = Controller;

                            if (SavePassword.IsChecked.GetValueOrDefault())
                            {
                                Protector.RequestProtection(AccountBox.Text, PasswordBox.Password);
                            }
                            else if (Protector.CheckExists(AccountBox.Text))
                            {
                                Protector.RemoveProtection(AccountBox.Text);
                            }
                        }
                        else
                        {
                            throw new TimeoutException("Ftp server do not response in time");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;

                AnonymousLogin.IsEnabled = true;
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

                LogTracer.Log(ex, "Could not connect to the ftp server");
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
