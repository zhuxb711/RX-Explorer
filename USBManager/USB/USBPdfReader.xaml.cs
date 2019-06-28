using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace USBManager
{
    public sealed partial class USBPdfReader : Page
    {
        private StorageFile PdfFile;
        private ObservableCollection<BitmapImage> PdfCollection;
        private PdfDocument Pdf;
        private int LastPageIndex = 0;
        private bool IsRunning = false;
        private Queue<int> LoadQueue;
        private AutoResetEvent ExitLocker;
        private CancellationTokenSource Cancellation;
        private uint MaxLoad = 0;
        public USBPdfReader()
        {
            InitializeComponent();
            Loaded += USBPdfReader_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is StorageFile file)
            {
                PdfFile = file;
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Flip.SelectionChanged -= Flip_SelectionChanged;
            Flip.SelectionChanged -= Flip_SelectionChanged1;

            PdfFile = null;
            LoadQueue.Clear();
            LoadQueue = null;
            IsRunning = false;

            await Task.Run(() =>
            {
                Cancellation.Cancel();
                ExitLocker.WaitOne();
            });

            ExitLocker.Dispose();
            ExitLocker = null;

            Cancellation.Dispose();
            Cancellation = null;

            PdfCollection.Clear();
            PdfCollection = null;
            Pdf = null;
        }

        private async void USBPdfReader_Loaded(object sender, RoutedEventArgs e)
        {
            PdfCollection = new ObservableCollection<BitmapImage>();
            LoadQueue = new Queue<int>();
            ExitLocker = new AutoResetEvent(false);
            Cancellation = new CancellationTokenSource();
            Flip.SelectionChanged += Flip_SelectionChanged;
            Flip.SelectionChanged += Flip_SelectionChanged1;
            Flip.ItemsSource = PdfCollection;
            MaxLoad = 0;
            LastPageIndex = 0;

            Pdf = await PdfDocument.LoadFromFileAsync(PdfFile);
            //对于PDF超过5页的一次性加载5页。不足5页的，有多少加载多少。或者收到取消指令退出
            for (uint i = 0; i < 5 && i < Pdf.PageCount && !Cancellation.IsCancellationRequested; i++)
            {
                using (PdfPage Page = Pdf.GetPage(i))
                {
                    using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                    {
                        await Page.RenderToStreamAsync(PageStream);
                        BitmapImage DisplayImage = new BitmapImage();
                        await DisplayImage.SetSourceAsync(PageStream);
                        PdfCollection.Add(DisplayImage);
                    }
                }
            }
            ExitLocker.Set();
        }

        //此事件主要负责显示当前页数
        private void Flip_SelectionChanged1(object sender, SelectionChangedEventArgs e)
        {
            int CurrentPage = Flip.SelectedIndex + 1;
            PageNotification.Show(CurrentPage + " / (共 " + Pdf.PageCount + " 页)", 1200);
        }

        private async void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /*
             * 此处采用队列的方式，每次事件激活时将Flip.SelectedIndex存入队列
             * 队列中的数字代表当前看的页码下标，设计上PDF必须超前5页加载资源以应对快速翻页
             * 因此从队列取出的数字经过+4后即为当前需要加载的页面
             * 多次快速进入此函数时，只会向队列添加数字，之后便会被IsRunning拦截
             * 最终达到页面总数时，取消此函数对于事件的订阅
             */
            LoadQueue.Enqueue(Flip.SelectedIndex);

            lock (SyncRootProvider.SyncRoot)
            {
                if (IsRunning)
                {
                    return;
                }
                IsRunning = true;
            }

            await Task.Run(async () =>
            {
                while (LoadQueue.Count != 0)
                {
                    //获取待处理的页码
                    int CurrentIndex = LoadQueue.Dequeue();

                    //如果LastPageIndex < CurrentIndex，说明是向右翻页
                    if (LastPageIndex < CurrentIndex)
                    {
                        uint CurrentLoading = (uint)(CurrentIndex + 4);

                        /*
                         * MaxLoad始终取CurrentLoading达到过的最大值
                         * 同时检查要加载的页码是否小于等于最大值
                         * 可避免因向左翻页再向右翻页从而通过LastPageIndex < CurrentIndex检查
                         * 导致已加载过的页面重复加载的问题
                         */
                        if (CurrentLoading <= MaxLoad)
                        {
                            continue;
                        }
                        MaxLoad = CurrentLoading;

                        if (CurrentLoading >= Pdf.PageCount)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                Flip.SelectionChanged -= Flip_SelectionChanged;
                            });
                            return;
                        }

                        using (PdfPage Page = Pdf.GetPage(CurrentLoading))
                        {
                            using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                            {
                                await Page.RenderToStreamAsync(PageStream);
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    BitmapImage DisplayImage = new BitmapImage();
                                    await DisplayImage.SetSourceAsync(PageStream);
                                    PdfCollection.Add(DisplayImage);
                                });
                            }
                        }
                    }
                    LastPageIndex = CurrentIndex;
                }
            });

            IsRunning = false;
        }

    }
}
