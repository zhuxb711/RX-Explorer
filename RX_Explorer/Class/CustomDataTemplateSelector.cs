using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public class NavigationRecordDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalTemplate { get; set; }
        public DataTemplate RootTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object Item)
        {
            if (Item is NavigationRecordDisplay Record && RootStorageFolder.Current.Path.Equals(Record.Path, StringComparison.OrdinalIgnoreCase))
            {
                return RootTemplate;
            }
            else
            {
                return NormalTemplate;
            }
        }
    }

    public class RatingControlDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Label { get; set; }
        public DataTemplate Rating { get; set; }

        protected override DataTemplate SelectTemplateCore(object Item)
        {
            if (Item is KeyValuePair<string, object> Pair && Pair.Key == Globalization.GetString("Properties_Details_Rating"))
            {
                return Rating;
            }
            else
            {
                return Label;
            }
        }
    }

    public class QuickStartDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalButtonTemplate { get; set; }
        public DataTemplate AddButtonTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object Item)
        {
            if (Item is QuickStartItem It)
            {
                if (It.Type == QuickStartType.AddButton)
                {
                    return AddButtonTemplate;
                }
                else
                {
                    return NormalButtonTemplate;
                }
            }
            else
            {
                throw new Exception("Input data is not match");
            }
        }
    }
}
