using PropertyChanged;
using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class AnimationController
    {
        private static AnimationController Instance;
        private static readonly object Locker = new object();

        public event EventHandler<bool> AnimationStateChanged;

        [DependsOn(nameof(IsEnableAnimation))]
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

        [DependsOn(nameof(IsEnableAnimation))]
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
                            Edge = EdgeTransitionLocation.Top
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

        [DependsOn(nameof(IsEnableAnimation))]
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

        [DependsOn(nameof(IsEnableAnimation))]
        public TransitionCollection ContentTransitions
        {
            get
            {
                if (IsEnableAnimation)
                {
                    return new TransitionCollection
                    {
                        new ContentThemeTransition()
                    };
                }
                else
                {
                    return new TransitionCollection();
                }
            }
        }

        [DependsOn(nameof(IsEnableAnimation))]
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

        [DependsOn(nameof(IsEnableAnimation))]
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
                if (ApplicationData.Current.LocalSettings.Values["EnableAnimation"] is bool Enabled)
                {
                    return Enabled;
                }

                return true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = value;
                ApplicationData.Current.SignalDataChanged();
                AnimationStateChanged?.Invoke(this, value);
            }
        }

        [DependsOn(nameof(IsEnableAnimation))]
        public bool IsEnableStartupAnimation
        {
            get
            {
                if (!IsEnableAnimation)
                {
                    return false;
                }

                if (ApplicationData.Current.LocalSettings.Values["IsEnableStartupAnimation"] is bool Enabled)
                {
                    return Enabled;
                }

                return true;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["IsEnableStartupAnimation"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        [DependsOn(nameof(IsEnableAnimation))]
        public bool IsEnableSelectionAnimation
        {
            get => IsEnableAnimation && Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["IsEnableSelectionAnimation"]);
            set
            {
                ApplicationData.Current.LocalSettings.Values["IsEnableSelectionAnimation"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

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
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    OnPropertyChanged(nameof(IsEnableAnimation));
                    OnPropertyChanged(nameof(IsEnableStartupAnimation));
                    OnPropertyChanged(nameof(IsEnableSelectionAnimation));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }
    }
}
