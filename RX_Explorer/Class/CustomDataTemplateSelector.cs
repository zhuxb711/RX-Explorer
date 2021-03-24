using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public class RatingControlDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Label { get; set; }
        public DataTemplate Rating { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is KeyValuePair<string, object> Pair && Pair.Key == Globalization.GetString("Properties_Details_Rating"))
            {
                return Rating;
            }
            else
            {
                return Label;
            }
        }
    }
}
