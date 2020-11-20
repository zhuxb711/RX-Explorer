using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Controls.Primitives;

namespace RX_Explorer.Class
{
    public sealed class ListViewBaseSelectionExtention
    {
        public double VerticalBottomScrollThreshold => View.ActualHeight - ThresholdBorderThickness;

        public double VerticalTopScrollThreshold => ThresholdBorderThickness;

        public double HorizontalRightScrollThreshold => View.ActualWidth - ThresholdBorderThickness;

        public double HorizontalLeftScrollThreshold => ThresholdBorderThickness;

        public double ThresholdBorderThickness { get; set; } = 35;

        private ListViewBase View;

        private Rectangle RectangleInCanvas;

        private Point AbsStartPoint;

        private ScrollViewer InnerScrollView;

        public ListViewBaseSelectionExtention(ListViewBase View, Rectangle RectangleInCanvas)
        {
            this.View = View ?? throw new ArgumentNullException(nameof(View), "Argument could not be null");
            this.RectangleInCanvas = RectangleInCanvas ?? throw new ArgumentNullException(nameof(RectangleInCanvas), "Argument could not be null");

            if (RectangleInCanvas.Parent is not Canvas)
            {
                throw new ArgumentException("Reactangle must be placed in Canvas", nameof(RectangleInCanvas));
            }

            this.View.PointerPressed += View_RectangleDrawStart;
            this.View.PointerReleased += View_RectangleDrawEnd;
            this.View.PointerCaptureLost += View_RectangleDrawEnd;
            this.View.PointerCanceled += View_RectangleDrawEnd;

            if (View.IsLoaded)
            {
                InnerScrollView = View.FindChildOfType<ScrollViewer>();
            }
            else
            {
                this.View.Loaded += View_Loaded;
            }
        }

        private void View_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Point RelativeEndPoint = e.GetCurrentPoint(View).Position;
            Point RelativeStartPoint = new Point(AbsStartPoint.X - InnerScrollView.HorizontalOffset, AbsStartPoint.Y - InnerScrollView.VerticalOffset);

            DrawRectangleInCanvas(RelativeStartPoint, RelativeEndPoint);

            IEnumerable<FileSystemStorageItemBase> SelectedArray = VisualTreeHelper.FindElementsInHostCoordinates(new Rect(RelativeStartPoint, RelativeEndPoint), View).OfType<SelectorItem>().Select((Item) => Item.Content as FileSystemStorageItemBase);

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

        private void View_RectangleDrawEnd(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            View.PointerMoved -= View_PointerMoved;

            RectangleInCanvas.SetValue(Canvas.LeftProperty, 0);
            RectangleInCanvas.SetValue(Canvas.TopProperty, 0);
            RectangleInCanvas.Width = 0;
            RectangleInCanvas.Height = 0;

            View.ReleasePointerCapture(e.Pointer);
        }

        private void View_RectangleDrawStart(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Point CurrentPoint = e.GetCurrentPoint(View).Position;
            AbsStartPoint = new Point(CurrentPoint.X + InnerScrollView.HorizontalOffset, CurrentPoint.Y + InnerScrollView.VerticalOffset);

            View.SelectedItems.Clear();
            View.CapturePointer(e.Pointer);

            View.PointerMoved += View_PointerMoved;
        }

        private void View_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            View.Loaded -= View_Loaded;

            InnerScrollView = View.FindChildOfType<ScrollViewer>();
        }

        private void SrcollIfNeed(Point RelativeEndPoint)
        {
            if (RelativeEndPoint.X > HorizontalRightScrollThreshold)
            {
                if (RelativeEndPoint.Y > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + RelativeEndPoint.X - HorizontalRightScrollThreshold, InnerScrollView.VerticalOffset + RelativeEndPoint.Y - VerticalBottomScrollThreshold, null);
                }
                else if (RelativeEndPoint.Y < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + RelativeEndPoint.X - HorizontalRightScrollThreshold, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + RelativeEndPoint.Y, null);
                }
                else
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset + RelativeEndPoint.X - HorizontalRightScrollThreshold, null, null);
                }
            }
            else if (RelativeEndPoint.X < HorizontalLeftScrollThreshold)
            {
                if (RelativeEndPoint.Y > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - RelativeEndPoint.X, InnerScrollView.VerticalOffset + RelativeEndPoint.Y - VerticalBottomScrollThreshold, null);
                }
                else if (RelativeEndPoint.Y < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - RelativeEndPoint.X, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + RelativeEndPoint.Y, null);
                }
                else
                {
                    InnerScrollView.ChangeView(InnerScrollView.HorizontalOffset - HorizontalLeftScrollThreshold - RelativeEndPoint.X, null, null);
                }
            }
            else
            {
                if (RelativeEndPoint.Y > VerticalBottomScrollThreshold)
                {
                    InnerScrollView.ChangeView(null, InnerScrollView.VerticalOffset + RelativeEndPoint.Y - VerticalBottomScrollThreshold, null);
                }
                else if (RelativeEndPoint.Y < VerticalTopScrollThreshold)
                {
                    InnerScrollView.ChangeView(null, InnerScrollView.VerticalOffset - VerticalTopScrollThreshold + RelativeEndPoint.Y, null);
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
    }
}
