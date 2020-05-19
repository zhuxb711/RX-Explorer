using System.ComponentModel;
using Windows.Storage;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;

namespace FileManager.Class
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

        public TransitionCollection PresenterGridViewTransitions
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

        public TransitionCollection PresenterListViewTransitions
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
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceAndLibraryTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QuickStartTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PresenterGridViewTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PresenterListViewTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddDeleteTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EntranceTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PaneLeftTransitions)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PaneTopTransitions)));

                    ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = value;
                }
            }
        }

        private bool isEnableAnimation;

        public event PropertyChangedEventHandler PropertyChanged;

        private volatile static AnimationController Instance;

        private static readonly object Locker = new object();

        public static AnimationController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ?? (Instance = new AnimationController());
                }
            }
        }

        private AnimationController()
        {
            if(ApplicationData.Current.LocalSettings.Values["EnableAnimation"] is bool Enable)
            {
                isEnableAnimation = Enable;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableAnimation"] = true;
                isEnableAnimation = true;
            }
        }
    }
}
