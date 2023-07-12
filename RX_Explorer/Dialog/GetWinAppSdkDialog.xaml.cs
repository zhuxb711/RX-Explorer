using RX_Explorer.Class;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;
using Windows.Services.Store;
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
                    string AccountName = string.Empty;

                    try
                    {
                        IReadOnlyList<User> CurrentUsers = await User.FindAllAsync();

                        foreach (User CurrentUser in CurrentUsers.Append(User.GetDefault())
                                                                 .Append(StoreContext.GetDefault().User)
                                                                 .Where((User) => User.Type == UserType.LocalUser && User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated))
                        {
                            AccountName = Convert.ToString(await CurrentUser.GetPropertyAsync(KnownUserProperties.AccountName));

                            if (!string.IsNullOrEmpty(AccountName))
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not retrieve the account email for current user");
                    }

                    if (string.IsNullOrEmpty(AccountName))
                    {
                        ContactDeveloper.Visibility = Visibility.Visible;
                        ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                        ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Empty_AccountName");
                    }
                    else
                    {
                        try
                        {
                            using (CancellationTokenSource Cancellation = new CancellationTokenSource(60000))
                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Cancellation.Token))
                            {
                                string AADToken = await Exclusive.Controller.GetAADTokenFromBackendAsync(Cancellation.Token);
                                string CustomerCollectionsId = await MSStoreHelper.GetCustomerCollectionsIdAsync(AADToken, AccountName);

                                RedeemCodeContentResponseDto RedeemCodeResponse = await Exclusive.Controller.GetRedeemCodeFromBackendAsync(CustomerCollectionsId, Cancellation.Token);

                                ActivateCodeTextBox.Text = RedeemCodeResponse.RedeemCode;
                                ActivateUrlTextBox.Text = RedeemCodeResponse.RedeemUrl;
                                CodeValidDate.Text = $"{Globalization.GetString("CodeValidDate")}: {RedeemCodeResponse.StartDate:d} - {RedeemCodeResponse.ExpireDate:d}";
                                ActivateUrlTextBox.Visibility = Visibility.Visible;
                                CodeValidDate.Visibility = Visibility.Visible;
                                GetActivationCodeButton.Visibility = Visibility.Collapsed;
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
        }

        private async void ContactDeveloper_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri($"mailto:zrfcfgs@outlook.com?subject={Uri.EscapeDataString(Globalization.GetString("ContactDeveloper_RedeemActivationCode"))}&body={Uri.EscapeDataString($"{Globalization.GetString("ContactDeveloper_YourAccount")}: {Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Globalization.GetString("ContactDeveloper_YourOrderScreenshot")}:{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}")}"));
        }
    }
}
