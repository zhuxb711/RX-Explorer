using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class SecureAreaIntroDialog : QueueContentDialog
    {
        public SecureAreaIntroDialog()
        {
            InitializeComponent();
            StorageFile IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri(Globalization.Language==LanguageEnum.Chinese?"ms-appx:///Assets/IntroFile-Chinese.txt": "ms-appx:///Assets/IntroFile-English.txt")).AsTask().Result;
            MarkDown.Text = FileIO.ReadTextAsync(IntroFile).AsTask().Result;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
