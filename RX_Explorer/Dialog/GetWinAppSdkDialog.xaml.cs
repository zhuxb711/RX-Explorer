using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Store;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class GetWinAppSdkDialog : QueueContentDialog
    {
        private readonly InterlockedNoReentryExecution NoReentryExecution;

        public GetWinAppSdkDialog()
        {
            InitializeComponent();
            NoReentryExecution = new InterlockedNoReentryExecution();
        }

        private async void GetActivationCodeButton_Click(object sender, RoutedEventArgs e)
        {
            await NoReentryExecution.ExecuteAsync(async () =>
            {
                ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.Gray);
                ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Downloading_ActivationCode");
                ActivateCodeTextBox.Text = string.Empty;
                ActivateCodeTextBox.IsEnabled = false;
                ActivateUrlTextBox.Text = string.Empty;
                ActivateUrlTextBox.Visibility = Visibility.Collapsed;
                GetActivationCodeTextContent.Visibility = Visibility.Collapsed;
                GetActivationCodeButton.Visibility = Visibility.Visible;

                try
                {
                    string AccountName = string.Empty;

                    IReadOnlyList<User> CurrentUsers = await User.FindAllAsync();

                    if (CurrentUsers.FirstOrDefault((User) => User.Type == UserType.LocalUser && User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated) is User CurrentUser)
                    {
                        AccountName = Convert.ToString(await CurrentUser.GetPropertyAsync(KnownUserProperties.AccountName));
                    }

                    if (string.IsNullOrEmpty(AccountName))
                    {
                        ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                        ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Empty_AccountName");
                    }
                    else
                    {
                        string ReceiptXml = await CurrentApp.GetAppReceiptAsync();

                        if (string.IsNullOrEmpty(ReceiptXml))
                        {
                            ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                            ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Empty_ReceiptData");
                        }
                        else
                        {
                            HttpWebRequest Request = WebRequest.CreateHttp("http://52.230.36.100:3303/validation/validateReceipt");
                            Request.ReadWriteTimeout = 60000;
                            Request.Timeout = 60000;
                            Request.Method = "POST";
                            Request.ContentType = "application/json";
                            Request.UserAgent = "RX-Explorer (UWP)";

                            using (Stream WriteStream = await Request.GetRequestStreamAsync())
                            using (StreamWriter Writer = new StreamWriter(WriteStream, Encoding.UTF8, 1024, true))
                            {
                                await Writer.WriteAsync(JsonSerializer.Serialize(new GetWinAppSdkRequestDto
                                {
                                    userId = AccountName,
                                    receipt = ReceiptXml
                                }));

                                await Writer.FlushAsync();
                            }

                            JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            };

                            try
                            {
                                using (HttpWebResponse Response = (HttpWebResponse)await Request.GetResponseAsync())
                                using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.GetEncoding(Response.CharacterSet)))
                                {
                                    GetWinAppSdkResponseDto ResponseDto = JsonSerializer.Deserialize<GetWinAppSdkResponseDto>(await Reader.ReadToEndAsync(), SerializerOptions);
                                    ActivateCodeTextBox.Text = ResponseDto.Content.ActivationCode;
                                    ActivateCodeTextBox.IsEnabled = true;
                                    ActivateUrlTextBox.Text = ResponseDto.Content.ActivationUrl;
                                    ActivateUrlTextBox.Visibility = Visibility.Visible;
                                    GetActivationCodeButton.Visibility = Visibility.Collapsed;
                                }
                            }
                            catch (WebException ex)
                            {
                                LogTracer.Log(ex, "Could not download the activation code");

                                ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);

                                if (ex.Response is HttpWebResponse Response)
                                {
                                    using (StreamReader Reader = new StreamReader(Response.GetResponseStream(), Encoding.UTF8))
                                    {
                                        ActivateCodeTextBox.PlaceholderText = JsonSerializer.Deserialize<GetWinAppSdkResponseDto>(await Reader.ReadToEndAsync(), SerializerOptions).ErrorMessage;
                                    }
                                }
                                else
                                {
                                    ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Unknown_Exception");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not retrieve the activation code");
                    ActivateCodeTextBox.PlaceholderForeground = new SolidColorBrush(Colors.OrangeRed);
                    ActivateCodeTextBox.PlaceholderText = Globalization.GetString("GetWinAppSdk_Unknown_Exception");
                }
                finally
                {
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
    }
}
