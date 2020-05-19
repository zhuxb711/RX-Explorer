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

            StorageFile IntroFile = null;
            switch (Globalization.CurrentLanguage)
            {
                case LanguageEnum.Chinese:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Chinese.txt")).AsTask().Result;
                        break;
                    }
                case LanguageEnum.English:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-English.txt")).AsTask().Result;
                        break;
                    }
                case LanguageEnum.French:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-French.txt")).AsTask().Result;
                        break;
                    }
            }

            MarkDown.Text = FileIO.ReadTextAsync(IntroFile).AsTask().Result;
        }
    }
}
