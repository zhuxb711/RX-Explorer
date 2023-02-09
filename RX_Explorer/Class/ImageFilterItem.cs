using ComputerVision;
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class ImageFilterItem : IDisposable
    {
        public string Text { get; private set; }

        public FilterType Type { get; private set; }

        public SoftwareBitmapSource Bitmap { get; private set; }

        public static async Task<ImageFilterItem> CreateAsync(SoftwareBitmap OriginBitmap, string Text, FilterType Type)
        {
            SoftwareBitmapSource Source = new SoftwareBitmapSource();

            switch (Type)
            {
                case FilterType.Origin:
                    {
                        await Source.SetBitmapAsync(OriginBitmap);
                        break;
                    }
                case FilterType.Invert:
                    {
                        using (SoftwareBitmap InvertEffectBitmap = await Task.Run(() => ComputerVisionProvider.InvertEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(InvertEffectBitmap);
                        }

                        break;
                    }
                case FilterType.Gray:
                    {
                        using (SoftwareBitmap GrayEffectBitmap = await Task.Run(() => ComputerVisionProvider.GrayEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(GrayEffectBitmap);
                        }

                        break;
                    }
                case FilterType.Threshold:
                    {
                        using (SoftwareBitmap ThresholdEffectBitmap = await Task.Run(() => ComputerVisionProvider.ThresholdEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(ThresholdEffectBitmap);
                        }

                        break;
                    }
                case FilterType.Sketch:
                    {
                        using (SoftwareBitmap SketchEffectBitmap = await Task.Run(() => ComputerVisionProvider.SketchEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(SketchEffectBitmap);
                        }

                        break;
                    }
                case FilterType.GaussianBlur:
                    {
                        using (SoftwareBitmap GaussianEffectBitmap = await Task.Run(() => ComputerVisionProvider.GaussianBlurEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(GaussianEffectBitmap);
                        }

                        break;
                    }
                case FilterType.Sepia:
                    {
                        using (SoftwareBitmap SepiaEffectBitmap = await Task.Run(() => ComputerVisionProvider.SepiaEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(SepiaEffectBitmap);
                        }

                        break;
                    }
                case FilterType.Mosaic:
                    {
                        using (SoftwareBitmap MosaicEffectBitmap = await Task.Run(() => ComputerVisionProvider.MosaicEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(MosaicEffectBitmap);
                        }

                        break;
                    }
                case FilterType.OilPainting:
                    {
                        using (SoftwareBitmap OilPaintingEffectBitmap = await Task.Run(() => ComputerVisionProvider.OilPaintingEffect(OriginBitmap)))
                        {
                            await Source.SetBitmapAsync(OilPaintingEffectBitmap);
                        }

                        break;
                    }
                default:
                    {
                        throw new NotSupportedException();
                    }
            }

            return new ImageFilterItem(Source, Text, Type);
        }

        private ImageFilterItem(SoftwareBitmapSource Bitmap, string Text, FilterType Type)
        {
            this.Bitmap = Bitmap;
            this.Text = Text;
            this.Type = Type;
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(ImageFilterItem));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                Bitmap.Dispose();
            });
        }

        ~ImageFilterItem()
        {
            Dispose();
        }
    }
}
