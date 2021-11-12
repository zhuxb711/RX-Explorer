using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers;
using ShareClassLibrary;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    /// <summary>
    /// 提供对全局背景的控制功能
    /// </summary>
    public class BackgroundController : INotifyPropertyChanged
    {
        private readonly AcrylicBrush CustomAcrylicBackgroundBrush;

        private readonly AcrylicBrush DefaultAcrylicBackgroundBrush;

        private CompositionEffectBrush CompositionAcrylicBrush;

        private event EventHandler CompositionAcrylicPresenterWasSetEvent;

        private UIElement CompositionAcrylicPresenter;

        public bool IsCompositionAcrylicEnabled
        {
            get
            {
                return isCompositionAcrylicEnabled;
            }
            set
            {
                if (isCompositionAcrylicEnabled != value)
                {
                    if (value)
                    {
                        EnableCompositionAcrylic();
                    }
                    else
                    {
                        DisableCompositionAcrylic();
                    }

                    isCompositionAcrylicEnabled = value;

                    OnPropertyChanged();
                }
            }
        }

        public bool IsMicaEffectEnabled
        {
            get
            {
                return isMicaEffectEnabled;
            }
            set
            {
                if (isMicaEffectEnabled != value)
                {
                    isMicaEffectEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool isMicaEffectEnabled;
        private bool isCompositionAcrylicEnabled;
        private BackgroundBrushType currentType;

        /// <summary>
        /// 图片背景刷
        /// </summary>
        private readonly ImageBrush PictureBackgroundBrush = new ImageBrush
        {
            Stretch = Stretch.UniformToFill
        };

        /// <summary>
        /// 纯色背景刷
        /// </summary>
        private readonly SolidColorBrush SolidColorBackgroundBrush;

        /// <summary>
        /// 必应背景刷
        /// </summary>
        private readonly ImageBrush BingPictureBursh = new ImageBrush
        {
            Stretch = Stretch.UniformToFill
        };

        /// <summary>
        /// 指示当前的背景类型
        /// </summary>
        public BackgroundBrushType CurrentType
        {
            get
            {
                return currentType;
            }
            private set
            {
                currentType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundBrush));
            }
        }

        /// <summary>
        /// 对外统一提供背景
        /// </summary>
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
                else
                {
                    return 0;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] = value;
                OnPropertyChanged();
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
                else
                {
                    return 0;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"] = value;
                OnPropertyChanged();
            }
        }

        private static BackgroundController Instance;

        private static readonly object Locker = new object();

        public static Color SolidColor_WhiteTheme { get; } = Colors.White;

        public static Color SolidColor_BlackTheme { get; } = "#1E1E1E".ToColor();

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly UISettings UIS;

        /// <summary>
        /// 获取背景控制器的实例
        /// </summary>
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

        /// <summary>
        /// 初始化BackgroundController对象
        /// </summary>
        private BackgroundController()
        {
            UIS = new UISettings();
            UIS.ColorValuesChanged += UIS_ColorValuesChanged;

            ApplicationData.Current.DataChanged += Current_DataChanged;

            if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
            {
                SolidColorBackgroundBrush = new SolidColorBrush(ColorType.ToColor());
            }
            else
            {
                if (UIS.GetColorValue(UIColorType.Background) == Colors.White)
                {
                    SolidColorBackgroundBrush = new SolidColorBrush(SolidColor_WhiteTheme);
                }
                else
                {
                    SolidColorBackgroundBrush = new SolidColorBrush(SolidColor_BlackTheme);
                }
            }

            DefaultAcrylicBackgroundBrush = new AcrylicBrush
            {
                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                TintColor = Colors.SlateGray,
                TintOpacity = 0.4,
                FallbackColor = Colors.DimGray
            };

            if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] is double TintOpacity)
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] is double TintLuminosity)
                {
                    CustomAcrylicBackgroundBrush = new AcrylicBrush
                    {
                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? Color.ToColor() : Colors.SlateGray,
                        TintOpacity = 1 - TintOpacity,
                        TintLuminosityOpacity = 1 - TintLuminosity,
                        FallbackColor = Colors.DimGray
                    };
                }
                else
                {
                    CustomAcrylicBackgroundBrush = new AcrylicBrush
                    {
                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? Color.ToColor() : Colors.SlateGray,
                        TintOpacity = 1 - TintOpacity,
                        FallbackColor = Colors.DimGray
                    };
                }
            }
            else
            {
                CustomAcrylicBackgroundBrush = new AcrylicBrush
                {
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    TintColor = Colors.SlateGray,
                    TintOpacity = 0.4,
                    FallbackColor = Colors.DimGray
                };
            }

            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is int ModeIndex)
            {
                switch (ModeIndex)
                {
                    case 0:
                        {
                            CurrentType = BackgroundBrushType.DefaultAcrylic;
                            break;
                        }
                    case 1:
                        {
                            CurrentType = BackgroundBrushType.SolidColor;

                            if (SolidColorBackgroundBrush.Color == Colors.White && AppThemeController.Current.Theme == ElementTheme.Dark)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Light;
                            }
                            else if (SolidColorBackgroundBrush.Color == SolidColor_BlackTheme && AppThemeController.Current.Theme == ElementTheme.Light)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Dark;
                            }

                            break;
                        }
                    case 2:
                        {
                            if (ApplicationData.Current.LocalSettings.Values["CustomUISubModeType"] is string SubMode)
                            {
                                CurrentType = Enum.Parse<BackgroundBrushType>(SubMode);

                                if (CurrentType == BackgroundBrushType.CustomAcrylic)
                                {
                                    IsCompositionAcrylicEnabled = SettingPage.PreventAcrylicFallbackEnabled;
                                }
                            }
                            else
                            {
                                CurrentType = BackgroundBrushType.CustomAcrylic;
                            }

                            break;
                        }
                    default:
                        {
                            IsMicaEffectEnabled = true;
                            CurrentType = BackgroundBrushType.Mica;
                            AppThemeController.Current.SyncAndSetSystemTheme();
                            break;
                        }
                }
            }
            else
            {
                CustomAcrylicBackgroundBrush = new AcrylicBrush
                {
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    TintColor = Colors.SlateGray,
                    TintOpacity = 0.4,
                    FallbackColor = Colors.DimGray
                };

                CurrentType = BackgroundBrushType.DefaultAcrylic;
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] is double Luminosity)
                {
                    TintLuminosityOpacity = Luminosity;
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] is double Opacity)
                {
                    TintOpacity = Opacity;
                }

                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                {
                    this.AcrylicColor = AcrylicColor.ToColor();
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] is float BlurValue)
                {
                    BackgroundBlur = BlurValue;
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundLightValue"] is float LightnessValue)
                {
                    BackgroundLightness = LightnessValue;
                }
            });
        }

        private async void UIS_ColorValuesChanged(UISettings sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (CurrentType)
                {
                    case BackgroundBrushType.SolidColor when !ApplicationData.Current.LocalSettings.Values.ContainsKey("SolidColorType"):
                        {
                            if (sender.GetColorValue(UIColorType.Background) == Colors.White)
                            {
                                SolidColorBackgroundBrush.Color = SolidColor_WhiteTheme;
                            }
                            else
                            {
                                SolidColorBackgroundBrush.Color = SolidColor_BlackTheme;
                            }

                            AppThemeController.Current.SyncAndSetSystemTheme();

                            OnPropertyChanged(nameof(BackgroundBrush));

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
                                BitmapImage Bitmap = new BitmapImage();

                                PictureBackgroundBrush.ImageSource = Bitmap;

                                try
                                {
                                    StorageFile File = await StorageFile.GetFileFromApplicationUriAsync(new Uri(UriString));

                                    using (IRandomAccessStream Stream = await File.OpenReadAsync())
                                    {
                                        await Bitmap.SetSourceAsync(Stream);
                                    }

                                    OnPropertyChanged(nameof(BackgroundBrush));
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
                                BitmapImage Bitmap = new BitmapImage();

                                BingPictureBursh.ImageSource = Bitmap;

                                using (IRandomAccessStream Stream = await ImageFile.GetRandomAccessStreamFromFileAsync(AccessMode.Read))
                                {
                                    await Bitmap.SetSourceAsync(Stream);
                                }

                                OnPropertyChanged(nameof(BackgroundBrush));
                            }
                            else
                            {
                                LogTracer.Log("Download Bing picture failed, BackgroundController.Initialize is not finished");
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Exception happend when loading image for background");
            }
        }

        private void EnableCompositionAcrylic()
        {
            if (CompositionAcrylicBrush == null)
            {
                CompositionAcrylicPresenterWasSetEvent += BackgroundController_AcrylicPresenterWasSet;

                if (CompositionAcrylicPresenter != null)
                {
                    CompositionAcrylicPresenterWasSetEvent?.Invoke(this, null);
                }
            }
        }

        private void BackgroundController_AcrylicPresenterWasSet(object sender, EventArgs e)
        {
            Visual ElementVisual = ElementCompositionPreview.GetElementVisual(CompositionAcrylicPresenter);
            Compositor VisualCompositor = ElementVisual.Compositor;

            CompositionBackdropBrush BackdropBrush = VisualCompositor.CreateHostBackdropBrush();

            float AcrylicAmount = Convert.ToSingle(TintLuminosityOpacity);

            GaussianBlurEffect BlurEffect = new GaussianBlurEffect()
            {
                BlurAmount = 10f,
                BorderMode = EffectBorderMode.Hard,
                Optimization = EffectOptimization.Balanced,
                Source = new ArithmeticCompositeEffect
                {
                    Name = "Mix",
                    MultiplyAmount = 0,
                    Source1 = new CompositionEffectSourceParameter("backdropBrush"),
                    Source2 = new ColorSourceEffect
                    {
                        Name = "Tint",
                        Color = (Color)CustomAcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty)
                    },
                    Source1Amount = AcrylicAmount,
                    Source2Amount = 1 - AcrylicAmount
                }
            };

            CompositionAcrylicBrush = VisualCompositor.CreateEffectFactory(BlurEffect, new[] { "Mix.Source1Amount", "Mix.Source2Amount", "Tint.Color" }).CreateBrush();
            CompositionAcrylicBrush.SetSourceParameter("backdropBrush", BackdropBrush);

            SpriteVisual SpVisual = VisualCompositor.CreateSpriteVisual();
            SpVisual.Brush = CompositionAcrylicBrush;

            ElementCompositionPreview.SetElementChildVisual(CompositionAcrylicPresenter, SpVisual);
            ExpressionAnimation bindSizeAnimation = VisualCompositor.CreateExpressionAnimation("ElementVisual.Size");
            bindSizeAnimation.SetReferenceParameter("ElementVisual", ElementVisual);
            SpVisual.StartAnimation("Size", bindSizeAnimation);

            OnPropertyChanged(nameof(TintLuminosityOpacity));
            OnPropertyChanged(nameof(AcrylicColor));
        }

        private void DisableCompositionAcrylic()
        {
            if (CompositionAcrylicBrush != null)
            {
                CompositionAcrylicPresenterWasSetEvent -= BackgroundController_AcrylicPresenterWasSet;

                CompositionAcrylicBrush.Dispose();
                CompositionAcrylicBrush = null;
            }
        }

        public void SetAcrylicEffectPresenter(UIElement Element)
        {
            CompositionAcrylicPresenter = Element ?? throw new ArgumentNullException(nameof(Element), "Argument could not be null");
            CompositionAcrylicPresenterWasSetEvent?.Invoke(this, null);
        }

        /// <summary>
        /// 提供颜色透明度的值
        /// </summary>
        public double TintOpacity
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] is double Opacity)
                {
                    return Opacity;
                }
                else
                {
                    return 0.4;
                }
            }
            set
            {
                CustomAcrylicBackgroundBrush.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacityValue"] = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 提供背景光透过率的值
        /// </summary>
        public double TintLuminosityOpacity
        {
            get
            {
                if (IsCompositionAcrylicEnabled)
                {
                    if (CompositionAcrylicBrush != null)
                    {
                        if (CompositionAcrylicBrush.Properties.TryGetScalar("Mix.Source1Amount", out float Value) == CompositionGetValueStatus.Succeeded)
                        {
                            return Value;
                        }
                        else
                        {
                            return 0;
                        }
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] is double Opacity)
                    {
                        return Opacity;
                    }
                    else
                    {
                        return 0.8;
                    }
                }
            }
            set
            {
                if (IsCompositionAcrylicEnabled && CompositionAcrylicBrush != null)
                {
                    CompositionAcrylicBrush.Properties.InsertScalar("Mix.Source1Amount", Convert.ToSingle(value));
                    CompositionAcrylicBrush.Properties.InsertScalar("Mix.Source2Amount", 1 - Convert.ToSingle(value));
                }

                CustomAcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosityValue"] = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 提供主题色的值
        /// </summary>
        public Color AcrylicColor
        {
            get
            {
                if (IsCompositionAcrylicEnabled)
                {
                    if (CompositionAcrylicBrush != null)
                    {
                        if (CompositionAcrylicBrush.Properties.TryGetColor("Tint.Color", out Color Value) == CompositionGetValueStatus.Succeeded)
                        {
                            return Value;
                        }
                        else
                        {
                            return Colors.SlateGray;
                        }
                    }
                    else
                    {
                        return Colors.SlateGray;
                    }
                }
                else
                {
                    return (Color)CustomAcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty);
                }
            }
            set
            {
                if (IsCompositionAcrylicEnabled && CompositionAcrylicBrush != null)
                {
                    if (CompositionAcrylicBrush.Properties.TryGetColor("Tint.Color", out Color Value) == CompositionGetValueStatus.Succeeded && Value.ToHex() != value.ToHex())
                    {
                        CompositionAcrylicBrush.Properties.InsertColor("Tint.Color", value);
                    }
                }

                if (CustomAcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty) is Color CurrentColor && CurrentColor.ToHex() != value.ToHex())
                {
                    CustomAcrylicBackgroundBrush.SetValue(AcrylicBrush.TintColorProperty, value);
                    ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = value.ToHex();

                    OnPropertyChanged();
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        /// <summary>
        /// 使用此方法以切换背景类型
        /// </summary>
        /// <param name="Type">背景类型</param>
        /// <param name="ImageUri">图片背景的Uri</param>
        public void SwitchTo(BackgroundBrushType Type, BitmapImage Background = null, Uri ImageUri = null, Color? Color = null)
        {
            CurrentType = Type;

            switch (Type)
            {
                case BackgroundBrushType.DefaultAcrylic:
                    {
                        IsMicaEffectEnabled = false;
                        IsCompositionAcrylicEnabled = false;

                        AppThemeController.Current.Theme = ElementTheme.Dark;

                        break;
                    }
                case BackgroundBrushType.CustomAcrylic:
                    {
                        IsMicaEffectEnabled = false;
                        IsCompositionAcrylicEnabled = false;

                        AppThemeController.Current.Theme = ElementTheme.Dark;
                        ApplicationData.Current.LocalSettings.Values["CustomUISubModeType"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.CustomAcrylic);

                        break;
                    }
                case BackgroundBrushType.Picture:
                    {
                        IsMicaEffectEnabled = false;
                        IsCompositionAcrylicEnabled = false;

                        PictureBackgroundBrush.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), $"if parameter: '{nameof(Type)}' is '{nameof(BackgroundBrushType.Picture)}', parameter: '{nameof(Background)}' could not be null");

                        ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = Convert.ToString(ImageUri);
                        ApplicationData.Current.LocalSettings.Values["CustomUISubModeType"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

                        break;
                    }
                case BackgroundBrushType.BingPicture:
                    {
                        IsMicaEffectEnabled = false;
                        IsCompositionAcrylicEnabled = false;

                        BingPictureBursh.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), $"if parameter: '{nameof(Type)}' is '{nameof(BackgroundBrushType.BingPicture)}', parameter: '{nameof(Background)}' could not be null");

                        ApplicationData.Current.LocalSettings.Values["CustomUISubModeType"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.BingPicture);

                        break;
                    }
                case BackgroundBrushType.SolidColor:
                    {
                        IsMicaEffectEnabled = false;
                        IsCompositionAcrylicEnabled = false;

                        if (Color == null)
                        {
                            ApplicationData.Current.LocalSettings.Values.Remove("SolidColorType");
                            AppThemeController.Current.SyncAndSetSystemTheme();
                        }
                        else
                        {
                            SolidColorBackgroundBrush.Color = Color.GetValueOrDefault();
                            AppThemeController.Current.Theme = Color == SolidColor_WhiteTheme ? ElementTheme.Light : ElementTheme.Dark;
                            ApplicationData.Current.LocalSettings.Values["SolidColorType"] = Color.GetValueOrDefault().ToString();
                        }

                        break;
                    }
                case BackgroundBrushType.Mica:
                    {
                        IsMicaEffectEnabled = true;
                        IsCompositionAcrylicEnabled = false;

                        AppThemeController.Current.SyncAndSetSystemTheme();

                        break;
                    }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
