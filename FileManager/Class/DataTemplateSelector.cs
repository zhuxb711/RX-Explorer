using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager.Class
{
    public sealed class QuickStartSelector : DataTemplateSelector
    {
        public DataTemplate NormalDataTemplate { get; set; }
        public DataTemplate AddDataTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is QuickStartItem Start && Start.DisplayName == null)
            {
                return AddDataTemplate;
            }
            else
            {
                return NormalDataTemplate;
            }
        }
    }

}
