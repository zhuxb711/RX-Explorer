using System;
using System.Collections.Generic;
using System.Linq;
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

        public double ThresholdBorderThickness { get; set; } = 35;

        private volatile bool AllowProcess;

        private ListViewBase View;

        private Rectangle RectangleInCanvas;

        private Point AbsStartPoint;

        private ScrollViewer InnerScrollView;

        private bool IsDisposed;

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
            }
            else
            {
                this.View.Loaded += View_Loaded;
            }
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
                Point RelativeEndPoint = e.GetCurrentPoint(View).Position;
                Point RelativeStartPoint = new Point(AbsStartPoint.X - InnerScrollView.HorizontalOffset, AbsStartPoint.Y - InnerScrollView.VerticalOffset);

                DrawRectangleInCanvas(RelativeStartPoint, RelativeEndPoint);

                GeneralTransform AbsToWindowTransform = View.TransformToVisual(Window.Current.Content);

                Rect SelectedRect = new Rect(RelativeStartPoint, RelativeEndPoint);

                if (SelectedRect.Width >= 15 && SelectedRect.Height >= 15)
                {
                    IEnumerable<FileSystemStorageItemBase> SelectedArray = VisualTreeHelper.FindElementsInHostCoordinates(AbsToWindowTransform.TransformBounds(SelectedRect), View).OfType<SelectorItem>().Select((Item) => Item.Content as FileSystemStorageItemBase);

                    foreach (FileSystemStorageItemBase Item in View.SelectedItems.Except(SelectedArray))
                    {
                        View.SelectedItems.Remove(Item);
                    }

                    foreach (FileSystemStorageItemBase Item in SelectedArray.Except(View.SelectedItems))
                    {
                        View.SelectedItems.Add(Item);
                    }

                    SrcollIfNeed(RelativeEndPoint);
                }
            }
        }

        private void View_RectangleDrawEnd(object sender, PointerRoutedEventArgs e)
        {
            AllowProcess = false;

            if ((View.PointerCaptures?.Any()).GetValueOrDefault())
            {
                View.ReleasePointerCaptures();
            }

            ResetPosition();
        }

        private void View_RectangleDrawStart(object sender, PointerRoutedEventArgs e)
        {
            PointerPoint Pointer = e.GetCurrentPoint(View);

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && Pointer.Properties.IsLeftButtonPressed)
            {
                Point CurrentPoint = Pointer.Position;

                AbsStartPoint = new Point(CurrentPoint.X + InnerScrollView.HorizontalOffset, CurrentPoint.Y + InnerScrollView.VerticalOffset);

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
            if (StartPoint.X <= EndPoint.X)
            {
                if (StartPoint.Y >= EndPoint.Y)
                {
                    RectangleInCanvas.SetValue(Canvas.LeftProperty, Math.Max(0, StartPoint.X));
                    RectangleInCanvas.SetValue(Canvas.TopProperty, Math.Max(0, EndPoint.Y));
                    RectangleInCanvas.Width = Math.Max(0, EndPoint.X) - Math.Max(0, StartPoint.X);
                    RectangleInCanvas.Height = Math.Max(0, StartPoint.Y) - Math.Max(0, EndPoint.Y);
                }
                else
                {
                    RectangleInCanvas.SetValue(Canvas.LeftProperty, Math.Max(0, StartPoint.X));
                    RectangleInCanvas.SetValue(Canvas.TopProperty, Math.Max(0, StartPoint.Y));
                    RectangleInCanvas.Width = Math.Max(0, EndPoint.X) - Math.Max(0, StartPoint.X);
                    RectangleInCanvas.Height = Math.Max(0, EndPoint.Y) - Math.Max(0, StartPoint.Y);
                }
            }
            else
            {
                if (StartPoint.Y >= EndPoint.Y)
                {
                    RectangleInCanvas.SetValue(Canvas.LeftProperty, Math.Max(0, EndPoint.X));
                    RectangleInCanvas.SetValue(Canvas.TopProperty, Math.Max(0, EndPoint.Y));
                    RectangleInCanvas.Width = Math.Max(0, StartPoint.X) - Math.Max(0, EndPoint.X);
                    RectangleInCanvas.Height = Math.Max(0, StartPoint.Y) - Math.Max(0, EndPoint.Y);
                }
                else
                {
                    RectangleInCanvas.SetValue(Canvas.LeftProperty, Math.Max(0, EndPoint.X));
                    RectangleInCanvas.SetValue(Canvas.TopProperty, Math.Max(0, StartPoint.Y));
                    RectangleInCanvas.Width = Math.Max(0, StartPoint.X) - Math.Max(0, EndPoint.X);
                    RectangleInCanvas.Height = Math.Max(0, EndPoint.Y) - Math.Max(0, StartPoint.Y);
                }
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                View.RemoveHandler(UIElement.PointerPressedEvent, PointerPressedHandler);
                View.RemoveHandler(UIElement.PointerReleasedEvent, PointerReleasedHandler);
                View.RemoveHandler(UIElement.PointerCaptureLostEvent, PointerCaptureLostHandler);
                View.RemoveHandler(UIElement.PointerCanceledEvent, PointerCanceledHandler);
                View.RemoveHandler(UIElement.PointerMovedEvent, PointerMovedHandler);

                ResetPosition();

                View = null;
                RectangleInCanvas = null;
                InnerScrollView = null;
            }
        }

        ~ListViewBaseSelectionExtention()
        {
            Dispose();
        }
    }
}
