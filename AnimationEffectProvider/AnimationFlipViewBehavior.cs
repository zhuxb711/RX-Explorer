using Microsoft.Xaml.Interactivity;
using System;
using System.Linq;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace AnimationEffectProvider
{
    public sealed class AnimationFlipViewBehavior : DependencyObject, IBehavior
    {
        FlipView Flip;
        ScrollViewer Viewer;
        Compositor compositor;
        CompositionPropertySet ScrollPropSet;
        ExpressionAnimation CenterPointAnimation;
        ExpressionAnimation ScaleAnimation;

        public DependencyObject AssociatedObject { get; private set; }

        public void Attach(DependencyObject associatedObject)
        {
            AssociatedObject = associatedObject;
            if (associatedObject is FlipView flip)
            {
                Flip = flip;
                Flip.Loaded += Flip_Loaded;
            }
            else
            {
                throw new ArgumentException("对象不是FlipView");
            }
        }

        private void Flip_Loaded(object sender, RoutedEventArgs e)
        {
            if (Helper.FindVisualChild<ScrollViewer>(Flip, "ScrollViewerMain") is ScrollViewer View)
            {
                Viewer = View;
                InitCompositionResources(Viewer);
            }
            else
            {
                throw new ArgumentException("ScrollViewerMain is not exist");
            }
        }

        public void Detach()
        {
            Flip.Loaded -= Flip_Loaded;
            Flip = null;
            Viewer = null;
            compositor = null;
            ScrollPropSet = null;
            CenterPointAnimation = null;
            ScaleAnimation = null;
        }

        void InitCompositionResources(ScrollViewer scroll)
        {
            if (compositor == null)
            {
                compositor = ElementCompositionPreview.GetElementVisual(Flip).Compositor;
            }
            if (scroll == null)
            {
                return;
            }

            ScrollPropSet = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(Viewer);
            if (CenterPointAnimation == null)
            {
                CenterPointAnimation = compositor.CreateExpressionAnimation("Vector3(visual.Size.X/2,visual.Size.Y/2,0)");
            }
            if (ScaleAnimation == null)
            {
                ScaleAnimation = compositor.CreateExpressionAnimation("Clamp(1- (visual.Offset.X + scroll.Translation.X) / visual.Size.X * 0.4, 0f, 1f)");
                //ScaleXAnimation = compositor.CreateExpressionAnimation("scroll.Translation.X % visual.Size.X < 0 ? ( -scroll.Translation.X % visual.Size.X) / visual.Size.X > 0.5 ? ( -scroll.Translation.X % visual.Size.X) / visual.Size.X : 1- ( -scroll.Translation.X % visual.Size.X) / visual.Size.X : 1");
                ScaleAnimation.SetReferenceParameter("scroll", ScrollPropSet);
            }
        }

        public void InitAnimation(InitOption Option)
        {
            if (compositor != null)
            {
                if (Option == InitOption.AroundImage)
                {
                    for (int i = Flip.SelectedIndex > 2 ? Flip.SelectedIndex - 2 : 0; i < Flip.SelectedIndex + 2 && i < Flip.Items.Count; i++)
                    {
                        if (Flip.ContainerFromIndex(i) is UIElement Element)
                        {
                            var Visual = ElementCompositionPreview.GetElementVisual(Element);
                            CenterPointAnimation.SetReferenceParameter("visual", Visual);
                            Visual.StartAnimation("CenterPoint", CenterPointAnimation);
                            Visual.StopAnimation("Scale.X");
                            ScaleAnimation.SetReferenceParameter("visual", Visual);
                            Visual.StartAnimation("Scale.X", ScaleAnimation);
                            Visual.StartAnimation("Scale.Y", ScaleAnimation);
                        }
                    }
                }
                else
                {
                    foreach (var Visual in from Item in Flip.Items
                                           let Element = Flip.ContainerFromItem(Item) as UIElement
                                           where Element != null
                                           select ElementCompositionPreview.GetElementVisual(Element))
                    {
                        CenterPointAnimation.SetReferenceParameter("visual", Visual);
                        Visual.StartAnimation("CenterPoint", CenterPointAnimation);
                        Visual.StopAnimation("Scale.X");
                        ScaleAnimation.SetReferenceParameter("visual", Visual);
                        Visual.StartAnimation("Scale.X", ScaleAnimation);
                        Visual.StartAnimation("Scale.Y", ScaleAnimation);
                    }
                }
            }
        }
    }


    internal static class Helper
    {
        public static T FindVisualChild<T>(DependencyObject obj, int Index = 0) where T : DependencyObject
        {
            if (Index == -1) return null;
            int count = VisualTreeHelper.GetChildrenCount(obj);
            int findedcount = 0;
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                {
                    if (findedcount == Index)
                    {
                        return (T)child;
                    }
                    else
                    {
                        findedcount++;
                    }
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child, findedcount);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        public static T FindVisualChild<T>(DependencyObject obj, string name) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(obj);
            int findedcount = 0;
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                {
                    if ((child as FrameworkElement).Name == name)
                    {
                        return (T)child;
                    }
                    else
                    {
                        findedcount++;
                    }
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child, findedcount);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }
    }

    public enum InitOption
    {
        AroundImage,
        Full
    }
}
