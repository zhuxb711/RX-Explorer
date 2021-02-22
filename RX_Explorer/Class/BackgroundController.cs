using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.ComponentModel;
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
        /// <summary>
        /// 亚克力背景刷
        /// </summary>
        private readonly AcrylicBrush AcrylicBackgroundBrush;

        private CompositionEffectBrush CompositionAcrylicBrush;

        private event EventHandler CompositionAcrylicPresenterWasSetEvent;

        private UIElement CompositionAcrylicPresenter;

        private bool isCompositionAcrylicEnabled;
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

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompositionAcrylicEnabled)));
                }
            }
        }

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
        public BackgroundBrushType CurrentType { get; private set; }

        /// <summary>
        /// 对外统一提供背景
        /// </summary>
        public Brush BackgroundBrush
        {
            get
            {
                switch (CurrentType)
                {
                    case BackgroundBrushType.Acrylic:
                        {
                            return AcrylicBackgroundBrush;
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

        private volatile static BackgroundController Instance;

        private static readonly object Locker = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly UISettings UIS;

        private bool IsInitialized;

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
                    SolidColorBackgroundBrush = new SolidColorBrush(Colors.White);
                }
                else
                {
                    SolidColorBackgroundBrush = new SolidColorBrush("#1E1E1E".ToColor());
                }
            }

            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is int ModeIndex)
            {
                switch (ModeIndex)
                {
                    case 0:
                        {
                            AcrylicBackgroundBrush = new AcrylicBrush
                            {
                                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                TintColor = Colors.LightSlateGray,
                                TintOpacity = 0.4,
                                FallbackColor = Colors.DimGray
                            };

                            CurrentType = BackgroundBrushType.Acrylic;

                            break;
                        }
                    case 1:
                        {
                            AcrylicBackgroundBrush = new AcrylicBrush
                            {
                                BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                TintColor = Colors.LightSlateGray,
                                TintOpacity = 0.4,
                                FallbackColor = Colors.DimGray
                            };

                            if (SolidColorBackgroundBrush.Color == Colors.White && AppThemeController.Current.Theme == ElementTheme.Dark)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Light;
                            }
                            else if (SolidColorBackgroundBrush.Color == "#1E1E1E".ToColor() && AppThemeController.Current.Theme == ElementTheme.Light)
                            {
                                AppThemeController.Current.Theme = ElementTheme.Dark;
                            }

                            CurrentType = BackgroundBrushType.SolidColor;

                            break;
                        }
                    default:
                        {
                            if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"]), out double TintOpacity))
                            {
                                if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"]), out double TintLuminosity))
                                {
                                    AcrylicBackgroundBrush = new AcrylicBrush
                                    {
                                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? Color.ToColor() : Colors.LightSlateGray,
                                        TintOpacity = 1 - TintOpacity,
                                        TintLuminosityOpacity = 1 - TintLuminosity,
                                        FallbackColor = Colors.DimGray
                                    };
                                }
                                else
                                {
                                    AcrylicBackgroundBrush = new AcrylicBrush
                                    {
                                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? Color.ToColor() : Colors.LightSlateGray,
                                        TintOpacity = 1 - TintOpacity,
                                        FallbackColor = Colors.DimGray
                                    };
                                }
                            }
                            else
                            {
                                AcrylicBackgroundBrush = new AcrylicBrush
                                {
                                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                    TintColor = Colors.LightSlateGray,
                                    TintOpacity = 0.4,
                                    FallbackColor = Colors.DimGray
                                };
                            }

                            if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string SubMode)
                            {
                                CurrentType = Enum.Parse<BackgroundBrushType>(SubMode);

                                if (CurrentType == BackgroundBrushType.Acrylic && ApplicationData.Current.LocalSettings.Values["PreventFallBack"] is bool IsPrevent)
                                {
                                    IsCompositionAcrylicEnabled = IsPrevent;
                                }
                            }

                            break;
                        }
                }
            }
            else
            {
                AcrylicBackgroundBrush = new AcrylicBrush
                {
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    TintColor = Colors.LightSlateGray,
                    TintOpacity = 0.4,
                    FallbackColor = Colors.DimGray
                };

                CurrentType = BackgroundBrushType.Acrylic;

                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = 0.6;
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                {
                    if (double.TryParse(Luminosity, out double Result))
                    {
                        TintLuminosityOpacity = Result;
                    }
                    else
                    {
                        TintLuminosityOpacity = 0.8;
                    }
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                {
                    if (double.TryParse(Opacity, out double Result))
                    {
                        TintOpacity = Result;
                    }
                    else
                    {
                        TintOpacity = 0.6;
                    }
                }

                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                {
                    this.AcrylicColor = AcrylicColor.ToColor();
                }
            });
        }

        private async void UIS_ColorValuesChanged(UISettings sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("SolidColorType") && CurrentType == BackgroundBrushType.SolidColor)
                {
                    if (UIS.GetColorValue(UIColorType.Background) == Colors.White)
                    {
                        SolidColorBackgroundBrush.Color = Colors.White;

                        AppThemeController.Current.Theme = ElementTheme.Light;
                    }
                    else
                    {
                        SolidColorBackgroundBrush.Color = "#1E1E1E".ToColor();

                        AppThemeController.Current.Theme = ElementTheme.Dark;
                    }

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
                }
            });
        }

        public async Task Initialize()
        {
            if (!IsInitialized)
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

                                    StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(UriString));

                                    using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                                    {
                                        await Bitmap.SetSourceAsync(Stream);
                                    }

                                    PictureBackgroundBrush.ImageSource = Bitmap;

                                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
                                }
                                else
                                {
                                    LogTracer.Log("UriString is empty, BackgroundController.Initialize is not finished");
                                }

                                break;
                            }

                        case BackgroundBrushType.BingPicture:
                            {
                                BitmapImage Bitmap = new BitmapImage();

                                if (await BingPictureDownloader.GetBingPictureAsync().ConfigureAwait(true) is StorageFile ImageFile)
                                {
                                    using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                                    {
                                        await Bitmap.SetSourceAsync(Stream);
                                    }

                                    BingPictureBursh.ImageSource = Bitmap;

                                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
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
                finally
                {
                    IsInitialized = true;
                }
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

            float AcrylicAmount;

            if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"]), out double TintLuminosity))
            {
                AcrylicAmount = Convert.ToSingle(TintLuminosity);
            }
            else
            {
                AcrylicAmount = 0.8f;
            }

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
                        Color = (Color)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty)
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

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TintLuminosityOpacity)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AcrylicColor)));
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
            if (Element == null)
            {
                throw new ArgumentNullException(nameof(Element), "Argument could not be null");
            }

            CompositionAcrylicPresenter = Element;

            CompositionAcrylicPresenterWasSetEvent?.Invoke(this, null);
        }

        /// <summary>
        /// 提供颜色透明度的值
        /// </summary>
        public double TintOpacity
        {
            get
            {
                return 1 - Convert.ToDouble(AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintOpacityProperty));
            }
            set
            {
                double CurrentValue = Convert.ToDouble(AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintOpacityProperty));

                if (CurrentValue != 1 - value)
                {
                    AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                    ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = Convert.ToString(value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TintOpacity)));
                    ApplicationData.Current.SignalDataChanged();
                }
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
                    return 1 - Convert.ToDouble(AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintLuminosityOpacityProperty));
                }
            }
            set
            {
                if (IsCompositionAcrylicEnabled && CompositionAcrylicBrush != null)
                {
                    CompositionAcrylicBrush.Properties.InsertScalar("Mix.Source1Amount", Convert.ToSingle(value));
                    CompositionAcrylicBrush.Properties.InsertScalar("Mix.Source2Amount", 1 - Convert.ToSingle(value));
                }

                if (value == -1)
                {
                    AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, null);
                    ApplicationData.Current.LocalSettings.Values.Remove("BackgroundTintLuminosity");
                }
                else
                {
                    double CurrentValue = Convert.ToDouble(AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintLuminosityOpacityProperty));

                    if (CurrentValue != 1 - value)
                    {
                        AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                        ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = Convert.ToString(value);

                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TintLuminosityOpacity)));
                        ApplicationData.Current.SignalDataChanged();
                    }
                }
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
                            return Colors.DimGray;
                        }
                    }
                    else
                    {
                        return Colors.DimGray;
                    }
                }
                else
                {
                    return (Color)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty);
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

                if (AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty) is Color CurrentColor && CurrentColor.ToHex() != value.ToHex())
                {
                    AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintColorProperty, value);
                    ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = value.ToHex();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AcrylicColor)));
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        /// <summary>
        /// 使用此方法以切换背景类型
        /// </summary>
        /// <param name="Type">背景类型</param>
        /// <param name="uri">图片背景的Uri</param>
        public void SwitchTo(BackgroundBrushType Type, BitmapImage Background = null, Uri ImageUri = null, Color? Color = null)
        {
            CurrentType = Type;

            switch (Type)
            {
                case BackgroundBrushType.Picture:
                    {
                        PictureBackgroundBrush.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), "if parameter: 'Type' is BackgroundBrushType.Picture, parameter: 'Background' could not be null");
                        ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = ImageUri?.ToString();
                        break;
                    }
                case BackgroundBrushType.BingPicture:
                    {
                        BingPictureBursh.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), "if parameter: 'Type' is BackgroundBrushType.BingPicture, parameter: 'Background' could not be null");
                        break;
                    }
                case BackgroundBrushType.SolidColor:
                    {
                        if (Color == null)
                        {
                            ApplicationData.Current.LocalSettings.Values.Remove("SolidColorType");

                            if (UIS.GetColorValue(UIColorType.Background) == Colors.White)
                            {
                                SolidColorBackgroundBrush.Color = Colors.White;
                                AppThemeController.Current.Theme = ElementTheme.Light;
                            }
                            else
                            {
                                SolidColorBackgroundBrush.Color = "#1E1E1E".ToColor();
                                AppThemeController.Current.Theme = ElementTheme.Dark;
                            }
                        }
                        else
                        {
                            AppThemeController.Current.Theme = Color == Colors.White ? ElementTheme.Light : ElementTheme.Dark;

                            SolidColorBackgroundBrush.Color = Color.GetValueOrDefault();

                            ApplicationData.Current.LocalSettings.Values["SolidColorType"] = Color.GetValueOrDefault().ToString();
                        }

                        break;
                    }
                case BackgroundBrushType.Acrylic:
                    {
                        break;
                    }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        }
    }
}
