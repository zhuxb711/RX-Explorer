using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml;

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

        private DispatcherTimer Timer;

        private ulong Length = 0;

        public AttributeDialog(IStorageItem Item)
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                if (Item is StorageFile file)
                {
                    Inc.Visibility = Visibility.Collapsed;

                    FileName = file.Name;
                    FileType = file.DisplayType + " (" + file.FileType + ")";
                    Path = file.Path;
                    CreateTime = file.DateCreated.ToString("F");

                    var Properties = await file.GetBasicPropertiesAsync();

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        FileSize = (Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString("0.00") + " KB" :
                                    (Properties.Size / 1048576f < 1024 ? Math.Round(Properties.Size / 1048576f, 2).ToString("0.00") + " MB" :
                                    (Properties.Size / 1073741824f < 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString("0.00") + " GB" :
                                    Math.Round(Properties.Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB"))) + " (" + Properties.Size.ToString("N0") + " 字节)";
                    }
                    else
                    {
                        FileSize = (Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString("0.00") + " KB" :
                                    (Properties.Size / 1048576f < 1024 ? Math.Round(Properties.Size / 1048576f, 2).ToString("0.00") + " MB" :
                                    (Properties.Size / 1073741824f < 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString("0.00") + " GB" :
                                    Math.Round(Properties.Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB"))) + " (" + Properties.Size.ToString("N0") + " bytes)";
                    }

                    ChangeTime = Properties.DateModified.ToString("F");

                    OnPropertyChanged();
                }
                else if (Item is StorageFolder folder)
                {
                    Cancellation = new CancellationTokenSource();

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        Na.Text = "文件夹名";
                        Include = "计算中...";
                        FileSize = "准备中...";
                    }
                    else
                    {
                        Na.Text = "FolderName";
                        Include = "Calculating...";
                        FileSize = "Preparing...";
                    }

                    FileType = folder.DisplayType;
                    FileName = folder.DisplayName;
                    Path = folder.Path;
                    CreateTime = folder.DateCreated.ToString("F");

                    var Properties = await folder.GetBasicPropertiesAsync();
                    ChangeTime = Properties.DateModified.ToString("F");

                    OnPropertyChanged();

                    var CountTask = CalculateFolderAndFileCount(folder);
                    var CalculateTask = CalculateFolderSize(folder);
                    await Task.WhenAll(CountTask, CalculateTask).ContinueWith((task) =>
                    {
                        Cancellation.Dispose();
                        Cancellation = null;
                    }, TaskScheduler.Current).ConfigureAwait(false);
                }
            };
        }

        private async Task CalculateFolderSize(StorageFolder Folder)
        {
            Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            Timer.Tick += Timer_Tick;

            QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });
            var Query = Folder.CreateFileQueryWithOptions(Options);

            try
            {
                uint TotalFiles = await Query.GetItemCountAsync();

                Timer.Start();

                for (uint Index = 0; Index < TotalFiles && !Cancellation.IsCancellationRequested; Index += 100)
                {
                    var Files = await Query.GetFilesAsync(Index, 100).AsTask(Cancellation.Token).ConfigureAwait(true);

                    for (int i = 0; i < Files.Count && !Cancellation.IsCancellationRequested; i++)
                    {
                        BasicProperties Properties = await Files[i].GetBasicPropertiesAsync();
                        Length += Properties.Size;
                    }
                }

                Timer_Tick(null, null);
            }
            catch (TaskCanceledException)
            {

            }
            finally
            {
                Timer.Tick -= Timer_Tick;
                Timer.Stop();
            }
        }

        private async Task CalculateFolderAndFileCount(StorageFolder folder)
        {
            QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            StorageFolderQueryResult FolderQuery = folder.CreateFolderQueryWithOptions(Options);
            StorageFileQueryResult FileQuery = folder.CreateFileQueryWithOptions(Options);

            try
            {
                var FolderCount = await FolderQuery.GetItemCountAsync().AsTask(Cancellation.Token).ConfigureAwait(true);
                var FileCount = await FileQuery.GetItemCountAsync().AsTask(Cancellation.Token).ConfigureAwait(true);
                Include = Globalization.Language == LanguageEnum.Chinese
                            ? $"{FileCount} 个文件 , {FolderCount} 个文件夹"
                            : $"{FileCount} files , {FolderCount} folders";
                OnPropertyChanged();
            }
            catch (TaskCanceledException)
            {

            }
        }

        private void Timer_Tick(object sender, object e)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                FileSize = Length / 1024f < 1024 ? Math.Round(Length / 1024f, 2).ToString("0.00") + " KB" :
                           (Length / 1048576f < 1024 ? Math.Round(Length / 1048576f, 2).ToString("0.00") + " MB" :
                           (Length / 1073741824f < 1024 ? Math.Round(Length / 1073741824f, 2).ToString("0.00") + " GB" :
                           Math.Round(Length / Convert.ToDouble(1099511627776), 2).ToString() + " TB")) + " (" + Length.ToString("N0") + " 字节)";
            }
            else
            {
                FileSize = Length / 1024f < 1024 ? Math.Round(Length / 1024f, 2).ToString("0.00") + " KB" :
                           (Length / 1048576f < 1024 ? Math.Round(Length / 1048576f, 2).ToString("0.00") + " MB" :
                           (Length / 1073741824f < 1024 ? Math.Round(Length / 1073741824f, 2).ToString("0.00") + " GB" :
                           Math.Round(Length / Convert.ToDouble(1099511627776), 2).ToString() + " TB")) + " (" + Length.ToString("N0") + " bytes)";
            }

            PropertyChanged.Invoke(this, new PropertyChangedEventArgs(nameof(FileSize)));
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
