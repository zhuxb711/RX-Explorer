using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class PdfReader : Page
    {
        private ObservableCollection<BitmapImage> PdfCollection;
        private PdfDocument Pdf;
        private int LastPageIndex;
        private Queue<int> LoadQueue;
        private ManualResetEvent ExitLocker;
        private CancellationTokenSource Cancellation;
        private uint MaxLoad;
        private double OriginHorizonOffset;
        private double OriginVerticalOffset;
        private Point OriginMousePosition;
        private int LockResource;

        public PdfReader()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileSystemStorageFile Parameters)
            {
                await Initialize(Parameters).ConfigureAwait(false);
            }
        }

        private async Task Initialize(FileSystemStorageFile PdfFile)
        {
            LoadingControl.IsLoading = true;

            PdfCollection = new ObservableCollection<BitmapImage>();
            LoadQueue = new Queue<int>();
            ExitLocker = new ManualResetEvent(false);
            Cancellation = new CancellationTokenSource();
            Flip.ItemsSource = PdfCollection;
            MaxLoad = 0;
            LastPageIndex = 0;

            try
            {
                using (IRandomAccessStream PdfStream = await PdfFile.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
                {
                    try
                    {
                        Pdf = await PdfDocument.LoadFromStreamAsync(PdfStream);
                    }
                    catch (Exception)
                    {
                        PdfPasswordDialog Dialog = new PdfPasswordDialog();
                        
                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                        {
                            Pdf = await PdfDocument.LoadFromStreamAsync(PdfStream, Dialog.Password);
                        }
                        else
                        {
                            Frame.GoBack();
                            return;
                        }
                    }
                }

                for (uint i = 0; i < 10 && i < Pdf.PageCount && !Cancellation.IsCancellationRequested; i++)
                {
                    using (PdfPage Page = Pdf.GetPage(i))
                    using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                    {
                        await Page.RenderToStreamAsync(PageStream);
                        BitmapImage DisplayImage = new BitmapImage();
                        PdfCollection.Add(DisplayImage);
                        await DisplayImage.SetSourceAsync(PageStream);
                    }
                }
            }
            catch (Exception)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_PDFOpenFailure"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                Frame.GoBack();
            }
            finally
            {
                ExitLocker.Set();

                if (!Cancellation.IsCancellationRequested)
                {
                    Flip.SelectionChanged += Flip_SelectionChanged;
                    Flip.SelectionChanged += Flip_SelectionChanged1;
                }

                await Task.Delay(1000).ConfigureAwait(true);

                LoadingControl.IsLoading = false;
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            Flip.SelectionChanged -= Flip_SelectionChanged;
            Flip.SelectionChanged -= Flip_SelectionChanged1;

            await Task.Run(() =>
            {
                Cancellation.Cancel();
                ExitLocker.WaitOne();
            }).ConfigureAwait(true);

            LoadQueue.Clear();
            LoadQueue = null;

            ExitLocker.Dispose();
            ExitLocker = null;

            Cancellation.Dispose();
            Cancellation = null;

            PdfCollection.Clear();
            PdfCollection = null;
            Pdf = null;
        }

        private void Flip_SelectionChanged1(object sender, SelectionChangedEventArgs e)
        {
            PageNotification.Show(Globalization.GetString("Pdf_Page_Tip").Replace("[CurrentPageIndex]", (Flip.SelectedIndex + 1).ToString()).Replace("[TotalPageCount]", Pdf.PageCount.ToString()), 1200);
        }

        private async void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadQueue.Enqueue(Flip.SelectedIndex);

            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                ExitLocker.Reset();

                try
                {
                    while (LoadQueue.Count != 0 && !Cancellation.IsCancellationRequested)
                    {
                        //获取待处理的页码
                        int CurrentIndex = LoadQueue.Dequeue();

                        //如果LastPageIndex < CurrentIndex，说明是向右翻页
                        if (LastPageIndex < CurrentIndex)
                        {
                            uint CurrentLoading = (uint)(CurrentIndex + 9);

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
                                Flip.SelectionChanged -= Flip_SelectionChanged;
                                return;
                            }

                            foreach (uint Index in Enumerable.Range(PdfCollection.Count, (int)CurrentLoading - PdfCollection.Count + 1))
                            {
                                if (Cancellation.IsCancellationRequested)
                                {
                                    break;
                                }

                                using (PdfPage Page = Pdf.GetPage(Index))
                                using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                                {
                                    await Page.RenderToStreamAsync(PageStream);

                                    BitmapImage DisplayImage = new BitmapImage();
                                    PdfCollection.Add(DisplayImage);
                                    await DisplayImage.SetSourceAsync(PageStream);
                                }
                            }
                        }
                        LastPageIndex = CurrentIndex;
                    }
                }
                finally
                {
                    ExitLocker.Set();
                    _ = Interlocked.Exchange(ref LockResource, 0);
                }
            }
        }

        private void ScrollViewerMain_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
            {
                ScrollViewer Viewer = (ScrollViewer)sender;
                Point TapPoint = e.GetPosition(Viewer);
                if (Math.Abs(Viewer.ZoomFactor - 1.0) < 1E-06)
                {
                    var ImageInSide = Viewer.FindChildOfType<Image>();
                    _ = Viewer.ChangeView(TapPoint.X, TapPoint.Y - (Viewer.ActualHeight - ImageInSide.ActualHeight), 2);
                }
                else
                {
                    _ = Viewer.ChangeView(null, null, 1);
                }
            }
        }

        private void ScrollViewerMain_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScrollViewer Viewer = (ScrollViewer)sender;

            if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var Point = e.GetCurrentPoint(Viewer);
                if (Point.Properties.IsLeftButtonPressed)
                {
                    var Position = Point.Position;

                    Viewer.ChangeView(OriginHorizonOffset + (OriginMousePosition.X - Position.X), OriginVerticalOffset + (OriginMousePosition.Y - Position.Y), null);
                }
            }
        }

        private void ScrollViewerMain_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScrollViewer Viewer = (ScrollViewer)sender;

            if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
                var Point = e.GetCurrentPoint(Viewer);
                if (Point.Properties.IsLeftButtonPressed)
                {
                    OriginMousePosition = Point.Position;
                    OriginHorizonOffset = Viewer.HorizontalOffset;
                    OriginVerticalOffset = Viewer.VerticalOffset;
                }
            }
        }

        private void ScrollViewerMain_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }
    }
}
