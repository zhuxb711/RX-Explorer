using FluentFTP;
using RX_Explorer.Class;
using System;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class FTPCredentialDialog : QueueContentDialog
    {
        public FTPClientController FtpController { get; private set; }

        private readonly FTPPathAnalysis Analysis;

        public FTPCredentialDialog(FTPPathAnalysis Analysis) : this()
        {
            this.Analysis = Analysis;
           
            FtpHost.Text = $"FTP服务器: {Analysis.Host}";

            if (!string.IsNullOrEmpty(Analysis.UserName))
            {
                UserNameBox.Text = Analysis.UserName;
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

            Message.Text = "正在尝试登录...";
            Message.Foreground = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Light ? Colors.Black : Colors.White);
            Message.Visibility = Windows.UI.Xaml.Visibility.Visible;
            ProgressControl.Visibility = Windows.UI.Xaml.Visibility.Visible;
            AnonymousLogin.IsEnabled = false;

            try
            {
                FTPClientController Controller = AnonymousLogin.IsChecked.GetValueOrDefault()
                                               ? new FTPClientController(Analysis.Host, Analysis.Port, "anonymous", "anonymous")
                                               : new FTPClientController(Analysis.Host, Analysis.Port, UserNameBox.Text, PasswordBox.Password);

                if (await Controller.ConnectAsync())
                {
                    FtpController = Controller;
                }
                else
                {
                    throw new TimeoutException("Ftp server do not response in time");
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
                        Message.Text = "该FTP服务器不允许以匿名方式登录";
                    }
                    else
                    {
                        Message.Text = "用户名或密码错误";
                    }
                }
                else
                {
                    Message.Text = "无法连接到该FTP服务器";
                }

                LogTracer.Log(ex, "Could not connect to the ftp server");
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
