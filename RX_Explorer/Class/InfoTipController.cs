using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace RX_Explorer.Class
{
    public sealed class InfoTipController
    {
        private static InfoTipController Instance;
        public static InfoTipController Current => Instance ??= new InfoTipController();

        private Panel Element;
        private readonly ConcurrentDictionary<InfoTipType, int> CounterDictionary;

        public void SetInfoTipPanel(Panel Element)
        {
            this.Element = Element;
        }

        public bool CheckIfAlreadyOpened(InfoTipType Type)
        {
            return CounterDictionary.GetOrAdd(Type, 0) > 0;
        }

        public void Show(InfoTipType Type)
        {
            if (Element == null)
            {
                throw new InvalidOperationException($"{nameof(SetInfoTipPanel)} must be called first");
            }

            if (CounterDictionary.AddOrUpdate(Type, 1, (_, CurrentValue) => CurrentValue + 1) == 1)
            {
                Storyboard ShowStoryboard = new Storyboard();

                InfoBar InfoTip = new InfoBar
                {
                    Name = Enum.GetName(typeof(InfoTipType), Type),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Opacity = 0,
                    IsOpen = true,
                    IsClosable = true
                };
                InfoTip.Closing += (s, e) =>
                {
                    if (e.Reason == InfoBarCloseReason.CloseButton)
                    {
                        if (CounterDictionary.TryRemove(Type, out _))
                        {
                            Hide(Type);
                            e.Cancel = true;
                        }
                    }
                };

                Canvas.SetZIndex(InfoTip, 0);
                Element.Children.Add(InfoTip);

                switch (Type)
                {
                    case InfoTipType.MandatoryUpdateAvailable:
                        {
                            Button ActionButton = new Button
                            {
                                Content = Globalization.GetString("SystemTip_UpdateAvailableActionButton")
                            };
                            ActionButton.Click += async (s, e) =>
                            {
                                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9N88QBQKF2RS"));
                            };

                            InfoTip.Title = Globalization.GetString("SystemTip_UpdateAvailableTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_ForeUpdateAvailableContent");
                            InfoTip.Severity = InfoBarSeverity.Error;
                            InfoTip.ActionButton = ActionButton;

                            break;
                        }
                    case InfoTipType.UpdateAvailable:
                        {
                            Button ActionButton = new Button
                            {
                                Content = Globalization.GetString("SystemTip_UpdateAvailableActionButton")
                            };
                            ActionButton.Click += async (s, e) =>
                            {
                                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?productid=9N88QBQKF2RS"));
                            };

                            InfoTip.Title = Globalization.GetString("SystemTip_UpdateAvailableTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_UpdateAvailableContent");
                            InfoTip.Severity = InfoBarSeverity.Success;
                            InfoTip.ActionButton = ActionButton;

                            break;
                        }
                    case InfoTipType.ConfigRestartRequired:
                        {
                            InfoTip.Title = Globalization.GetString("SystemTip_RestartTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_ConfigRestartContent");
                            InfoTip.Severity = InfoBarSeverity.Warning;

                            break;
                        }
                    case InfoTipType.LanguageRestartRequired:
                        {
                            InfoTip.Title = Globalization.GetString("SystemTip_RestartTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_LanguageRestartContent");
                            InfoTip.Severity = InfoBarSeverity.Warning;

                            break;
                        }
                    case InfoTipType.FontFamilyRestartRequired:
                        {
                            InfoTip.Title = Globalization.GetString("SystemTip_RestartTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_FontFamilyRestartContent");
                            InfoTip.Severity = InfoBarSeverity.Warning;

                            break;
                        }
                    case InfoTipType.ThumbnailDelay:
                        {
                            InfoTip.Title = Globalization.GetString("SystemTip_LoadFileDelayTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_LoadFileDelayContent");
                            InfoTip.Severity = InfoBarSeverity.Warning;

                            ShowStoryboard.Completed += async (s, e) =>
                            {
                                await Task.Delay(8000).ContinueWith((_) =>
                                {
                                    Hide(InfoTipType.ThumbnailDelay);
                                }, TaskScheduler.FromCurrentSynchronizationContext());
                            };

                            break;
                        }
                    case InfoTipType.FullTrustBusy:
                        {
                            InfoTip.Title = Globalization.GetString("SystemTip_FullTrustBusyTitle");
                            InfoTip.Message = Globalization.GetString("SystemTip_FullTrustBusyContent");
                            InfoTip.Severity = InfoBarSeverity.Warning;

                            break;
                        }
                }

                DoubleAnimation ShowAnimation = new DoubleAnimation
                {
                    EnableDependentAnimation = true,
                    Duration = TimeSpan.FromMilliseconds(500),
                    From = 0,
                    To = 1
                };
                Storyboard.SetTarget(ShowAnimation, InfoTip);
                Storyboard.SetTargetProperty(ShowAnimation, "Opacity");

                ShowStoryboard.Children.Add(ShowAnimation);

                ShowStoryboard.Begin();
            }
        }

        public void Hide(InfoTipType Type)
        {
            if (Element == null)
            {
                throw new InvalidOperationException($"{nameof(SetInfoTipPanel)} must be called first");
            }

            if (CounterDictionary.AddOrUpdate(Type, 0, (_, CurrentValue) => Math.Max(CurrentValue - 1, 0)) == 0)
            {
                if (Element.Children.OfType<InfoBar>().FirstOrDefault((Tip) => Tip.Name == Enum.GetName(typeof(InfoTipType), Type)) is InfoBar InfoTip)
                {
                    Storyboard HideStoryboard = new Storyboard();
                    HideStoryboard.Completed += (s, e) =>
                    {
                        Element.Children.Remove(InfoTip);
                    };

                    DoubleAnimation HideAnimation = new DoubleAnimation
                    {
                        EnableDependentAnimation = true,
                        Duration = TimeSpan.FromMilliseconds(500),
                        From = 1,
                        To = 0
                    };
                    Storyboard.SetTarget(HideAnimation, InfoTip);
                    Storyboard.SetTargetProperty(HideAnimation, "Opacity");

                    HideStoryboard.Children.Add(HideAnimation);

                    HideStoryboard.Begin();
                }
            }
        }

        private InfoTipController()
        {
            CounterDictionary = new ConcurrentDictionary<InfoTipType, int>();
        }
    }
}
