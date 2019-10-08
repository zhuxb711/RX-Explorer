using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class WhatIsNew : ContentDialog
    {
        public WhatIsNew()
        {
            InitializeComponent();
            Init();
        }

        private async void Init()
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                StorageFile UpdateFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/UpdateLog-Chinese.txt"));
                MarkDown.Text = await FileIO.ReadTextAsync(UpdateFile);
            }
            else
            {
                StorageFile UpdateFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/UpdateLog-English.txt"));
                MarkDown.Text = await FileIO.ReadTextAsync(UpdateFile);
            }
        }

        public new async Task ShowAsync()
        {
            await Task.Delay(2000);
            _ = await base.ShowAsync();
        }
    }
}
