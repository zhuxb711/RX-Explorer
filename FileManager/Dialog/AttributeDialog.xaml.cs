using System;
using System.ComponentModel;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class AttributeDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public string FileName { get; private set; }

        public string FileType { get; private set; }

        public string Path { get; private set; }

        public string FileSize { get; private set; }

        public string CreateTime { get; private set; }

        public string ChangeTime { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public AttributeDialog(IStorageItem Item)
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                if (Item is StorageFile file)
                {
                    FileName = file.Name;
                    FileType = file.DisplayType + " (" + file.FileType + ")";
                    Path = file.Path;
                    CreateTime = file.DateCreated.ToString("F");

                    var Properties = await file.GetBasicPropertiesAsync();

                    FileSize = (Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString("0.00") + " KB" :
                    (Properties.Size / 1048576f < 1024 ? Math.Round(Properties.Size / 1048576f, 2).ToString("0.00") + " MB" :
                    (Properties.Size / 1073741824f < 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString("0.00") + " GB" :
                    Math.Round(Properties.Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB"))) + " (" + Properties.Size.ToString("N0") + " 字节)";

                    ChangeTime = Properties.DateModified.ToString("F");
                }
                else if (Item is StorageFolder folder)
                {
                    Si.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Na.Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                ? "文件夹名"
                                : "FolderName";
                    Ty.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                    FileName = folder.DisplayName;
                    Path = folder.Path;
                    CreateTime = folder.DateCreated.ToString("F");

                    var Properties = await folder.GetBasicPropertiesAsync();
                    ChangeTime = Properties.DateModified.ToString("F");
                }
                OnPropertyChanged();
            };
        }

        public void OnPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FileName"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FileType"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Path"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("FileSize"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("CreateTime"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("ChangeTime"));
            }
        }
    }
}
