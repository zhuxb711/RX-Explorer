using RX_Explorer.Class;
using ShareClassLibrary;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class LinkOptionsDialog : QueueContentDialog
    {
        public string Path { get; private set; }

        public string[] Arguments { get; private set; }

        public string Comment { get; private set; }

        public string WorkDirectory { get; private set; }

        public int HotKey { get; private set; }

        public bool RunAsAdmin { get; private set; }

        public WindowState WindowState { get; private set; }

        public LinkOptionsDialog()
        {
            InitializeComponent();

            WindowStateComboBox.Items.Add("Normal");
            WindowStateComboBox.Items.Add("Minimized");
            WindowStateComboBox.Items.Add("Maximized");

            WindowStateComboBox.SelectedIndex = 0;
        }

        private async void BrowserFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                TargetPath.Text = File.Path;
            }
        }

        private async void BrowserFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                TargetPath.Text = Folder.Path;
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(TargetPath.Text))
            {
                args.Cancel = true;
                EmptyTip.IsOpen = true;
            }
            else
            {
                Path = TargetPath.Text;
                WindowState = (WindowState)WindowStateComboBox.SelectedIndex;
                WorkDirectory = LinkWorkDirectory.Text;
                HotKey = (int)Enum.Parse<VirtualKey>(HotKeyInput.Text.Replace("Ctrl + Alt + ", string.Empty));

                if (!string.IsNullOrWhiteSpace(LinkArgument.Text))
                {
                    Arguments = Regex.Matches(LinkArgument.Text, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value).ToArray();
                }

                if (!string.IsNullOrWhiteSpace(LinkDescription.Text))
                {
                    Comment = LinkDescription.Text;
                }
            }
        }

        private void HotKeyInput_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Back)
            {
                HotKeyInput.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
            }
            else if (e.Key != VirtualKey.Shift && e.Key != VirtualKey.Control && e.Key != VirtualKey.CapitalLock && e.Key != VirtualKey.Menu)
            {
                string KeyName = Enum.GetName(typeof(VirtualKey), e.Key);

                if (string.IsNullOrEmpty(KeyName))
                {
                    HotKeyInput.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                }
                else
                {
                    if (e.Key >= VirtualKey.F1 && e.Key <= VirtualKey.F24)
                    {
                        HotKeyInput.Text = KeyName;
                    }
                    else
                    {
                        HotKeyInput.Text = $"Ctrl + Alt + {KeyName}";
                    }
                }
            }
        }
    }
}
