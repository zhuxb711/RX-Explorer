using Microsoft.Toolkit.Uwp.Helpers;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Exception Ex)
            {
                string[] MessageSplit = Array.Empty<string>();

                try
                {
                    if (!string.IsNullOrWhiteSpace(Ex.Message))
                    {
                        MessageSplit = Ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch (Exception)
                {
                    //No need to hanle this exception;
                }

                string[] StackTraceSplit = Array.Empty<string>();

                try
                {
                    if (!string.IsNullOrWhiteSpace(Ex.StackTrace))
                    {
                        StackTraceSplit = Ex.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch (Exception)
                {
                    //No need to hanle this exception;
                }

                StringBuilder Builder = new StringBuilder()
                                        .AppendLine("------------------------------------")
                                        .AppendLine($"UnhandledException: {(string.IsNullOrWhiteSpace(Ex.Message) ? "<Empty>" : Ex.Message)}")
                                        .AppendLine("------------------------------------")
                                        .AppendLine("Source: RX-Explorer")
                                        .AppendLine()
                                        .AppendLine($"Version: {Package.Current.Id.Version.ToFormattedString()}")
                                        .AppendLine()
                                        .AppendLine($"Exception: {Ex.GetType().FullName}")
                                        .AppendLine()
                                        .AppendLine("Message:")
                                        .AppendLine(MessageSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, MessageSplit))
                                        .AppendLine()
                                        .AppendLine("StackTrace:")
                                        .AppendLine(StackTraceSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, StackTraceSplit))
                                        .AppendLine();

                Message.Text = Builder.ToString();
            }
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("mailto:zrfcfgs@outlook.com?subject=BugReport&body=" + Uri.EscapeDataString(Message.Text)));
        }

        private async void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            FileSavePicker Picker = new FileSavePicker
            {
                SuggestedFileName = "Export_All_Error_Log.txt",
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            Picker.FileTypeChoices.Add(Globalization.GetString("File_Type_TXT_Description"), new List<string> { ".txt" });

            if (await Picker.PickSaveFileAsync() is StorageFile PickedFile)
            {
                await LogTracer.ExportAllLogAsync(PickedFile).ConfigureAwait(false);
            }
        }
    }
}
