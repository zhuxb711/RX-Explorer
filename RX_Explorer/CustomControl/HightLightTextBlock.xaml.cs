using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.CustomControl
{
    public sealed partial class HightLightTextBlock : UserControl
    {
        public HightLightTextBlock()
        {
            InitializeComponent();
        }

        public string Text
        {
            get
            {
                return (string)GetValue(TextProperty);
            }
            set
            {
                SetValue(TextProperty, value);
            }
        }


        public string HightLightText
        {
            get
            {
                return (string)GetValue(HightLightTextProperty);
            }
            set
            {
                SetValue(HightLightTextProperty, value);
            }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(HightLightTextBlock), new PropertyMetadata(string.Empty, new PropertyChangedCallback(Text_Changed)));
        public static readonly DependencyProperty HightLightTextProperty = DependencyProperty.Register("HightLightText", typeof(string), typeof(HightLightTextBlock), new PropertyMetadata(string.Empty, new PropertyChangedCallback(HightLightText_Changed)));

        private static void Text_Changed(DependencyObject Object, DependencyPropertyChangedEventArgs Args)
        {
            if (Object is HightLightTextBlock Instance)
            {
                if (Args.NewValue != null && Args.NewValue is string NewText)
                {
                    TextHighlighter HighLighter = new TextHighlighter()
                    {
                        Background = new SolidColorBrush(Colors.Yellow),
                        Ranges =
                        {
                            new TextRange()
                            {
                                StartIndex = NewText.IndexOf(Instance.HightLightText, StringComparison.OrdinalIgnoreCase),
                                Length = Instance.HightLightText.Length
                            }
                        }
                    };

                    Instance.ResultTextBlock.TextHighlighters.Clear();
                    Instance.ResultTextBlock.TextHighlighters.Add(HighLighter);
                }
            }
        }

        private static void HightLightText_Changed(DependencyObject Object, DependencyPropertyChangedEventArgs Args)
        {
            if (Object is HightLightTextBlock Instance)
            {
                if (Args.NewValue != null && Args.NewValue is string NewHightlightText)
                {
                    TextHighlighter HighLighter = new TextHighlighter()
                    {
                        Background = new SolidColorBrush(Colors.Yellow),
                        Ranges =
                        {
                            new TextRange()
                            {
                                StartIndex = Instance.Text.IndexOf(NewHightlightText, StringComparison.OrdinalIgnoreCase),
                                Length = NewHightlightText.Length
                            }
                        }
                    };

                    Instance.ResultTextBlock.TextHighlighters.Clear();
                    Instance.ResultTextBlock.TextHighlighters.Add(HighLighter);
                }
            }
        }
    }
}
