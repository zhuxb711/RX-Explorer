using OpenCvSharp;
using OpenCvSharp.XImgProc;
using System;
using System.Linq;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace ComputerVision
{
    public static class ComputerVisionProvider
    {
        public static SoftwareBitmap CreateCircleBitmapFromColor(int Width, int Height, Color FillColor)
        {
            if (Width <= 0 || Height <= 0)
            {
                throw new ArgumentException("Argument must be positive");
            }

            using (Mat CircleMat = Mat.Zeros(Height, Width, MatType.CV_8UC4))
            {
                Cv2.Circle(CircleMat, Width / 2, Height / 2, Math.Min(Width, Height) / 4, new Scalar(FillColor.B, FillColor.G, FillColor.R, FillColor.A), -1, LineTypes.AntiAlias);
                return CircleMat.MatToSoftwareBitmap();
            }
        }

        public static float DetectAvgBrightness(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat BGRMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3))
            {
                Cv2.CvtColor(InputMat, BGRMat, ColorConversionCodes.BGRA2BGR);

                using (Mat HSVMat = new Mat(BGRMat.Rows, BGRMat.Cols, MatType.CV_8UC3))
                {
                    Cv2.CvtColor(BGRMat, HSVMat, ColorConversionCodes.BGR2HSV);

                    using (Mat VChannel = HSVMat.ExtractChannel(2))
                    {
                        return Convert.ToSingle(Cv2.Mean(VChannel).Val0);
                    }
                }
            }
        }

        public static SoftwareBitmap ExtendImageBorder(SoftwareBitmap Input, Color Colors, int Top, int Left, int Right, int Bottom)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CopyMakeBorder(InputMat, OutputMat, Top, Bottom, Left, Right, BorderTypes.Constant, new Scalar(Colors.B, Colors.G, Colors.R));

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap RotateEffect(SoftwareBitmap Input, int Angle)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                switch (Angle)
                {
                    case 90:
                        {
                            Cv2.Transpose(InputMat, OutputMat);
                            Cv2.Flip(OutputMat, OutputMat, FlipMode.Y);
                            break;
                        }
                    case 180:
                        {
                            Cv2.Flip(InputMat, OutputMat, FlipMode.XY);
                            break;
                        }
                    case -90:
                        {
                            Cv2.Transpose(InputMat, OutputMat);
                            Cv2.Flip(OutputMat, OutputMat, FlipMode.X);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Angle 仅支持90、180、-90度");
                        }
                }

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap FlipEffect(SoftwareBitmap Input, bool FlipByX)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                if (FlipByX)
                {
                    Cv2.Flip(InputMat, OutputMat, FlipMode.X);
                }
                else
                {
                    Cv2.Flip(InputMat, OutputMat, FlipMode.Y);
                }

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap AdjustBrightnessContrast(SoftwareBitmap Input, double Alpha, double Beta)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                InputMat.ConvertTo(OutputMat, MatType.CV_8UC4, Alpha, Beta);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap AutoColorEnhancement(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            using (Mat Temp = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
            using (Mat Gray = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
            {
                InputMat.ConvertTo(Temp, MatType.CV_32FC3);

                Cv2.CvtColor(Temp, Gray, ColorConversionCodes.BGR2GRAY);

                Cv2.MinMaxLoc(Gray, out _, out double LwMax);

                float LwAver;
                int Num;

                using (Mat Lw_ = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                {
                    Num = InputMat.Rows * InputMat.Cols;

                    //计算每个数组元素绝对值的自然对数
                    Cv2.Log(Gray + 1e-3f, Lw_);

                    //矩阵自然指数
                    LwAver = Convert.ToSingle(Math.Exp(Cv2.Sum(Lw_)[0] / Num));
                }

                using (Mat Lg = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                {
                    Cv2.Log(Gray / LwAver + 1f, Lg);
                    Cv2.Divide(Lg, Math.Log(LwMax / LwAver + 1f), Lg);

                    //局部自适应
                    using (Mat Lout = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                    {
                        int kernelSize = Convert.ToInt32(Math.Floor(Convert.ToDouble(Math.Max(3, Math.Max(InputMat.Rows / 100, InputMat.Cols / 100)))));

                        using (Mat Lp = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                        {
                            using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize)))
                            {
                                Cv2.Dilate(Lg, Lp, kernel);
                            }

                            using (Mat Hg = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                            {
                                CvXImgProc.GuidedFilter(Lg, Lp, Hg, 10, 0.01);

                                Cv2.MinMaxLoc(Lg, out _, out double LgMax);

                                using (Mat alpha = 1.0f + Lg * (36 / LgMax))
                                using (Mat Lg_ = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32FC3))
                                {
                                    Cv2.Log(Lg + 1e-3f, Lg_);

                                    float beta = 10 * Convert.ToSingle(Math.Exp(Cv2.Sum(Lg_)[0] / Num));

                                    Cv2.Log(Lg / Hg + beta, Lout);
                                    Cv2.Multiply(alpha, Lout, Lout);
                                    Cv2.Normalize(Lout, Lout, 0, 255, NormTypes.MinMax);
                                }
                            }
                        }

                        using (Mat Gain = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_32F))
                        {
                            for (int i = 0; i < InputMat.Rows; i++)
                            {
                                for (int j = 0; j < InputMat.Cols; j++)
                                {
                                    float x = Gray.At<float>(i, j);
                                    float y = Lout.At<float>(i, j);
                                    Gain.At<float>(i, j) = x == 0 ? y : y / x;
                                }
                            }

                            Mat[] BGRChannel = Cv2.Split(Temp);

                            try
                            {
                                BGRChannel[0] = (Gain.Mul(BGRChannel[0] + Gray) + BGRChannel[0] - Gray) * 0.5f;
                                BGRChannel[1] = (Gain.Mul(BGRChannel[1] + Gray) + BGRChannel[1] - Gray) * 0.5f;
                                BGRChannel[2] = (Gain.Mul(BGRChannel[2] + Gray) + BGRChannel[2] - Gray) * 0.5f;

                                Cv2.Merge(BGRChannel, Temp);

                                Temp.ConvertTo(OutputMat, MatType.CV_8UC4);
                            }
                            finally
                            {
                                Array.ForEach(BGRChannel, (Channel) => Channel.Dispose());
                            }
                        }
                    }
                }

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap AutoWhiteBalance(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3))
            {
                Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2BGR);

                int[] HistRGB = new int[767];

                int MaxVal = 0;

                for (int i = 0; i < InputMat.Rows; i++)
                {
                    for (int j = 0; j < InputMat.Cols; j++)
                    {
                        Vec3b Vec = TempMat.At<Vec3b>(i, j);

                        MaxVal = Math.Max(Math.Max(Math.Max(MaxVal, Vec.Item0), Vec.Item1), Vec.Item2);

                        HistRGB[Vec.Item0 + Vec.Item1 + Vec.Item2]++;
                    }
                }

                int Threshold = 0;
                int Sum = 0;
                for (int i = 766; i >= 0; i--)
                {
                    Sum += HistRGB[i];
                    if (Sum > InputMat.Rows * InputMat.Cols * 0.1)
                    {
                        Threshold = i;
                        break;
                    }
                }

                int AvgB = 0;
                int AvgG = 0;
                int AvgR = 0;
                int Cnt = 0;
                for (int i = 0; i < InputMat.Rows; i++)
                {
                    for (int j = 0; j < InputMat.Cols; j++)
                    {
                        Vec3b Vec = TempMat.At<Vec3b>(i, j);

                        int SumP = Vec.Item0 + Vec.Item1 + Vec.Item2;

                        if (SumP > Threshold)
                        {
                            AvgB += Vec.Item0;
                            AvgG += Vec.Item1;
                            AvgR += Vec.Item2;
                            Cnt++;
                        }
                    }
                }

                AvgB /= Cnt;
                AvgG /= Cnt;
                AvgR /= Cnt;
                for (int i = 0; i < InputMat.Rows; i++)
                {
                    for (int j = 0; j < InputMat.Cols; j++)
                    {
                        Vec3b Vec = TempMat.At<Vec3b>(i, j);

                        int Blue = Vec.Item0 * MaxVal / AvgB;
                        int Green = Vec.Item1 * MaxVal / AvgG;
                        int Red = Vec.Item2 * MaxVal / AvgR;

                        if (Red > 255)
                        {
                            Red = 255;
                        }
                        else if (Red < 0)
                        {
                            Red = 0;
                        }

                        if (Green > 255)
                        {
                            Green = 255;
                        }
                        else if (Green < 0)
                        {
                            Green = 0;
                        }

                        if (Blue > 255)
                        {
                            Blue = 255;
                        }
                        else if (Blue < 0)
                        {
                            Blue = 0;
                        }

                        TempMat.At<Vec3b>(i, j) = new Vec3b((byte)Blue, (byte)Green, (byte)Red);
                    }
                }

                Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.BGR2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap InvertEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.BitwiseNot(InputMat, OutputMat);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap GenenateResizedThumbnail(SoftwareBitmap Input, int Height, int Width)
        {
            using (Mat OnputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(OnputMat.Rows, OnputMat.Cols, MatType.CV_8UC4))
            {
                if (OnputMat.Cols > OnputMat.Rows)
                {
                    int CropSize = OnputMat.Rows;
                    int StartXPosition = (OnputMat.Cols - CropSize) / 2;
                    int StartYPosition = 0;
                    Rect CropRect = new Rect(StartXPosition, StartYPosition, CropSize, CropSize);
                    using (Mat CroppedMat = new Mat(OnputMat, CropRect))
                    {
                        Cv2.Resize(CroppedMat, OutputMat, new Size(Width, Height), 0, 0, InterpolationFlags.Linear);
                    }
                }
                else
                {
                    int CropSize = OnputMat.Cols;
                    int StartYPosition = (OnputMat.Rows - CropSize) / 2;
                    int StartXPosition = 0;
                    Rect CropRect = new Rect(StartXPosition, StartYPosition, CropSize, CropSize);
                    using (Mat CroppedMat = new Mat(OnputMat, CropRect))
                    {
                        Cv2.Resize(CroppedMat, OutputMat, new Size(Width, Height), 0, 0, InterpolationFlags.Linear);
                    }
                }

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap ThresholdEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2GRAY);
                Cv2.Threshold(OutputMat, OutputMat, 128, 255, ThresholdTypes.Otsu);
                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.GRAY2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap SketchEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2GRAY);
                Cv2.AdaptiveThreshold(OutputMat, OutputMat, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 11, 2);
                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.GRAY2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap GaussianBlurEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.GaussianBlur(InputMat, OutputMat, new Size(0, 0), 8);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap SepiaEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2BGR);

                float[] Data = new float[9] { 0.273f, 0.534f, 0.131f, 0.349f, 0.686f, 0.168f, 0.393f, 0.769f, 0.189f };

                using (Mat Kernel = new Mat<float>(3, 3, Data))
                {
                    Cv2.Transform(OutputMat, OutputMat, Kernel);
                }

                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.BGR2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap OilPaintingEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2BGR);
                Cv2.PyrMeanShiftFiltering(OutputMat, OutputMat, 12, 60);
                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.BGR2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap GrayEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2GRAY);
                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.GRAY2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap CalculateHistogram(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3))
            {
                Mat[] OutputRGB = new Mat[3]
                {
                    new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3),
                    new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3),
                    new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC3)
                };

                try
                {
                    int[] hissize = new int[1] { 512 };
                    int histHeight = 512, histWidth = 512;
                    float[] ranges = new float[2] { 0.0f, 256.0f };

                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2BGR);

                    Mat[] RGBChannels = Cv2.Split(TempMat);

                    try
                    {
                        Cv2.CalcHist(new Mat[1] { RGBChannels[0] }, new int[1] { 0 }, null, OutputRGB[0], 1, hissize, new float[][] { ranges });
                        Cv2.CalcHist(new Mat[1] { RGBChannels[1] }, new int[1] { 0 }, null, OutputRGB[1], 1, hissize, new float[][] { ranges });
                        Cv2.CalcHist(new Mat[1] { RGBChannels[2] }, new int[1] { 0 }, null, OutputRGB[2], 1, hissize, new float[][] { ranges });

                        Cv2.Normalize(OutputRGB[0], OutputRGB[0], 0, histHeight, NormTypes.MinMax);
                        Cv2.Normalize(OutputRGB[1], OutputRGB[1], 0, histHeight, NormTypes.MinMax);
                        Cv2.Normalize(OutputRGB[2], OutputRGB[2], 0, histHeight, NormTypes.MinMax);

                        using (Mat histImage = new Mat(histHeight, histWidth, MatType.CV_8UC3, new Scalar(20, 20, 20)))
                        {
                            int binStep = Convert.ToInt32(Math.Round(histWidth / (float)hissize[0], MidpointRounding.AwayFromZero));

                            for (int i = 1; i < hissize[0]; i++)
                            {
                                Cv2.Line(histImage, new Point(binStep * (i - 1), histHeight - Convert.ToInt32(Math.Round(OutputRGB[0].At<float>(i - 1), MidpointRounding.AwayFromZero))), new Point(binStep * (i), histHeight - Convert.ToInt32(Math.Round(OutputRGB[0].At<float>(i), MidpointRounding.AwayFromZero))), new Scalar(255, 0, 0));
                                Cv2.Line(histImage, new Point(binStep * (i - 1), histHeight - Convert.ToInt32(Math.Round(OutputRGB[1].At<float>(i - 1), MidpointRounding.AwayFromZero))), new Point(binStep * (i), histHeight - Convert.ToInt32(Math.Round(OutputRGB[1].At<float>(i), MidpointRounding.AwayFromZero))), new Scalar(0, 255, 0));
                                Cv2.Line(histImage, new Point(binStep * (i - 1), histHeight - Convert.ToInt32(Math.Round(OutputRGB[2].At<float>(i - 1), MidpointRounding.AwayFromZero))), new Point(binStep * (i), histHeight - Convert.ToInt32(Math.Round(OutputRGB[2].At<float>(i), MidpointRounding.AwayFromZero))), new Scalar(0, 0, 255));
                            }

                            Cv2.CvtColor(histImage, OutputMat, ColorConversionCodes.BGR2BGRA);

                            return OutputMat.MatToSoftwareBitmap();
                        }
                    }
                    finally
                    {
                        Array.ForEach(RGBChannels, (Channel) => Channel.Dispose());
                    }
                }
                finally
                {
                    Array.ForEach(OutputRGB, (Channel) => Channel.Dispose());
                }
            }
        }

        public static SoftwareBitmap MosaicEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CvtColor(InputMat, OutputMat, ColorConversionCodes.BGRA2BGR);

                int level = 15;

                for (int i = 0; i < OutputMat.Rows; i += level)
                {
                    for (int j = 0; j < OutputMat.Cols; j += level)
                    {
                        Rect mosaicRect;

                        if (j + level > OutputMat.Cols && i + level > OutputMat.Rows)
                        {
                            mosaicRect = new Rect(j, i, OutputMat.Cols - j, OutputMat.Rows - i);
                        }
                        else if (j + level > OutputMat.Cols)
                        {
                            mosaicRect = new Rect(j, i, OutputMat.Cols - j, level);
                        }
                        else if (i + level > OutputMat.Rows)
                        {
                            mosaicRect = new Rect(j, i, level, OutputMat.Rows - i);
                        }
                        else
                        {
                            mosaicRect = new Rect(j, i, level, level);
                        }

                        Vec3b Vec = OutputMat.At<Vec3b>(i, j);

                        Scalar scalar = new Scalar(
                            Vec.Item0,
                            Vec.Item1,
                            Vec.Item2);

                        OutputMat[mosaicRect] = new Mat(mosaicRect.Size, MatType.CV_8UC3, scalar);
                    }
                }

                Cv2.CvtColor(OutputMat, OutputMat, ColorConversionCodes.BGR2BGRA);

                return OutputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap ResizeToActual(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            {
                Mat[] Channels = Array.Empty<Mat>();

                try
                {
                    Channels = Cv2.Split(InputMat);

                    if (Channels.Length == 4)
                    {
                        Mat Contour = Channels.Last().FindNonZero();
                        Rect ActualArea = Cv2.BoundingRect(Contour);
                        Rect ExtraArea = new Rect(Math.Max(ActualArea.X - 5, 0), Math.Max(ActualArea.Y - 5, 0), Math.Min(ActualArea.Width + 10, InputMat.Width - ActualArea.X), Math.Min(ActualArea.Height + 10, InputMat.Height - ActualArea.Y));
                        return InputMat[ExtraArea].Clone().MatToSoftwareBitmap();
                    }
                    else
                    {
                        throw new ArgumentException("Input must be BGRA image");
                    }
                }
                catch (Exception)
                {
                    return SoftwareBitmap.Copy(Input);
                }
                finally
                {
                    Array.ForEach(Channels, (Channel) => Channel.Dispose());
                }
            }
        }

        private static unsafe Mat SoftwareBitmapToMat(this SoftwareBitmap Bitmap)
        {
            try
            {
                using (BitmapBuffer Buffer = Bitmap.LockBuffer(BitmapBufferAccessMode.Write))
                using (Windows.Foundation.IMemoryBufferReference Reference = Buffer.CreateReference())
                {
                    ((IMemoryBufferByteAccess)Reference).GetBuffer(out byte* dataInBytes, out uint capacity);

                    return new Mat(Bitmap.PixelHeight, Bitmap.PixelWidth, MatType.CV_8UC4, (IntPtr)dataInBytes);
                }
            }
            catch
            {
                return new Mat(Bitmap.PixelHeight, Bitmap.PixelWidth, MatType.CV_8UC4);
            }
        }

        private static unsafe SoftwareBitmap MatToSoftwareBitmap(this Mat Input)
        {
            try
            {
                SoftwareBitmap Output = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Input.Width, Input.Height, BitmapAlphaMode.Premultiplied);

                using (BitmapBuffer Buffer = Output.LockBuffer(BitmapBufferAccessMode.ReadWrite))
                using (Windows.Foundation.IMemoryBufferReference Reference = Buffer.CreateReference())
                {
                    ((IMemoryBufferByteAccess)Reference).GetBuffer(out byte* DataInBytes, out _);

                    BitmapPlaneDescription BufferLayout = Buffer.GetPlaneDescription(0);

                    for (int i = 0; i < BufferLayout.Height; i++)
                    {
                        for (int j = 0; j < BufferLayout.Width; j++)
                        {
                            int Index = BufferLayout.StartIndex + (BufferLayout.Stride * i) + (4 * j);

                            DataInBytes[Index] = Input.DataPointer[Index];
                            DataInBytes[Index + 1] = Input.DataPointer[Index + 1];
                            DataInBytes[Index + 2] = Input.DataPointer[Index + 2];
                            DataInBytes[Index + 3] = Input.DataPointer[Index + 3];
                        }
                    }
                }

                return Output;
            }
            catch
            {
                return new SoftwareBitmap(BitmapPixelFormat.Bgra8, Input.Width, Input.Height, BitmapAlphaMode.Premultiplied);
            }
        }
    }
}
