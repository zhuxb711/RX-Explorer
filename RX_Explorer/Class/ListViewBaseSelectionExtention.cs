using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace RX_Explorer.Class
{
    public sealed class ListViewBaseSelectionExtention : IDisposable
    {
        public double VerticalBottomScrollThreshold => View.ActualHeight - ThresholdBorderThickness;

        public double VerticalTopScrollThreshold => ThresholdBorderThickness;

        public double HorizontalRightScrollThreshold => View.ActualWidth - ThresholdBorderThickness;

        public double HorizontalLeftScrollThreshold => ThresholdBorderThickness;

        public double ThresholdBorderThickness { get; } = 30;

        private volatile bool AllowProcess;

        private ListViewBase View;

        private Rectangle RectangleInCanvas;

        private Point AbsStartPoint;

        private ScrollViewer InnerScrollView;

        private ScrollBar InnerScrollBar;

        private bool IsDisposed;

        private int Locker = 0;

        private List<KeyValuePair<SelectorItem, Rect>> AbsItemLocationRecord = new List<KeyValuePair<SelectorItem, Rect>>();

        private readonly PointerEventHandler PointerPressedHandler;

        private readonly PointerEventHandler PointerReleasedHandler;

        private readonly PointerEventHandler PointerCaptureLostHandler;

        private readonly PointerEventHandler PointerCanceledHandler;

        private readonly PointerEventHandler PointerMovedHandler;

        public ListViewBaseSelectionExtention(ListViewBase View, Rectangle RectangleInCanvas)
        {
            this.View = View ?? throw new ArgumentNullException(nameof(View), "Argument could not be null");
            this.RectangleInCanvas = RectangleInCanvas ?? throw new ArgumentNullException(nameof(RectangleInCanvas), "Argument could not be null");

            this.View.AddHandler(UIElement.PointerPressedEvent, PointerPressedHandler = new PointerEventHandler(View_RectangleDrawStart), true);
            this.View.AddHandler(UIElement.PointerReleasedEvent, PointerReleasedHandler = new PointerEventHandler(View_RectangleDrawEnd), true);
            this.View.AddHandler(UIElement.PointerCaptureLostEvent, PointerCaptureLostHandler = new PointerEventHandler(View_RectangleDrawEnd), true);
            this.View.AddHandler(UIElement.PointerCanceledEvent, PointerCanceledHandler = new PointerEventHandler(View_RectangleDrawEnd), true);
            this.View.AddHandler(UIElement.PointerMovedEvent, PointerMovedHandler = new PointerEventHandler(View_PointerMoved), true);

            if (View.IsLoaded)
            {
                InnerScrollView = View.FindChildOfType<ScrollViewer>();
                InnerScrollBar = View.FindChildOfType<ScrollBar>();

                if (InnerScrollBar != null)
                {
                    InnerScrollBar.Scroll += InnerScrollBar_Scroll;
                }
            }
            else
            {
                this.View.Loaded += View_Loaded;
            }
        }

        private void InnerScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            Disable();
        }

        public void ResetPosition()
        {
            RectangleInCanvas.SetValue(Canvas.LeftProperty, 0);
            RectangleInCanvas.SetValue(Canvas.TopProperty, 0);
            RectangleInCanvas.Width = 0;
            RectangleInCanvas.Height = 0;
        }

        public void Enable()
        {
            AllowProcess = true;
        }

        public void Disable()
        {
            AllowProcess = false;

            AbsItemLocationRecord.Clear();

            if ((View.PointerCaptures?.Any()).GetValueOrDefault())
            {
                View.ReleasePointerCaptures();
            }

            ResetPosition();
        }

        private void View_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (AllowProcess && e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && e.GetCurrentPoint(View).Properties.IsLeftButtonPressed)
            {
                if (Interlocked.Exchange(ref Locker, 1) == 0)
                {
                    try
                    {
                        Point RelativeEndPoint = e.GetCurrentPoint(View).Position;
                        SrcollIfNeed(RelativeEndPoint);

                        Point AbsEndPoint = new Point(RelativeEndPoint.X + InnerScrollView.HorizontalOffset, RelativeEndPoint.Y + InnerScrollView.VerticalOffset);
                        Point RelativeStartPoint = new Point(AbsStartPoint.X - InnerScrollView.HorizontalOffset, AbsStartPoint.Y - InnerScrollView.VerticalOffset);

                        DrawRectangleInCanvas(RelativeStartPoint, RelativeEndPoint);

                        if (Math.Abs(RelativeStartPoint.X - RelativeEndPoint.X) >= 20 && Math.Abs(RelativeStartPoint.Y - RelativeEndPoint.Y) >= 20)
                        {
                            foreach (SelectorItem Item in VisualTreeHelper.FindElementsInHostCoordinates(View.TransformToVisual(Window.Current.Content).TransformBounds(new Rect(0, 0, InnerScrollView.ViewportWidth, InnerScrollView.ViewportHeight)), View).OfType<SelectorItem>().Except(AbsItemLocationRecord.Select((Rec) => Rec.Key)))
                            {
                                AbsItemLocationRecord.Add(new KeyValuePair<SelectorItem, Rect>(Item, Item.TransformToVisual(View).TransformBounds(new Rect(InnerScrollView.HorizontalOffset, InnerScrollView.VerticalOffset, Item.ActualWidth, Item.ActualHeight))));
                            }

                            Rect AbsBoxSelectionRect = new Rect(Math.Min(AbsStartPoint.X, Math.Max(AbsEndPoint.X, InnerScrollView.HorizontalOffset)), Math.Min(AbsStartPoint.Y, Math.Max(AbsEndPoint.Y, InnerScrollView.VerticalOffset)), Math.Min(Math.Abs(AbsStartPoint.X - AbsEndPoint.X), InnerScrollView.HorizontalOffset + InnerScrollView.ViewportWidth), Math.Min(Math.Abs(AbsStartPoint.Y - AbsEndPoint.Y), InnerScrollView.VerticalOffset + InnerScrollView.ViewportHeight));

                            foreach (KeyValuePair<SelectorItem, Rect> Pair in AbsItemLocationRecord)
                            {
                                Rect Intersect = RectHelper.Intersect(AbsBoxSelectionRect, Pair.Value);

                                if (Pair.Key.Content is FileSystemStorageItemBase Item)
                                {
                                    if (!Intersect.IsEmpty && Intersect.Width > 0 && Intersect.Height > 0)
                                    {
                                        View.SelectedItems.Add(Item);
                                    }
                                    else
                                    {
                                        View.SelectedItems.Remove(Item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex);
                    }
                    finally
                    {
                        _ = Interlocked.Exchange(ref Locker, 0);
                    }
                }
            }
        }


        private void View_RectangleDrawEnd(object sender, PointerRoutedEventArgs e)
        {
            Disable();
        }

        private void View_RectangleDrawStart(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint Pointer = e.GetCurrentPoint(View);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && Pointer.Properties.IsLeftButtonPressed)
            {
                Point CurrentPoint = Pointer.Position;

                AbsStartPoint = new Point(CurrentPoint.X + (InnerScrollView?.HorizontalOffset).GetValueOrDefault(), CurrentPoint.Y + (InnerScrollView?.VerticalOffset).GetValueOrDefault());

                if (AllowProcess)
                {
                    View.CapturePointer(e.Pointer);
                }
            }
        }

        private void View_Loaded(object sender, RoutedEventArgs e)
        {
            View.Loaded -= View_Loaded;

            InnerScrollView = View.FindChildOfType<ScrollViewer>();
            InnerScrollBar = InnerScrollView.FindChildOfType<ScrollBar>();
            InnerScrollBar.Scroll += InnerScrollBar_Scroll;
        }

        private void SrcollIfNeed(Point RelativeEndPoint)
        {
            double XOffset = Math.Max(RelativeEndPoint.X, 0);
            double YOffset = Math.Max(RelativeEndPoint.Y, 0);

            if (XOffset > HorizontalRightScrollThreshold)
            {
                if (YOffset > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, InnerScrollView.VerticalOffset + YOffset - VerticalBottomScrollThreshold, null);
                }
                else if (YOffset < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + YOffset, null);
                }
                else
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null);
                }
            }
            else if (XOffset < HorizontalLeftScrollThreshold)
            {
                if (YOffset > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - XOffset, InnerScrollView.VerticalOffset + YOffset - VerticalBottomScrollThreshold, null);
                }
                else if (YOffset < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - XOffset, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + YOffset, null);
                }
                else
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - XOffset, null, null);
                }
            }
            else
            {
                if (YOffset > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(null, InnerScrollView.VerticalOffset + YOffset - VerticalBottomScrollThreshold, null);
                }
                else if (YOffset < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(null, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + YOffset, null);
                }
            }
        }

        private void DrawRectangleInCanvas(Point StartPoint, Point EndPoint)
        {
            if (AllowProcess)
            {
                if (StartPoint.X <= EndPoint.X)
                {
                    if (StartPoint.Y >= EndPoint.Y)
                    {
                        double LeftPoint = Math.Max(0, StartPoint.X);
                        double TopPoint = Math.Max(0, EndPoint.Y);

                        RectangleInCanvas.SetValue(Canvas.LeftProperty, LeftPoint);
                        RectangleInCanvas.SetValue(Canvas.TopProperty, TopPoint);
                        RectangleInCanvas.Width = Math.Max(0, Math.Min(Math.Max(0, EndPoint.X), InnerScrollView.ViewportWidth) - LeftPoint);
                        RectangleInCanvas.Height = Math.Max(0, Math.Min(Math.Max(0, StartPoint.Y), InnerScrollView.ViewportHeight) - TopPoint);
                    }
                    else
                    {
                        double LeftPoint = Math.Max(0, StartPoint.X);
                        double TopPoint = Math.Max(0, StartPoint.Y);

                        RectangleInCanvas.SetValue(Canvas.LeftProperty, LeftPoint);
                        RectangleInCanvas.SetValue(Canvas.TopProperty, TopPoint);
                        RectangleInCanvas.Width = Math.Max(0, Math.Min(Math.Max(0, EndPoint.X), InnerScrollView.ViewportWidth) - LeftPoint);
                        RectangleInCanvas.Height = Math.Max(0, Math.Min(Math.Max(0, EndPoint.Y), InnerScrollView.ViewportHeight) - TopPoint);
                    }
                }
                else
                {
                    if (StartPoint.Y >= EndPoint.Y)
                    {
                        double LeftPoint = Math.Max(0, EndPoint.X);
                        double TopPoint = Math.Max(0, EndPoint.Y);

                        RectangleInCanvas.SetValue(Canvas.LeftProperty, LeftPoint);
                        RectangleInCanvas.SetValue(Canvas.TopProperty, TopPoint);
                        RectangleInCanvas.Width = Math.Max(0, Math.Min(Math.Max(0, StartPoint.X), InnerScrollView.ViewportWidth) - LeftPoint);
                        RectangleInCanvas.Height = Math.Max(0, Math.Min(Math.Max(0, StartPoint.Y), InnerScrollView.ViewportHeight) - TopPoint);
                    }
                    else
                    {
                        double LeftPoint = Math.Max(0, EndPoint.X);
                        double TopPoint = Math.Max(0, StartPoint.Y);

                        RectangleInCanvas.SetValue(Canvas.LeftProperty, LeftPoint);
                        RectangleInCanvas.SetValue(Canvas.TopProperty, TopPoint);
                        RectangleInCanvas.Width = Math.Max(0, Math.Min(Math.Max(0, StartPoint.X), InnerScrollView.ViewportWidth) - LeftPoint);
                        RectangleInCanvas.Height = Math.Max(0, Math.Min(Math.Max(0, EndPoint.Y), InnerScrollView.ViewportHeight) - TopPoint);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                GC.SuppressFinalize(this);

                View.RemoveHandler(UIElement.PointerPressedEvent, PointerPressedHandler);
                View.RemoveHandler(UIElement.PointerReleasedEvent, PointerReleasedHandler);
                View.RemoveHandler(UIElement.PointerCaptureLostEvent, PointerCaptureLostHandler);
                View.RemoveHandler(UIElement.PointerCanceledEvent, PointerCanceledHandler);
                View.RemoveHandler(UIElement.PointerMovedEvent, PointerMovedHandler);

                InnerScrollBar.Scroll -= InnerScrollBar_Scroll;

                ResetPosition();

                View = null;
                RectangleInCanvas = null;
                InnerScrollView = null;
                InnerScrollBar = null;
            }
        }

        ~ListViewBaseSelectionExtention()
        {
            Dispose();
        }
    }
}
