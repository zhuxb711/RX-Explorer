using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.DirectX;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace AnimationEffectProvider
{
    public class EntranceAnimationEffect
    {
        private readonly FrameworkElement BasePage;

        private readonly FrameworkElement UIToShow;

        private readonly Rect SplashScreenRect;

        private readonly float ScaleFactor;

        public event EventHandler AnimationCompleted;

        public EntranceAnimationEffect(FrameworkElement BasePage, FrameworkElement UIToShow, Rect SplashScreenRect, float ScaleFactor = 20f)
        {
            this.UIToShow = UIToShow;
            this.BasePage = BasePage;
            this.SplashScreenRect = SplashScreenRect;
            this.ScaleFactor = ScaleFactor;
            SurfaceLoader.Initialize(ElementCompositionPreview.GetElementVisual(BasePage).Compositor);
        }

        public async Task PrepareEntranceEffect()
        {
            try
            {
                Compositor BaseCompositor = ElementCompositionPreview.GetElementVisual(BasePage).Compositor;
                Vector2 WindowSize = new Vector2(Convert.ToSingle(Window.Current.Bounds.Width), Convert.ToSingle(Window.Current.Bounds.Height));

                ContainerVisual Visual = BaseCompositor.CreateContainerVisual();
                Visual.Size = WindowSize;
                Visual.CenterPoint = new Vector3(WindowSize.X, WindowSize.Y, 0) * 0.5f;
                ElementCompositionPreview.SetElementChildVisual(BasePage, Visual);

                SpriteVisual BackgroundSprite = BaseCompositor.CreateSpriteVisual();
                BackgroundSprite.Size = WindowSize;

                //For Win11 only
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 11))
                {
                    if (Application.Current.Resources["ApplicationPageBackgroundThemeBrush"] is SolidColorBrush Brush)
                    {
                        BackgroundSprite.Brush = BaseCompositor.CreateColorBrush(Brush.Color);
                    }
                    else
                    {
                        BackgroundSprite.Brush = BaseCompositor.CreateColorBrush();
                    }
                }
                else
                {
                    BackgroundSprite.Brush = BaseCompositor.CreateColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
                }

                Visual.Children.InsertAtBottom(BackgroundSprite);

                CompositionDrawingSurface ImageDrawingSurface = await SurfaceLoader.LoadFromUriAsync(new Uri("ms-appx:///Assets/SplashScreen.png"));

                SpriteVisual ImageSprite = BaseCompositor.CreateSpriteVisual();
                ImageSprite.Brush = BaseCompositor.CreateSurfaceBrush(ImageDrawingSurface);
                ImageSprite.Offset = new Vector3(Convert.ToSingle(SplashScreenRect.X), Convert.ToSingle(SplashScreenRect.Y), 0f);
                ImageSprite.Size = new Vector2(Convert.ToSingle(SplashScreenRect.Width), Convert.ToSingle(SplashScreenRect.Height));

                Visual.Children.InsertAtTop(ImageSprite);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in PrepareEntranceEffect, message:{ex.Message}");
            }
        }

        public void StartEntranceEffect()
        {
            try
            {
                TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(1000);

                ContainerVisual Container = (ContainerVisual)ElementCompositionPreview.GetElementChildVisual(BasePage);

                ScalarKeyFrameAnimation FadeOutAnimation = Container.Compositor.CreateScalarKeyFrameAnimation();
                FadeOutAnimation.InsertKeyFrame(0, 1);
                FadeOutAnimation.InsertKeyFrame(1, 0);
                FadeOutAnimation.Duration = AnimationDuration;

                Vector2KeyFrameAnimation ScaleUIAnimation = Container.Compositor.CreateVector2KeyFrameAnimation();
                ScaleUIAnimation.InsertKeyFrame(0.1f, new Vector2(1 / ScaleFactor, 1 / ScaleFactor));
                ScaleUIAnimation.InsertKeyFrame(1, new Vector2(1, 1));
                ScaleUIAnimation.Duration = AnimationDuration;

                Vector2KeyFrameAnimation ScaleSplashAnimation = Container.Compositor.CreateVector2KeyFrameAnimation();
                ScaleSplashAnimation.InsertKeyFrame(0, new Vector2(1, 1));
                ScaleSplashAnimation.InsertKeyFrame(1, new Vector2(ScaleFactor, ScaleFactor));
                ScaleSplashAnimation.Duration = AnimationDuration;

                Visual UIVisual = ElementCompositionPreview.GetElementVisual(UIToShow);
                UIVisual.Size = new Vector2((float)UIToShow.ActualWidth, (float)UIToShow.ActualHeight);
                UIVisual.CenterPoint = new Vector3(UIVisual.Size.X, UIVisual.Size.Y, 0) * 0.5f;

                CompositionScopedBatch BatchAnimation = Container.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                Container.StartAnimation("Opacity", FadeOutAnimation);
                Container.StartAnimation("Scale.XY", ScaleSplashAnimation);
                UIVisual.StartAnimation("Scale.XY", ScaleUIAnimation);

                BatchAnimation.Completed += (s, a) =>
                {
                    ElementCompositionPreview.SetElementChildVisual(BasePage, null);
                    SurfaceLoader.Uninitialize();
                    AnimationCompleted?.Invoke(this, null);
                };

                BatchAnimation.End();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StartEntranceEffect, message:{ex.Message}");
            }
        }
    }

    internal class SurfaceLoader
    {
        private static Compositor _compositor;
        private static CanvasDevice _canvasDevice;
        private static CompositionGraphicsDevice _compositionDevice;
        public delegate CompositionDrawingSurface LoadTimeEffectHandler(CanvasBitmap bitmap, CompositionGraphicsDevice device, Size sizeTarget);

        public static void Initialize(Compositor compositor)
        {
            if (!IsInitialized)
            {
                _compositor = compositor;
                _canvasDevice = new CanvasDevice();
                _compositionDevice = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);

                IsInitialized = true;
            }
        }

        public static void Uninitialize()
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

        public static bool IsInitialized { get; private set; }

        public static async Task<CompositionDrawingSurface> LoadFromUriAsync(Uri uri)
        {
            CanvasBitmap CBitmap = await CanvasBitmap.LoadAsync(_canvasDevice, uri);

            CompositionDrawingSurface DrawingSurface = _compositionDevice.CreateDrawingSurface(CBitmap.Size, DirectXPixelFormat.B8G8R8A8UIntNormalized, DirectXAlphaMode.Premultiplied);

            using (CanvasDrawingSession DraingSession = CanvasComposition.CreateDrawingSession(DrawingSurface))
            {
                DraingSession.Clear(Color.FromArgb(0, 0, 0, 0));
                DraingSession.DrawImage(CBitmap, new Rect(0, 0, CBitmap.Size.Width, CBitmap.Size.Height));
            }

            return DrawingSurface;
        }
    }
}
