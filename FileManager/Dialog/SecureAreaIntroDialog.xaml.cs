using FileManager.Class;
using System;
using Windows.Storage;

namespace FileManager.Dialog
{
    public sealed partial class SecureAreaIntroDialog : QueueContentDialog
    {
        public SecureAreaIntroDialog()
        {
            InitializeComponent();
            StorageFile IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri(Globalization.Language == LanguageEnum.Chinese ? "ms-appx:///Assets/IntroFile-Chinese.txt" : "ms-appx:///Assets/IntroFile-English.txt")).AsTask().Result;
            MarkDown.Text = FileIO.ReadTextAsync(IntroFile).AsTask().Result;
        }
    }
}
