using RX_Explorer.Class;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaIntroDialog : QueueContentDialog
    {
        public SecureAreaIntroDialog()
        {
            InitializeComponent();
            Loaded += SecureAreaIntroDialog_Loaded;
        }

        private async void SecureAreaIntroDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Task MinDelayTask = Task.Delay(1000);

            MarkDown.Text = await FileIO.ReadTextAsync(Globalization.CurrentLanguage switch
            {
                LanguageEnum.Chinese_Simplified => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Chinese_S.txt")),
                LanguageEnum.Chinese_Traditional => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Chinese_T.txt")),
                LanguageEnum.English => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-English.txt")),
                LanguageEnum.French => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-French.txt")),
                LanguageEnum.Spanish => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Spanish.txt")),
                LanguageEnum.German => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-German.txt")),
                _ => throw new NotSupportedException()
            });

            await MinDelayTask.ContinueWith((_) => LoadingTip.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }
}
