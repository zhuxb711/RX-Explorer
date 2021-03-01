using RX_Explorer.CustomControl;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

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

        private static async Task GenerateSubMenuItemsAsync(IList<MenuFlyoutItemBase> Items, ContextMenuItem[] SubMenus, RoutedEventHandler ClickHandler)
        {
            foreach (ContextMenuItem SubItem in SubMenus)
            {
                if (SubItem.SubMenus.Length > 0)
                {
                    MenuFlyoutSubItem Item = new MenuFlyoutSubItem
                    {
                        Text = SubItem.Name,
                        Tag = SubItem
                    };

                    await GenerateSubMenuItemsAsync(Item.Items, SubItem.SubMenus, ClickHandler).ConfigureAwait(true);

                    Items.Add(Item);
                }
                else
                {
                    MenuFlyoutItemWithImage FlyoutItem = new MenuFlyoutItemWithImage
                    {
                        Text = SubItem.Name,
                        Tag = SubItem
                    };

                    if (SubItem.IconData.Length != 0)
                    {
                        using (MemoryStream Stream = new MemoryStream(SubItem.IconData))
                        {
                            BitmapImage Icon = new BitmapImage();
                            FlyoutItem.ImageIcon = Icon;
                            await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                        }
                    }
                    else
                    {
                        FlyoutItem.Icon = new FontIcon
                        {
                            FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                            Glyph = "\uE2AC"
                        };
                    }

                    FlyoutItem.Click += ClickHandler;

                    Items.Add(FlyoutItem);
                }
            }
        }

        public async Task<AppBarButtonWithImage> GenerateUIButtonAsync(RoutedEventHandler ClickHandler)
        {
            AppBarButtonWithImage Button = new AppBarButtonWithImage
            {
                Label = Name,
                MinWidth = 250,
                Tag = this
            };
            Button.Click += ClickHandler;

            if (IconData.Length != 0)
            {
                using (MemoryStream Stream = new MemoryStream(IconData))
                {
                    BitmapImage Icon = new BitmapImage();
                    Button.ImageIcon = Icon;
                    await Icon.SetSourceAsync(Stream.AsRandomAccessStream());
                }
            }
            else
            {
                Button.Icon = new FontIcon
                {
                    FontFamily = new Windows.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE2AC"
                };
            }

            if (SubMenus.Length > 0)
            {
                MenuFlyout Flyout = new MenuFlyout();

                await GenerateSubMenuItemsAsync(Flyout.Items, SubMenus, ClickHandler).ConfigureAwait(true);

                Button.Flyout = Flyout;
            }

            return Button;
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
