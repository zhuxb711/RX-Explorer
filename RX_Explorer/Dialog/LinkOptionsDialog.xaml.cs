using RX_Explorer.Class;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace RX_Explorer.Dialog
{
    public sealed partial class LinkOptionsDialog : QueueContentDialog
    {
        public string Path { get; private set; }

        public IEnumerable<string> Arguments { get; private set; }

        public string Comment { get; private set; }

        public string WorkDirectory { get; private set; }

        public byte HotKey { get; private set; }

        public bool RunAsAdmin { get; private set; }

        public WindowState WindowState { get; private set; }

        public LinkOptionsDialog()
        {
            InitializeComponent();

            WindowStateComboBox.Items.Add(Globalization.GetString("ShortcutWindowsStateText1"));
            WindowStateComboBox.Items.Add(Globalization.GetString("ShortcutWindowsStateText2"));
            WindowStateComboBox.Items.Add(Globalization.GetString("ShortcutWindowsStateText3"));

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
                HotKey = (byte)Enum.Parse<VirtualKey>(HotKeyInput.Text.Replace("Ctrl + Alt + ", string.Empty));

                if (!string.IsNullOrWhiteSpace(LinkArgument.Text))
                {
                    Arguments = Regex.Matches(LinkArgument.Text, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value);
                }

                if (!string.IsNullOrWhiteSpace(LinkDescription.Text))
                {
                    Comment = LinkDescription.Text;
                }
            }
        }

        private void HotKeyInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Back:
                    {
                        HotKeyInput.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        break;
                    }
                case VirtualKey.Shift:
                case VirtualKey.Control:
                case VirtualKey.CapitalLock:
                case VirtualKey.Menu:
                case VirtualKey.Space:
                case VirtualKey.Tab:
                    {
                        break;
                    }
                default:
                    {
                        string KeyName = Enum.GetName(typeof(VirtualKey), e.Key);

                        if (string.IsNullOrEmpty(KeyName))
                        {
                            HotKeyInput.Text = Enum.GetName(typeof(VirtualKey), VirtualKey.None);
                        }
                        else
                        {
                            if ((e.Key >= VirtualKey.F1 && e.Key <= VirtualKey.F24) || (e.Key >= VirtualKey.NumberPad0 && e.Key <= VirtualKey.NumberPad9))
                            {
                                HotKeyInput.Text = KeyName;
                            }
                            else
                            {
                                HotKeyInput.Text = $"Ctrl + Alt + {KeyName}";
                            }
                        }

                        break;
                    }
            }
        }
    }
}
