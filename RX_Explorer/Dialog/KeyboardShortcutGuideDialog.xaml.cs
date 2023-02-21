using RX_Explorer.Class;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class KeyboardShortcutGuideDialog : QueueContentDialog
    {
        public KeyboardShortcutGuideDialog()
        {
            InitializeComponent();
            Loaded += KeyboardShortcutGuideDialog_Loaded;
        }

        private async void KeyboardShortcutGuideDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Task MinDelayTask = Task.Delay(1000);

            MarkDown.Text = await FileIO.ReadTextAsync(Globalization.CurrentLanguage switch
            {
                LanguageEnum.Chinese_Simplified => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_cn_s.txt")),
                LanguageEnum.English => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_en.txt")),
                LanguageEnum.French => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_fr.txt")),
                LanguageEnum.Chinese_Traditional => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_cn_t.txt")),
                LanguageEnum.Spanish => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_es.txt")),
                LanguageEnum.German => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/KeyboardShortcut_de.txt")),
                _ => throw new Exception("Unsupported language")
            });

            await MinDelayTask.ContinueWith((_) => LoadingTip.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
