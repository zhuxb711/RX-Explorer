using RX_Explorer.Class;
using System;
using Windows.Storage;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaIntroDialog : QueueContentDialog
    {
        public SecureAreaIntroDialog()
        {
            InitializeComponent();

            StorageFile IntroFile = null;

            switch (Globalization.CurrentLanguage)
            {
                case LanguageEnum.Chinese_Simplified:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Chinese_S.txt")).AsTask().Result;
                        break;
                    }
                case LanguageEnum.Chinese_Traditional:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Chinese_T.txt")).AsTask().Result;
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
                case LanguageEnum.Spanish:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-Spanish.txt")).AsTask().Result;
                        break;
                    }
                case LanguageEnum.German:
                    {
                        IntroFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/IntroFile-German.txt")).AsTask().Result;
                        break;
                    }
            }

            MarkDown.Text = FileIO.ReadTextAsync(IntroFile).AsTask().Result;
        }
    }
}
