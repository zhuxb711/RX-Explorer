using RX_Explorer.Class;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class ModifyDefaultTerminalDialog : QueueContentDialog
    {
        private ObservableCollection<TerminalProfile> TerminalList;

        private TerminalProfile LastSelectedProfile;

        public ModifyDefaultTerminalDialog()
        {
            InitializeComponent();
            Loading += ModifyDefaultTerminalDialog_Loading;
        }

        private async void ModifyDefaultTerminalDialog_Loading(FrameworkElement sender, object args)
        {
            TerminalList = new ObservableCollection<TerminalProfile>(await SQLite.Current.GetAllTerminalProfile().ConfigureAwait(true));

            switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
            {
                case LaunchQuerySupportStatus.Available:
                case LaunchQuerySupportStatus.NotSupported:
                    {
                        TerminalList.Add(new TerminalProfile("Windows Terminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "/d [CurrentLocation]"));
                        break;
                    }
            }

            ProfileSelector.ItemsSource = TerminalList;
        }

        private void ProfileSelector_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Text) && TerminalList.All((Profile) => Profile.Name != args.Text))
            {
                TerminalList.Add(new TerminalProfile(args.Text, string.Empty, string.Empty));
            }
        }

        private void ProfileSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LastSelectedProfile != null)
            {
                LastSelectedProfile.Path = ExecutablePath.Text;
                LastSelectedProfile.Argument = Argument.Text;
            }
            else
            {
                RemoveProfile.Visibility = Visibility.Visible;
            }

            if (ProfileSelector.SelectedItem is TerminalProfile Profile)
            {
                ExecutablePath.Text = Profile.Path;
                Argument.Text = Profile.Argument;
                LastSelectedProfile = Profile;
            }
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            if (ProfileSelector.SelectedItem is TerminalProfile CurrentProfile)
            {
                CurrentProfile.Path = ExecutablePath.Text;
                CurrentProfile.Argument = Argument.Text;
            }

            if (TerminalList.FirstOrDefault((Profile) => string.IsNullOrWhiteSpace(Profile.Name)) is TerminalProfile ErrProfile1)
            {
                ProfileSelector.SelectedItem = ErrProfile1;
                EmptyTip.Target = ProfileSelector;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (TerminalList.FirstOrDefault((Profile) => string.IsNullOrWhiteSpace(Profile.Path)) is TerminalProfile ErrProfile2)
            {
                ProfileSelector.SelectedItem = ErrProfile2;
                EmptyTip.Target = ExecutablePath;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
            else if (TerminalList.FirstOrDefault((Profile) => !Profile.Argument.Contains("[CurrentLocation]", StringComparison.CurrentCulture)) is TerminalProfile ErrProfile3)
            {
                ProfileSelector.SelectedItem = ErrProfile3;
                FormatErrorTip.Target = Argument;
                FormatErrorTip.IsOpen = true;
                args.Cancel = true;
            }
            else
            {
                foreach (TerminalProfile Profile in TerminalList)
                {
                    await SQLite.Current.SetOrModifyTerminalProfile(Profile).ConfigureAwait(true);
                }
            }

            Deferral.Complete();
        }

        private async void RemoveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (ProfileSelector.SelectedItem is TerminalProfile Profile)
            {
                await SQLite.Current.DeleteTerminalProfile(Profile).ConfigureAwait(true);

                TerminalList.Remove(Profile);

                ProfileSelector.Text = string.Empty;
                ExecutablePath.Text = string.Empty;
                Argument.Text = string.Empty;
            }
        }
    }
}
