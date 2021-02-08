using ShareClassLibrary;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;

namespace RX_Explorer.Class
{
    public sealed class ContextMenuItem : IEquatable<ContextMenuItem>
    {
        public string Name
        {
            get
            {
                return DataPackage.Name;
            }
        }

        public int Id
        {
            get
            {
                return DataPackage.Id;
            }
        }

        public string Verb
        {
            get
            {
                return DataPackage.Verb;
            }
        }

        public byte[] IconData
        {
            get
            {
                return DataPackage.IconData;
            }
        }

        public ContextMenuItem[] SubMenus { get; }

        public string BelongTo { get; }

        private readonly ContextMenuPackage DataPackage;

        public ContextMenuItem(ContextMenuPackage DataPackage, string BelongTo)
        {
            this.DataPackage = DataPackage;
            this.BelongTo = BelongTo;

            SubMenus = DataPackage.SubMenus.Select((Menu) => new ContextMenuItem(Menu, BelongTo)).ToArray();
        }

        private static async Task<Button> GenerateUIButtonCoreAsync(CommandBarFlyout ParentFlyout, ContextMenuItem Item)
        {
            Grid Gr = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            Gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });

            TextBlock Block = new TextBlock
            {
                Text = Item.Name
            };
            Grid.SetColumn(Block, 2);
            Gr.Children.Add(Block);

            if (Item.IconData.Length != 0)
            {
                Image ImageControl = new Image
                {
                    Stretch = Windows.UI.Xaml.Media.Stretch.Uniform,
                    Height = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(ImageControl, 0);
                Gr.Children.Add(ImageControl);

                using (MemoryStream Stream = new MemoryStream(Item.IconData))
                {
                    BitmapImage Icon = new BitmapImage();
                    ImageControl.Source = Icon;
                    await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                }
            }

            Button Btn = new Button
            {
                Content = Gr,
                Tag = Item,
                Style = (Style)Application.Current.Resources["ButtonLikeCommandBarFlyout"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            Btn.Click += async (s, e) =>
            {
                if (s is Button Btn)
                {
                    if (Btn.Flyout != null)
                    {
                        Btn.Flyout.ShowAt(Btn);
                    }
                    else if (Btn.Tag is ContextMenuItem MenuItem)
                    {
                        ParentFlyout.Hide();
                        await MenuItem.InvokeAsync().ConfigureAwait(true);
                    }
                }
            };

            if (Item.SubMenus.Length > 0)
            {
                StackPanel Panel = new StackPanel();

                foreach (ContextMenuItem SubItem in Item.SubMenus)
                {
                    Panel.Children.Add(await GenerateUIButtonCoreAsync(ParentFlyout, SubItem).ConfigureAwait(true));
                }

                Btn.Flyout = new Flyout
                {
                    Content = Panel,
                    Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                };

                Gr.ColumnDefinitions[3].Width = new GridLength(12);

                Viewbox Box = new Viewbox
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = new FontIcon
                    {
                        Glyph = "\uE974"
                    }
                };

                Grid.SetColumn(Box, 3);
                Gr.Children.Add(Box);
            }

            return Btn;
        }

        public Task<Button> GenerateUIButtonAsync(CommandBarFlyout ParentFlyout)
        {
            return GenerateUIButtonCoreAsync(ParentFlyout, this);
        }

        public async Task InvokeAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.InvokeContextMenuItemAsync(this).ConfigureAwait(false);
            }
        }

        public bool Equals(ContextMenuItem other)
        {
            return Id == other?.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj is ContextMenuItem Item)
            {
                return Id == Item.Id;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
