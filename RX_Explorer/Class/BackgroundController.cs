using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
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
        private BackgroundBrushType CurrentType;

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
            if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
            {
                SolidColorBackgroundBrush = new SolidColorBrush(GetColorFromHexString(ColorType));
            }
            else
            {
                SolidColorBackgroundBrush = new SolidColorBrush(Colors.White);
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

                            CurrentType = BackgroundBrushType.SolidColor;

                            if (SolidColorBackgroundBrush.Color == Colors.White && AppThemeController.Current.Theme == ElementTheme.Dark)
                            {
                                AppThemeController.Current.ChangeThemeTo(ElementTheme.Light);
                            }
                            else if (SolidColorBackgroundBrush.Color == Colors.Black && AppThemeController.Current.Theme == ElementTheme.Light)
                            {
                                AppThemeController.Current.ChangeThemeTo(ElementTheme.Dark);
                            }

                            break;
                        }
                    default:
                        {
                            if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903))
                            {
                                if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"]), out double TintOpacity))
                                {
                                    if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"]), out double TintLuminosity))
                                    {
                                        AcrylicBackgroundBrush = new AcrylicBrush
                                        {
                                            BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                            TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? GetColorFromHexString(Color) : Colors.LightSlateGray,
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
                                            TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? GetColorFromHexString(Color) : Colors.LightSlateGray,
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
                            }
                            else
                            {
                                if (double.TryParse(Convert.ToString(ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"]), out double TintOpacity))
                                {
                                    AcrylicBackgroundBrush = new AcrylicBrush
                                    {
                                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? GetColorFromHexString(Color) : Colors.LightSlateGray,
                                        TintOpacity = 1 - TintOpacity,
                                        FallbackColor = Colors.DimGray
                                    };
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
                            }

                            if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string SubMode)
                            {
                                CurrentType = (BackgroundBrushType)Enum.Parse(typeof(BackgroundBrushType), SubMode);
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
            }
        }

        public async Task Initialize()
        {
            switch (CurrentType)
            {
                case BackgroundBrushType.Picture:
                    {
                        string UriString = Convert.ToString(ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"]);
                        
                        BitmapImage Bitmap = new BitmapImage();

                        StorageFile ImageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(UriString));

                        using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                        {
                            await Bitmap.SetSourceAsync(Stream);
                        }

                        PictureBackgroundBrush.ImageSource = Bitmap;

                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
                        break;
                    }

                case BackgroundBrushType.BingPicture:
                    {
                        BitmapImage Bitmap = new BitmapImage();

                        StorageFile ImageFile = await BingPictureDownloader.DownloadDailyPicture().ConfigureAwait(true);

                        using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                        {
                            await Bitmap.SetSourceAsync(Stream);
                        }

                        BingPictureBursh.ImageSource = Bitmap;

                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
                        break;
                    }
            }
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
                AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = Convert.ToString(value);
            }
        }

        /// <summary>
        /// 提供背景光透过率的值
        /// </summary>
        public double TintLuminosityOpacity
        {
            get
            {
                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903))
                {
                    return 1 - Convert.ToDouble(AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintLuminosityOpacityProperty));
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903))
                {
                    if (value == -1)
                    {
                        AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, null);
                    }
                    else
                    {
                        AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                        ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = Convert.ToString(value);
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
                return (Color)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty);
            }
            set
            {
                AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintColorProperty, value);
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
                        if (ImageUri == null)
                        {
                            throw new ArgumentNullException(nameof(ImageUri), "if parameter: 'Type' is BackgroundBrushType.Picture, parameter: 'ImageUri' could not be null");
                        }

                        PictureBackgroundBrush.ImageSource = Background ?? throw new ArgumentNullException(nameof(Background), "if parameter: 'Type' is BackgroundBrushType.Picture, parameter: 'Background' could not be null");

                        ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = ImageUri.ToString();

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
                            throw new ArgumentNullException(nameof(Color), "if parameter: 'Type' is BackgroundBrushType.SolidColor, parameter: 'Color' could not be null");
                        }

                        SolidColorBackgroundBrush.Color = Color.GetValueOrDefault();

                        ApplicationData.Current.LocalSettings.Values["SolidColorType"] = Color.GetValueOrDefault().ToString();

                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        }

        /// <summary>
        /// 将16进制字符串转换成Color对象
        /// </summary>
        /// <param name="Hex">十六进制字符串</param>
        /// <returns></returns>
        public static Color GetColorFromHexString(string Hex)
        {
            if (string.IsNullOrWhiteSpace(Hex))
            {
                throw new ArgumentException("Hex could not be null or empty", nameof(Hex));
            }

            Hex = Hex.Replace("#", string.Empty);

            bool ExistAlpha = Hex.Length == 8 || Hex.Length == 4;
            bool IsDoubleHex = Hex.Length == 8 || Hex.Length == 6;

            if (!ExistAlpha && Hex.Length != 6 && Hex.Length != 3)
            {
                throw new ArgumentException("输入的hex不是有效颜色");
            }

            int n = 0;
            byte a;
            int HexCount = IsDoubleHex ? 2 : 1;
            if (ExistAlpha)
            {
                n = HexCount;
                a = (byte)ConvertHexToByte(Hex, 0, HexCount);
                if (!IsDoubleHex)
                {
                    a = (byte)(a * 16 + a);
                }
            }
            else
            {
                a = 0xFF;
            }

            var r = (byte)ConvertHexToByte(Hex, n, HexCount);
            var g = (byte)ConvertHexToByte(Hex, n + HexCount, HexCount);
            var b = (byte)ConvertHexToByte(Hex, n + 2 * HexCount, HexCount);
            if (!IsDoubleHex)
            {
                r = (byte)(r * 16 + r);
                g = (byte)(g * 16 + g);
                b = (byte)(b * 16 + b);
            }

            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// 将十六进制字符串转换成byte
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <param name="n">起始位置</param>
        /// <param name="count">长度</param>
        /// <returns></returns>
        private static uint ConvertHexToByte(string hex, int n, int count = 2)
        {
            return Convert.ToUInt32(hex.Substring(n, count), 16);
        }
    }
}
