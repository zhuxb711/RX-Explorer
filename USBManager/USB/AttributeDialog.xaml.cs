using System;
using System.ComponentModel;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace USBManager
{
    public sealed partial class AttributeDialog : ContentDialog, INotifyPropertyChanged
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
                    CreateTime = file.DateCreated.Year + "年" + file.DateCreated.Month + "月" + file.DateCreated.Day + "日, " + file.DateCreated.Hour + ":" + file.DateCreated.Minute + ":" + file.DateCreated.Second;

                    var Properties = await file.GetBasicPropertiesAsync();

                    FileSize = Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
                (Properties.Size / 1048576f >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
                Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB") + " (" + Properties.Size.ToString("N0") + " 字节)";

                    ChangeTime = Properties.DateModified.Year + "年" + Properties.DateModified.Month + "月" + Properties.DateModified.Day + "日, " + (Properties.DateModified.Hour < 10 ? "0" + Properties.DateModified.Hour : Properties.DateModified.Hour.ToString()) + ":" + (Properties.DateModified.Minute < 10 ? "0" + Properties.DateModified.Minute : Properties.DateModified.Minute.ToString()) + ":" + (Properties.DateModified.Second < 10 ? "0" + Properties.DateModified.Second : Properties.DateModified.Second.ToString());
                }
                else if(Item is StorageFolder folder)
                {
                    Si.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Na.Text = "文件夹名";
                    Ty.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                    FileName = folder.Name;
                    Path = folder.Path;
                    CreateTime = folder.DateCreated.Year + "年" + folder.DateCreated.Month + "月" + folder.DateCreated.Day + "日, " + folder.DateCreated.Hour + ":" + folder.DateCreated.Minute + ":" + folder.DateCreated.Second;

                    var Properties = await folder.GetBasicPropertiesAsync();
                    ChangeTime = Properties.DateModified.Year + "年" + Properties.DateModified.Month + "月" + Properties.DateModified.Day + "日, " + (Properties.DateModified.Hour < 10 ? "0" + Properties.DateModified.Hour : Properties.DateModified.Hour.ToString()) + ":" + (Properties.DateModified.Minute < 10 ? "0" + Properties.DateModified.Minute : Properties.DateModified.Minute.ToString()) + ":" + (Properties.DateModified.Second < 10 ? "0" + Properties.DateModified.Second : Properties.DateModified.Second.ToString());
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
