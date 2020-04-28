using FileManager.Class;
using System;
using Windows.Storage;

namespace FileManager.Dialog
{
    public sealed partial class WhatIsNew : QueueContentDialog
    {
        public WhatIsNew()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                StorageFile UpdateFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/UpdateLog-Chinese.txt")).AsTask().Result;
                MarkDown.Text = FileIO.ReadTextAsync(UpdateFile).AsTask().Result;
            }
            else
            {
                StorageFile UpdateFile = StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/UpdateLog-English.txt")).AsTask().Result;
                MarkDown.Text = FileIO.ReadTextAsync(UpdateFile).AsTask().Result;
            }
        }
    }
}
