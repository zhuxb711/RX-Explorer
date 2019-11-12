using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

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

        public string Include { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private CancellationTokenSource Cancellation;

        public AttributeDialog(IStorageItem Item)
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                if (Item is StorageFile file)
                {
                    Inc.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

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

                    OnPropertyChanged();
                }
                else if (Item is StorageFolder folder)
                {
                    Cancellation = new CancellationTokenSource();

                    Si.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    Na.Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                ? "文件夹名"
                                : "FolderName";
                    FileType = folder.DisplayType;
                    FileName = folder.DisplayName;
                    Path = folder.Path;
                    CreateTime = folder.DateCreated.ToString("F");

                    var Properties = await folder.GetBasicPropertiesAsync();
                    ChangeTime = Properties.DateModified.ToString("F");

                    Include = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                ? "计算中..."
                                : "Calculating...";

                    OnPropertyChanged();

                    QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    StorageFolderQueryResult FolderQuery = folder.CreateFolderQueryWithOptions(Options);
                    StorageFileQueryResult FileQuery = folder.CreateFileQueryWithOptions(Options);
                    Cancellation.Token.Register(() =>
                    {
                        Cancellation.Dispose();
                        Cancellation = null;
                    });
                    try
                    {
                        var FolderCount = await FolderQuery.GetItemCountAsync().AsTask(Cancellation.Token);
                        var FileCount = await FileQuery.GetItemCountAsync().AsTask(Cancellation.Token);
                        Include = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                                    ? $"{FileCount} 个文件 , {FolderCount} 个文件夹"
                                    : $"{FileCount} files , {FolderCount} folders";
                        OnPropertyChanged();
                    }
                    catch (TaskCanceledException)
                    {

                    }
                }
            };
        }

        public void OnPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Path)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(CreateTime)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(ChangeTime)));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(Include)));
            }
        }

        private void QueueContentDialog_CloseButtonClick(Windows.UI.Xaml.Controls.ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogButtonClickEventArgs args)
        {
            Cancellation?.Cancel();
        }
    }
}
