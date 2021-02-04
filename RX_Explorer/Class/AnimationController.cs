using System;
using System.ComponentModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;

namespace RX_Explorer.Class
{
    public sealed class AnimationController : INotifyPropertyChanged
    {
        public TransitionCollection DeviceAndLibraryTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new PaneThemeTransition
                        {
                            Edge = EdgeTransitionLocation.Right
                        },
                        new AddDeleteThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection QuickStartTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new PaneThemeTransition
                        {
                            Edge = EdgeTransitionLocation.Bottom
                        },
                        new AddDeleteThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection AddDeleteTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new AddDeleteThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection BladeViewTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new EntranceThemeTransition
                        {
                            IsStaggeringEnabled = true
                        },
                        new RepositionThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection EntranceTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new EntranceThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection RepositionTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new RepositionThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection PaneLeftTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new PaneThemeTransition
                        {
                            Edge = EdgeTransitionLocation.Left
                        }
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public TransitionCollection PaneTopTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new PaneThemeTransition
                        {
                            Edge = EdgeTransitionLocation.Top
                        }
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        public bool IsEnableAnimation
        {
            get
            {
                return isEnableAnimation;
            }
            set
            {
                if (value != isEnableAnimation)
                {
                    isEnableAnimation = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnableAnimation)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceAndLibraryTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuickStartTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddDeleteTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EntranceTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PaneLeftTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PaneTopTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RepositionTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BladeViewTransitions)));

                    ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = value;
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        public bool IsDisableStartupAnimation
        {
            get
            {
                return isDisableStartupAnimation || !isEnableAnimation;
            }
            set
            {
                if (value != isDisableStartupAnimation)
                {
                    isDisableStartupAnimation = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDisableStartupAnimation)));

                    ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] = value;
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        private bool isEnableAnimation;

        private bool isDisableStartupAnimation;

        public event PropertyChangedEventHandler PropertyChanged;

        private volatile static AnimationController Instance;

        private static readonly object Locker = new object();

        public static AnimationController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new AnimationController();
                }
            }
        }

        private AnimationController()
        {
            ApplicationData.Current.DataChanged += Current_DataChanged;

            if (ApplicationData.Current.LocalSettings.Values["EnableAnimation"] is bool Enable)
            {
                isEnableAnimation = Enable;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = true;
                isEnableAnimation = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] is bool StartupAnimation)
            {
                isDisableStartupAnimation = StartupAnimation;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] = false;
                isDisableStartupAnimation = false;
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IsEnableAnimation = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["EnableAnimation"]);

                IsDisableStartupAnimation = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"]);
            });
        }
    }
}
