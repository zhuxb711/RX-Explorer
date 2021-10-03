using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public class AnimationStyleSelector : StyleSelector
    {
        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            switch (container)
            {
                case ListViewItem:
                    {
                        if (AnimationController.Current.IsDisableSelectionAnimation)
                        {
                            return Application.Current.Resources["CustomListViewItemWithoutAnimationStyle"] as Style;
                        }
                        else
                        {
                            return Application.Current.Resources["CustomListViewItemWithAnimationStyle"] as Style;
                        }
                    }
                case GridViewItem:
                    {
                        if (AnimationController.Current.IsDisableSelectionAnimation)
                        {
                            return Application.Current.Resources["CustomGridViewItemWithoutAnimationStyle"] as Style;
                        }
                        else
                        {
                            return Application.Current.Resources["CustomGridViewItemWithAnimationStyle"] as Style;
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }
    }
}
