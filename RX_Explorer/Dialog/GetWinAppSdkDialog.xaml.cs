using RX_Explorer.Class;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class GetWinAppSdkDialog : QueueContentDialog
    {
        private readonly InterlockedNoReentryExecution NoReentryExecution = new InterlockedNoReentryExecution();

        public GetWinAppSdkDialog()
        {
            InitializeComponent();
        }

        private async void GetActivationCodeButton_Click(object sender, RoutedEventArgs e)
        {
            static async Task<string> GetCurrnetUserAccountNameAsync()
            {
                List<User> UserList = new List<User>();

                foreach (User User in await Helper.AbsorbExceptionAsync(() => User.FindAllAsync().AsTask()))
                {
                    if (User.Type == UserType.LocalUser && User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated)
                    {
                        UserList.Add(User);
                    }
                }

                foreach (User CurrentUser in UserList.Prepend(Helper.AbsorbException(User.GetDefault)).OfType<User>())
                {
                    string AccountName = Convert.ToString(await Helper.AbsorbExceptionAsync(() => CurrentUser.GetPropertyAsync(KnownUserProperties.AccountName).AsTask()));

                    if (!Helper.IsEmail(AccountName))
                    {
                        AccountName = Convert.ToString(await Helper.AbsorbExceptionAsync(() => CurrentUser.GetPropertyAsync(KnownUserProperties.DomainName).AsTask()));
                        AccountName = AccountName.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                        if (!Helper.IsEmail(AccountName))
                        {
                            continue;
                        }
                    }

                    return AccountName;
                }

                return string.Empty;
            }

            await NoReentryExecution.ExecuteAsync(async () =>
            {
                ActivateCodeTextBox.IsEnabled = false;
                ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.Gray);
                ActivateCodeTextBox.PlaceholderText = $"{Globalization.GetString("GetWinAppSdk_Downloading_ActivationCode")}...";
                ActivateCodeTextBox.Text = string.Empty;
                ActivateUrlTextBox.Text = string.Empty;
                CodeValidDate.Text = string.Empty;
                CodeValidDate.Visibility = Visibility.Collapsed;
                ActivateUrlTextBox.Visibility = Visibility.Collapsed;
                GetActivationCodeTextContent.Visibility = Visibility.Collapsed;
                GetActivationCodeButton.Visibility = Visibility.Visible;
                ContactDeveloper.Visibility = Visibility.Collapsed;

                try
                {
                    string AccountName = await GetCurrnetUserAccountNameAsync();

                    if (string.IsNullOrWhiteSpace(AccountName))
                    {
                        ContactDeveloper.Visibility = Visibility.Visible;
                        ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                        ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Empty_AccountName");
                    }
                    else
                    {
                        try
                        {
                            using (CancellationTokenSource GetAADTokenCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(GetAADTokenCancellation.Token))
                            {
                                string AADToken = await Exclusive.Controller.GetAADTokenFromBackendAsync(GetAADTokenCancellation.Token);
                                string CustomerCollectionsId = await MSStoreHelper.GetCustomerCollectionsIdAsync(AADToken, AccountName);

                                using (CancellationTokenSource GetRedeemCodeCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                                {
                                    RedeemCodeContentResponseDto RedeemCodeResponse = await Exclusive.Controller.GetRedeemCodeFromBackendAsync(CustomerCollectionsId, GetRedeemCodeCancellation.Token);

                                    CodeValidDate.Visibility = Visibility.Visible;
                                    ActivateUrlTextBox.Visibility = Visibility.Visible;
                                    GetActivationCodeButton.Visibility = Visibility.Collapsed;
                                    ActivateUrlTextBox.Text = RedeemCodeResponse.RedeemUrl;
                                    ActivateCodeTextBox.Text = RedeemCodeResponse.RedeemCode;
                                    CodeValidDate.Text = $"{Globalization.GetString("CodeValidDate")}: {RedeemCodeResponse.StartDate:d} - {RedeemCodeResponse.ExpireDate:d}";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ContactDeveloper.Visibility = Visibility.Visible;
                            ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                            ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Redeem_Error");
                            LogTracer.Log(ex, $"Could not retrieve the activation code, reason: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    ActivateCodeTextBox.IsEnabled = true;
                    GetActivationCodeTextContent.Visibility = Visibility.Visible;
                }
            });
        }

        private void ActivateCodeCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();

            DataPackage Package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };

            Package.SetText(ActivateCodeTextBox.Text);

            Clipboard.SetContent(Package);
        }

        private void ActivateUrlCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.Clear();

            DataPackage Package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };

            Package.SetText(ActivateUrlTextBox.Text);

            Clipboard.SetContent(Package);
            Clipboard.Flush();
        }

        private async void ContactDeveloper_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri($"mailto:zrfcfgs@outlook.com?subject={Uri.EscapeDataString(Globalization.GetString("ContactDeveloper_RedeemActivationCode"))}&body={Uri.EscapeDataString($"{Globalization.GetString("ContactDeveloper_YourAccount")}: {Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Globalization.GetString("ContactDeveloper_YourOrderScreenshot")}:{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}")}"));
        }
    }
}
