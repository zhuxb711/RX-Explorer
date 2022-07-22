using Microsoft.UI.Xaml.Controls;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public sealed class ContextMenuItem : IEquatable<ContextMenuItem>
    {
        public string Name => DataPackage.Name;

        public int Id => DataPackage.Id;

        public string Verb => DataPackage.Verb;

        public byte[] IconData => DataPackage.IconData;

        public string[] RelatedPath => DataPackage.RelatedPath;

        private ContextMenuItem[] subMenu;

        public ContextMenuItem[] SubMenus => subMenu ??= DataPackage.SubMenus.Select((Menu) => new ContextMenuItem(Menu)).ToArray();

        private readonly ContextMenuPackage DataPackage;

        public ContextMenuItem(ContextMenuPackage DataPackage)
        {
            this.DataPackage = DataPackage;
        }

        public static async Task<IReadOnlyList<MenuFlyoutItemBase>> GenerateSubMenuItemsAsync(ContextMenuItem[] SubMenus, RoutedEventHandler ClickHandler)
        {
            List<MenuFlyoutItemBase> MenuItems = new List<MenuFlyoutItemBase>(SubMenus.Length);

            foreach (ContextMenuItem SubItem in SubMenus.OrderByFastStringSortAlgorithm((Item) => Item.Name, SortDirection.Ascending))
            {
                if (SubItem.SubMenus.Length > 0)
                {
                    MenuFlyoutSubItem Item = new MenuFlyoutSubItem
                    {
                        Text = SubItem.Name,
                        Tag = SubItem,
                        MinWidth = 150,
                        MaxWidth = 300,
                        FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                        Icon = new FontIcon
                        {
                            Glyph = "\uE2AC"
                        }
                    };

                    ToolTipService.SetToolTip(Item, SubItem.Name);

                    Item.Items.AddRange(await GenerateSubMenuItemsAsync(SubItem.SubMenus, ClickHandler));

                    MenuItems.Add(Item);
                }
                else
                {
                    MenuFlyoutItem FlyoutItem = new MenuFlyoutItem
                    {
                        Text = SubItem.Name,
                        Tag = SubItem,
                        MinWidth = 150,
                        MaxWidth = 300,
                        FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                    };
                    FlyoutItem.Click += ClickHandler;

                    if (SubItem.IconData.Length > 0)
                    {
                        FlyoutItem.Icon = new ImageIcon
                        {
                            Source = await Helper.CreateBitmapImageAsync(SubItem.IconData)
                        };
                    }
                    else
                    {
                        FlyoutItem.Icon = new FontIcon
                        {
                            Glyph = "\uE2AC"
                        };
                    }

                    ToolTipService.SetToolTip(FlyoutItem, SubItem.Name);

                    MenuItems.Add(FlyoutItem);
                }
            }

            return MenuItems;
        }

        public async Task<AppBarButton> GenerateUIButtonAsync(RoutedEventHandler ClickHandler)
        {
            AppBarButton Button = new AppBarButton
            {
                Label = Name,
                Tag = this,
                Width = 320,
                FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily,
                Name = "ExtraButton"
            };
            Button.Click += ClickHandler;

            if (IconData.Length > 0)
            {
                Button.Icon = new ImageIcon
                {
                    Source = await Helper.CreateBitmapImageAsync(IconData)
                };
            }
            else
            {
                Button.Icon = new FontIcon
                {
                    Glyph = "\uE2AC"
                };
            }

            if (SubMenus.Length > 0)
            {
                MenuFlyout Flyout = new MenuFlyout();

                Flyout.Items.AddRange(await GenerateSubMenuItemsAsync(SubMenus, ClickHandler));

                Button.Flyout = Flyout;
            }

            return Button;
        }

        public async Task<bool> InvokeAsync()
        {
            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await Exclusive.Controller.InvokeContextMenuItemAsync(DataPackage).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(InvokeAsync)}");
            }

            return false;
        }

        public bool Equals(ContextMenuItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                return Id == other.Id;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is ContextMenuItem Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(ContextMenuItem left, ContextMenuItem right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Id == right.Id;
                }
            }
        }

        public static bool operator !=(ContextMenuItem left, ContextMenuItem right)
        {
            if (left is null)
            {
                return right is not null;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return left.Id != right.Id;
                }
            }
        }
    }
}
