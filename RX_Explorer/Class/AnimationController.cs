using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

        public event EventHandler<bool> AnimationStateChanged;

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

                    OnPropertyChanged(nameof(IsEnableAnimation));
                    OnPropertyChanged(nameof(DeviceAndLibraryTransitions));
                    OnPropertyChanged(nameof(QuickStartTransitions));
                    OnPropertyChanged(nameof(AddDeleteTransitions));
                    OnPropertyChanged(nameof(PaneTopTransitions));
                    OnPropertyChanged(nameof(RepositionTransitions));
                    OnPropertyChanged(nameof(ContentTransitions));

                    ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = value;
                    ApplicationData.Current.SignalDataChanged();

                    AnimationStateChanged?.Invoke(this, value);
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

                    OnPropertyChanged();

                    ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] = value;
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        public bool IsDisableSelectionAnimation
        {
            get
            {
                return isDisableSelectionAnimation || !isEnableAnimation;
            }
            set
            {
                if (value != isDisableSelectionAnimation)
                {
                    isDisableSelectionAnimation = value;

                    OnPropertyChanged();

                    ApplicationData.Current.LocalSettings.Values["IsDisableSelectionAnimation"] = value;
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        private bool isEnableAnimation;

        private bool isDisableStartupAnimation;

        private bool isDisableSelectionAnimation;

        public event PropertyChangedEventHandler PropertyChanged;

        private static AnimationController Instance;

        private static readonly object Locker = new object();

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
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

            if (ApplicationData.Current.LocalSettings.Values["EnableAnimation"] is bool Enable)
            {
                isEnableAnimation = Enable;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = true;
                isEnableAnimation = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] is bool DisableStartupAnimation)
            {
                isDisableStartupAnimation = DisableStartupAnimation;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"] = false;
                isDisableStartupAnimation = false;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDisableSelectionAnimation"] is bool DisableSelectionAnimation)
            {
                isDisableSelectionAnimation = DisableSelectionAnimation;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDisableSelectionAnimation"] = false;
                isDisableSelectionAnimation = false;
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IsEnableAnimation = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["EnableAnimation"]);
                IsDisableStartupAnimation = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["IsDisableStartupAnimation"]);
                IsDisableSelectionAnimation = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["IsDisableSelectionAnimation"]);
            });
        }
    }
}
