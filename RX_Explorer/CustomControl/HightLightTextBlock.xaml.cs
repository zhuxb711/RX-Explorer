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
                Instance.ResultTextBlock.TextHighlighters.Clear();

                if (Args.NewValue is string NewText)
                {
                    int StartIndex = NewText.IndexOf(Instance.HightLightText, StringComparison.OrdinalIgnoreCase);

                    if (StartIndex >= 0)
                    {
                        Instance.ResultTextBlock.TextHighlighters.Add(new TextHighlighter()
                        {
                            Background = new SolidColorBrush(Colors.Yellow),
                            Ranges =
                            {
                                new TextRange()
                                {
                                    StartIndex = StartIndex,
                                    Length = Instance.HightLightText.Length
                                }
                            }
                        });
                    }
                }
            }
        }

        private static void HightLightText_Changed(DependencyObject Object, DependencyPropertyChangedEventArgs Args)
        {
            if (Object is HightLightTextBlock Instance)
            {
                Instance.ResultTextBlock.TextHighlighters.Clear();

                if (Args.NewValue is string NewHightlightText)
                {
                    int StartIndex = Instance.Text.IndexOf(NewHightlightText, StringComparison.OrdinalIgnoreCase);

                    if (StartIndex >= 0)
                    {
                        Instance.ResultTextBlock.TextHighlighters.Add(new TextHighlighter()
                        {
                            Background = new SolidColorBrush(Colors.Yellow),
                            Ranges =
                            {
                                new TextRange()
                                {
                                    StartIndex = StartIndex,
                                    Length = NewHightlightText.Length
                                }
                            }
                        });
                    }
                }
            }
        }
    }
}
