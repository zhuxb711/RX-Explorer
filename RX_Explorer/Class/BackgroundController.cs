using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers;
using PropertyChanged;
using RX_Explorer.View;
using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class BackgroundController
    {
        private static BackgroundController Instance;
        private static readonly object Locker = new object();
        private static readonly UISettings Settings = new UISettings();

        private UIElement CompositionAcrylicPresenter;
        private CompositionEffectBrush CompositionAcrylicBrush;

        public event EventHandler<BackgroundBrushType> BackgroundTypeChanged;

        public Color WhiteThemeColor { get; } = Colors.White;

        public Color BlackThemeColor { get; } = "#1E1E1E".ToColor();

        private ImageBrush BingPictureBursh { get; set; }

        private ImageBrush PictureBackgroundBrush { get; set; }

        private SolidColorBrush SolidColorBackgroundBrush { get; set; }

        private AcrylicBrush CustomAcrylicBackgroundBrush { get; set; }

        private AcrylicBrush DefaultAcrylicBackgroundBrush { get; set; }

        public bool IsMicaEffectEnabled { get; set; }

        [OnChangedMethod(nameof(OnIsCompositionAcrylicBackgroundEnabledChanged))]
        public bool IsCompositionAcrylicBackgroundEnabled { get; set; }

        public double TintOpacity
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] is double Opacity)
                {
                    return Opacity;
                }

                return 0.4;
            }
            set
            {
                CustomAcrylicBackgroundBrush?.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public double TintLuminosityOpacity
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] is double Luminosity)
                {
                    return Luminosity;
                }

                return 0.8;
            }
            set
            {
                CompositionAcrylicBrush?.Properties.InsertScalar("Mix.Source1Amount", Convert.ToSingle(value));
                CompositionAcrylicBrush?.Properties.InsertScalar("Mix.Source2Amount", 1 - Convert.ToSingle(value));
                CustomAcrylicBackgroundBrush?.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public Color AcrylicColor
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color)
                {
                    return Color.ToColor();
                }

                return Colors.SlateGray;
            }
            set
            {
                CompositionAcrylicBrush?.Properties.InsertColor("Tint.Color", value);
                CustomAcrylicBackgroundBrush?.SetValue(AcrylicBrush.TintColorProperty, value);
                ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = value.ToHex();
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public BackgroundBrushType CurrentType
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["UIModeType"] is string UIMode)
                {
                    return Enum.Parse<BackgroundBrushType>(UIMode);
                }

                return BackgroundBrushType.DefaultAcrylic;
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values["UIModeType"] = Enum.GetName(typeof(BackgroundBrushType), value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        [DependsOn(nameof(CurrentType),
                   nameof(DefaultAcrylicBackgroundBrush),
                   nameof(CustomAcrylicBackgroundBrush),
                   nameof(PictureBackgroundBrush),
                   nameof(BingPictureBursh),
                   nameof(SolidColorBackgroundBrush))]
        public Brush BackgroundBrush
        {
            get
            {
                switch (CurrentType)
                {
                    case BackgroundBrushType.DefaultAcrylic:
                        {
                            return DefaultAcrylicBackgroundBrush;
                        }
                    case BackgroundBrushType.CustomAcrylic:
                        {
                            return CustomAcrylicBackgroundBrush;
                        }
                    case BackgroundBrushType.Picture:
                        {
                            return PictureBackgroundBrush;
                        }
                    case BackgroundBrushType.BingPicture:
                        {
                            return BingPictureBursh;
                        }
                    default:
                        {
                            return SolidColorBackgroundBrush;
                        }
                }
            }
        }

        public double BackgroundBlur
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] is double BlurValue)
                {
                    return BlurValue;
                }

                return 0;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public double BackgroundLightness
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"] is double LightValue)
                {
                    return LightValue;
                }

                return 0;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"] = value;
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static BackgroundController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new BackgroundController();
                }
            }
        }

        private void OnIsCompositionAcrylicBackgroundEnabledChanged()
        {
            CompositionAcrylicBrush?.Dispose();

            if (IsCompositionAcrylicBackgroundEnabled)
            {
                CompositionAcrylicBrush = GenerateAndSetCompositionAcrylicBrush(CompositionAcrylicPresenter);
            }
        }

        private BackgroundController()
        {
            BingPictureBursh = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };

            PictureBackgroundBrush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill
            };

            DefaultAcrylicBackgroundBrush = new AcrylicBrush
            {
                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                TintColor = Colors.SlateGray,
                TintOpacity = 0.4,
                FallbackColor = Colors.DimGray
            };

            CustomAcrylicBackgroundBrush = new AcrylicBrush
            {
                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                TintColor = AcrylicColor,
                TintOpacity = 1 - TintOpacity,
                TintLuminosityOpacity = 1 - TintLuminosityOpacity,
                FallbackColor = Colors.DimGray
            };

            if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
            {
                SolidColorBackgroundBrush = new SolidColorBrush(ColorType.ToColor());
            }
            else if (Settings.GetColorValue(UIColorType.Background) == Colors.White)
            {
                SolidColorBackgroundBrush = new SolidColorBrush(WhiteThemeColor);
            }
            else
            {
                SolidColorBackgroundBrush = new SolidColorBrush(BlackThemeColor);
            }

            Settings.ColorValuesChanged += Settings_ColorValuesChanged;
            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(TintOpacity));
                    OnPropertyChanged(nameof(TintLuminosityOpacity));
                    OnPropertyChanged(nameof(AcrylicColor));
                    OnPropertyChanged(nameof(BackgroundBlur));
                    OnPropertyChanged(nameof(BackgroundLightness));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private async void Settings_ColorValuesChanged(UISettings sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (CurrentType)
                {
                    case BackgroundBrushType.SolidColor when !ApplicationData.Current.LocalSettings.Values.ContainsKey("SolidColorType"):
                        {
                            if (sender.GetColorValue(UIColorType.Background) == Colors.White)
                            {
                                SolidColorBackgroundBrush = new SolidColorBrush(WhiteThemeColor);
                            }
                            else
                            {
                                SolidColorBackgroundBrush = new SolidColorBrush(BlackThemeColor);
                            }

                            AppThemeController.Current.SyncAndSetSystemTheme();
                            break;
                        }
                    case BackgroundBrushType.Mica:
                        {
                            AppThemeController.Current.SyncAndSetSystemTheme();
                            break;
                        }
                }
            });
        }

        public async Task InitializeAsync()
        {
            try
            {
                switch (CurrentType)
                {
                    case BackgroundBrushType.Picture:
                        {
                            string UriString = Convert.ToString(ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"]);

                            if (!string.IsNullOrEmpty(UriString))
                            {
                                try
                                {
                                    StorageFile File = await StorageFile.GetFileFromApplicationUriAsync(new Uri(UriString));

                                    using (IRandomAccessStream Stream = await File.OpenReadAsync())
                                    {
                                        PictureBackgroundBrush = new ImageBrush
                                        {
                                            Stretch = Stretch.UniformToFill,
                                            ImageSource = await Helper.CreateBitmapImageAsync(Stream)
                                        };
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"PicturePath is \"{UriString}\" but could not found, {nameof(BackgroundController.InitializeAsync)} is not finished");
                                }
                            }
                            else
                            {
                                LogTracer.Log($"PicturePath is empty, {nameof(BackgroundController.InitializeAsync)} is not finished");
                            }

                            break;
                        }

                    case BackgroundBrushType.BingPicture:
                        {
                            if (await BingPictureDownloader.GetBingPictureAsync() is FileSystemStorageFile ImageFile)
                            {
                                using (Stream Stream = await ImageFile.GetStreamFromFileAsync(AccessMode.Read))
                                {
                                    BingPictureBursh = new ImageBrush
                                    {
                                        Stretch = Stretch.UniformToFill,
                                        ImageSource = await Helper.CreateBitmapImageAsync(Stream.AsRandomAccessStream())
                                    };
                                }
                            }
                            else
                            {
                                LogTracer.Log("Download Bing picture failed, BackgroundController.Initialize is not finished");
                            }

                            break;
                        }
                    case BackgroundBrushType.SolidColor:
                        {
                            if (SolidColorBackgroundBrush.Color == WhiteThemeColor && AppThemeController.Current.Theme == ElementTheme.Dark)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Light;
                            }
                            else if (SolidColorBackgroundBrush.Color == BlackThemeColor && AppThemeController.Current.Theme == ElementTheme.Light)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Dark;
                            }

                            break;
                        }
                    case BackgroundBrushType.CustomAcrylic:
                        {
                            IsCompositionAcrylicBackgroundEnabled = SettingPage.IsPreventAcrylicFallbackEnabled;
                            break;
                        }
                    case BackgroundBrushType.Mica:
                        {
                            IsMicaEffectEnabled = true;
                            AppThemeController.Current.SyncAndSetSystemTheme();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when loading initialize the background controller");
            }
        }

        private CompositionEffectBrush GenerateAndSetCompositionAcrylicBrush(UIElement Presenter)
        {
            if (Presenter != null)
            {
                Visual ElementVisual = ElementCompositionPreview.GetElementVisual(Presenter);

                CompositionEffectBrush Brush = ElementVisual.Compositor.CreateEffectFactory(new GaussianBlurEffect()
                {
                    BlurAmount = 10f,
                    BorderMode = EffectBorderMode.Hard,
                    Optimization = EffectOptimization.Balanced,
                    Source = new ArithmeticCompositeEffect
                    {
                        Name = "Mix",
                        MultiplyAmount = 0,
                        Source1 = new CompositionEffectSourceParameter("BackdropBrush"),
                        Source2 = new ColorSourceEffect
                        {
                            Name = "Tint",
                            Color = (Color)CustomAcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty)
                        },
                        Source1Amount = Convert.ToSingle(TintLuminosityOpacity),
                        Source2Amount = 1 - Convert.ToSingle(TintLuminosityOpacity)
                    }
                }, new string[] { "Mix.Source1Amount", "Mix.Source2Amount", "Tint.Color" }).CreateBrush();

                Brush.SetSourceParameter("BackdropBrush", ElementVisual.Compositor.CreateHostBackdropBrush());

                SpriteVisual SpVisual = ElementVisual.Compositor.CreateSpriteVisual();
                SpVisual.Brush = Brush;

                ElementCompositionPreview.SetElementChildVisual(Presenter, SpVisual);

                ExpressionAnimation BindSizeAnimation = ElementVisual.Compositor.CreateExpressionAnimation("ElementVisual.Size");
                BindSizeAnimation.SetReferenceParameter("ElementVisual", ElementVisual);
                SpVisual.StartAnimation("Size", BindSizeAnimation);

                return Brush;
            }

            return null;
        }

        public void SetAcrylicEffectPresenter(UIElement Element)
        {
            CompositionAcrylicPresenter = Element ?? throw new ArgumentNullException(nameof(Element), "Argument could not be null");

            if (IsCompositionAcrylicBackgroundEnabled)
            {
                CompositionAcrylicBrush?.Dispose();
                CompositionAcrylicBrush = GenerateAndSetCompositionAcrylicBrush(Element);
            }
        }

        public void SwitchTo(BackgroundBrushType Type, BitmapImage Background = null, Uri ImageUri = null, Color? Color = null)
        {
            try
            {
                CurrentType = Type;

                switch (Type)
                {
                    case BackgroundBrushType.DefaultAcrylic:
                        {
                            IsMicaEffectEnabled = false;
                            IsCompositionAcrylicBackgroundEnabled = false;

                            AppThemeController.Current.Theme = ElementTheme.Dark;
                            break;
                        }
                    case BackgroundBrushType.CustomAcrylic:
                        {
                            IsMicaEffectEnabled = false;

                            SettingPage.CustomModeSubType = BackgroundBrushType.CustomAcrylic;
                            AppThemeController.Current.Theme = ElementTheme.Dark;
                            break;
                        }
                    case BackgroundBrushType.Picture:
                        {
                            IsMicaEffectEnabled = false;
                            IsCompositionAcrylicBackgroundEnabled = false;
                            PictureBackgroundBrush.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), $"if parameter: '{nameof(Type)}' is '{nameof(BackgroundBrushType.Picture)}', parameter: '{nameof(Background)}' could not be null");

                            SettingPage.CustomModeSubType = BackgroundBrushType.Picture;
                            ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = Convert.ToString(ImageUri);
                            break;
                        }
                    case BackgroundBrushType.BingPicture:
                        {
                            IsMicaEffectEnabled = false;
                            IsCompositionAcrylicBackgroundEnabled = false;

                            BingPictureBursh.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), $"if parameter: '{nameof(Type)}' is '{nameof(BackgroundBrushType.BingPicture)}', parameter: '{nameof(Background)}' could not be null");

                            SettingPage.CustomModeSubType = BackgroundBrushType.BingPicture;
                            break;
                        }
                    case BackgroundBrushType.SolidColor:
                        {
                            IsMicaEffectEnabled = false;
                            IsCompositionAcrylicBackgroundEnabled = false;

                            if (Color == null)
                            {
                                if (Settings.GetColorValue(UIColorType.Background) == Colors.White)
                                {
                                    SolidColorBackgroundBrush.Color = WhiteThemeColor;
                                }
                                else
                                {
                                    SolidColorBackgroundBrush.Color = BlackThemeColor;
                                }

                                ApplicationData.Current.LocalSettings.Values.Remove("SolidColorType");
                                AppThemeController.Current.SyncAndSetSystemTheme();
                            }
                            else
                            {
                                SolidColorBackgroundBrush.Color = Color.GetValueOrDefault();
                                AppThemeController.Current.Theme = Color == WhiteThemeColor ? ElementTheme.Light : ElementTheme.Dark;
                                ApplicationData.Current.LocalSettings.Values["SolidColorType"] = Color.GetValueOrDefault().ToString();
                            }

                            break;
                        }
                    case BackgroundBrushType.Mica:
                        {
                            IsMicaEffectEnabled = true;
                            IsCompositionAcrylicBackgroundEnabled = false;

                            SettingPage.CustomModeSubType = BackgroundBrushType.Mica;
                            AppThemeController.Current.SyncAndSetSystemTheme();
                            break;
                        }
                }

                BackgroundTypeChanged.Invoke(this, Type);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not switch the background type");
            }
        }
    }
}
