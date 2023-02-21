using RX_Explorer.Class;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class AQSGuide : QueueContentDialog
    {
        public AQSGuide()
        {
            InitializeComponent();
            Loaded += AQSGuide_Loaded;
        }

        private async void AQSGuide_Loaded(object sender, RoutedEventArgs e)
        {
            Task MinDelayTask = Task.Delay(1000);

            MarkDown.Text = await FileIO.ReadTextAsync(Globalization.CurrentLanguage switch
            {
                LanguageEnum.Chinese_Simplified => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_cn_s.txt")),
                LanguageEnum.English => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_en.txt")),
                LanguageEnum.French => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_fr.txt")),
                LanguageEnum.Chinese_Traditional => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_cn_t.txt")),
                LanguageEnum.Spanish => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_es.txt")),
                LanguageEnum.German => await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/AQSGuide_de.txt")),
                _ => throw new Exception("Unsupported language")
            });

            await MinDelayTask.ContinueWith((_) => LoadingTip.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async void MarkDown_LinkClicked(object sender, Microsoft.Toolkit.Uwp.UI.Controls.LinkClickedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri(e.Link));
        }
    }
}
