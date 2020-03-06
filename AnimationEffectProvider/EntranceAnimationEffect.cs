using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace AnimationEffectProvider
{
    public class EntranceAnimationEffect
    {
        private readonly Page BasePage;

        private readonly UIElement UIToShow;

        private readonly Rect SplashScreenRect;

        public event EventHandler AnimationCompleted;

        public EntranceAnimationEffect(Page BasePage, UIElement UIToShow, Rect SplashScreenRect)
        {
            this.UIToShow = UIToShow;
            this.BasePage = BasePage;
            this.SplashScreenRect = SplashScreenRect;
            SurfaceLoader.Initialize(ElementCompositionPreview.GetElementVisual(BasePage).Compositor);
        }

        public async void PrepareEntranceEffect()
        {
            Compositor compositor = ElementCompositionPreview.GetElementVisual(BasePage).Compositor;
            Vector2 windowSize = new Vector2((float)Window.Current.Bounds.Width, (float)Window.Current.Bounds.Height);

            //1.创建ContainerVisual实例填充背景色和图片；
            //2.设置中心缩放
            ContainerVisual cv = compositor.CreateContainerVisual();
            cv.Size = windowSize;
            cv.CenterPoint = new Vector3(windowSize.X, windowSize.Y, 0) * 0.5f;
            ElementCompositionPreview.SetElementChildVisual(BasePage, cv);

            //创建sprite的背景色为APP的主题色
            SpriteVisual backgroundSprite = compositor.CreateSpriteVisual();
            backgroundSprite.Size = windowSize;
            backgroundSprite.Brush = compositor.CreateColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
            cv.Children.InsertAtBottom(backgroundSprite);

            //创建包含初始屏图像的sprite的尺寸和位置
            CompositionDrawingSurface surface = await SurfaceLoader.LoadFromUri(new Uri("ms-appx:///Assets/SplashScreen.png"));
            SpriteVisual imageSprite = compositor.CreateSpriteVisual();
            imageSprite.Brush = compositor.CreateSurfaceBrush(surface);
            imageSprite.Offset = new Vector3((float)SplashScreenRect.X, (float)SplashScreenRect.Y, 0f);
            imageSprite.Size = new Vector2((float)SplashScreenRect.Width, (float)SplashScreenRect.Height);
            cv.Children.InsertAtTop(imageSprite);
        }

        public void StartEntranceEffect()
        {
            ContainerVisual container = (ContainerVisual)ElementCompositionPreview.GetElementChildVisual(BasePage);
            Compositor compositor = container.Compositor;

            // 设置缩放和动画
            const float ScaleFactor = 20f;
            TimeSpan duration = TimeSpan.FromMilliseconds(1500);
            LinearEasingFunction linearEase = compositor.CreateLinearEasingFunction();
            CubicBezierEasingFunction easeInOut = compositor.CreateCubicBezierEasingFunction(new Vector2(.38f, 0f), new Vector2(.45f, 1f));

            // 创建淡出动画
            ScalarKeyFrameAnimation fadeOutAnimation = compositor.CreateScalarKeyFrameAnimation();
            fadeOutAnimation.InsertKeyFrame(1, 0);
            fadeOutAnimation.Duration = duration;

            // Grid的动画
            Vector2KeyFrameAnimation scaleUpGridAnimation = compositor.CreateVector2KeyFrameAnimation();
            scaleUpGridAnimation.InsertKeyFrame(0.1f, new Vector2(1 / ScaleFactor, 1 / ScaleFactor));
            scaleUpGridAnimation.InsertKeyFrame(1, new Vector2(1, 1));
            scaleUpGridAnimation.Duration = duration;

            // 初始屏动画
            Vector2KeyFrameAnimation scaleUpSplashAnimation = compositor.CreateVector2KeyFrameAnimation();
            scaleUpSplashAnimation.InsertKeyFrame(0, new Vector2(1, 1));
            scaleUpSplashAnimation.InsertKeyFrame(1, new Vector2(ScaleFactor, ScaleFactor));
            scaleUpSplashAnimation.Duration = duration;

            // 设置Grid的中心缩放视觉
            Visual gridVisual = ElementCompositionPreview.GetElementVisual(UIToShow);
            gridVisual.Size = UIToShow.ActualSize;
            gridVisual.CenterPoint = new Vector3(gridVisual.Size.X, gridVisual.Size.Y, 0) * .5f;

            // 创建一个视觉组，当改组所有视觉执行完后不再显示
            CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            container.StartAnimation("Opacity", fadeOutAnimation);
            container.StartAnimation("Scale.XY", scaleUpSplashAnimation);
            gridVisual.StartAnimation("Scale.XY", scaleUpGridAnimation);

            batch.Completed += (s, a) =>
            {
                ElementCompositionPreview.SetElementChildVisual(BasePage, null);
                SurfaceLoader.Uninitialize();
                AnimationCompleted?.Invoke(this, null);
            };
            batch.End();
        }
    }

    internal class SurfaceLoader
    {
        private static Compositor _compositor;
        private static CanvasDevice _canvasDevice;
        private static CompositionGraphicsDevice _compositionDevice;
        public delegate CompositionDrawingSurface LoadTimeEffectHandler(CanvasBitmap bitmap, CompositionGraphicsDevice device, Size sizeTarget);

        static public void Initialize(Compositor compositor)
        {
            if (!IsInitialized)
            {
                _compositor = compositor;
                _canvasDevice = new CanvasDevice();
                _compositionDevice = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

                IsInitialized = true;
            }
        }

        static public void Uninitialize()
        {
            _compositor = null;

            if (_compositionDevice != null)
            {
                _compositionDevice.Dispose();
                _compositionDevice = null;
            }

            if (_canvasDevice != null)
            {
                _canvasDevice.Dispose();
                _canvasDevice = null;
            }

            IsInitialized = false;
        }

        static public bool IsInitialized { get; private set; }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri)
        {
            return await LoadFromUri(uri, Size.Empty);
        }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri, Size sizeTarget)
        {
            CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, uri);
            Size sizeSource = bitmap.Size;

            if (sizeTarget.IsEmpty)
            {
                sizeTarget = sizeSource;
            }

            CompositionDrawingSurface surface = _compositionDevice.CreateDrawingSurface(sizeTarget, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(Color.FromArgb(0, 0, 0, 0));
                ds.DrawImage(bitmap, new Rect(0, 0, sizeTarget.Width, sizeTarget.Height), new Rect(0, 0, sizeSource.Width, sizeSource.Height));
            }

            return surface;
        }

        static public CompositionDrawingSurface LoadText(string text, Size sizeTarget, CanvasTextFormat textFormat, Color textColor, Color bgColor)
        {
            CompositionDrawingSurface surface = _compositionDevice.CreateDrawingSurface(sizeTarget, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);
            using (var ds = CanvasComposition.CreateDrawingSession(surface))
            {
                ds.Clear(bgColor);
                ds.DrawText(text, new Rect(0, 0, sizeTarget.Width, sizeTarget.Height), textColor, textFormat);
            }

            return surface;
        }

        static public async Task<CompositionDrawingSurface> LoadFromUri(Uri uri, Size sizeTarget, LoadTimeEffectHandler loadEffectHandler)
        {
            if (loadEffectHandler != null)
            {
                CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, uri);
                return loadEffectHandler(bitmap, _compositionDevice, sizeTarget);
            }
            else
            {
                return await LoadFromUri(uri, sizeTarget);
            }
        }
    }
}
