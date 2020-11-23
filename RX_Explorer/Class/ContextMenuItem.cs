using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;

namespace RX_Explorer.Class
{
    public sealed class ContextMenuItem : IEquatable<ContextMenuItem>
    {
        public string Description { get; private set; }

        public string Verb { get; private set; }

        public byte[]? IconData { get; private set; }

        public string BelongTo { get; private set; }

        public ContextMenuItem(string Description, string Verb, string IconData, string BelongTo)
        {
            this.Description = Description;
            this.Verb = Verb;
            this.IconData = string.IsNullOrEmpty(IconData) ? null : Convert.FromBase64String(IconData);
            this.BelongTo = BelongTo;
        }

        public void UpdateBelonging(string BelongTo)
        {
            this.BelongTo = BelongTo;
        }

        public async Task<Button> GenerateUIButton()
        {
            Grid Gr = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock Block = new TextBlock
            {
                Text = Description
            };
            Block.SetValue(Grid.ColumnProperty, 2);
            Gr.Children.Add(Block);

            Image ImageControl = new Image
            {
                Stretch = Windows.UI.Xaml.Media.Stretch.Uniform,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ImageControl.SetValue(Grid.ColumnProperty, 0);
            Gr.Children.Add(ImageControl);

            if (IconData != null)
            {
                using (MemoryStream Stream = new MemoryStream(IconData))
                {
                    BitmapImage Icon = new BitmapImage();
                    ImageControl.Source = Icon;
                    await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                }
            }
            else
            {
                BitmapImage Icon = new BitmapImage();
                ImageControl.Source = Icon;
                Icon.UriSource = AppThemeController.Current.Theme == ElementTheme.Light ? new Uri("ms-appx:///Assets/DefaultAppIcon-Black.png") : new Uri("ms-appx:///Assets/DefaultAppIcon-White.png");
            }


            Button Btn = new Button
            {
                Content = Gr,
                Tag = this,
                Style = (Style)Application.Current.Resources["ButtonLikeCommandBarFlyout"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left
            };

            return Btn;
        }

        public async Task Invoke()
        {
            await FullTrustProcessController.Current.InvokeContextMenuItem(this).ConfigureAwait(false);
        }

        public bool Equals(ContextMenuItem other)
        {
            return Verb == other?.Verb;
        }

        public override bool Equals(object obj)
        {
            if (obj is ContextMenuItem Item)
            {
                return Verb == Item.Verb;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Verb.GetHashCode();
        }
    }
}
