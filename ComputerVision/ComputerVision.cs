﻿using OpenCvSharp;
using OpenCvSharp.XImgProc;
using System;
using System.Linq;
using System.Threading;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace ComputerVision
{
    public static class ComputerVisionProvider
    {
        static ComputerVisionProvider()
        {
            if (!Cv2.UseOptimized())
            {
                Cv2.SetUseOptimized(true);
            }
        }

        public static SoftwareBitmap CreateCircleBitmapFromColor(int Width, int Height, Color FillColor)
        {
            if (Width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Width), "Argument must be positive");
            }

            if (Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Height), "Argument must be positive");
            }

            using (Mat OutputMat = CreateEmptyOutputBitmap(Width, Height, out SoftwareBitmap Output))
            {
                Cv2.Circle(OutputMat, Width / 2, Height / 2, Math.Min(Width, Height) / 4, new Scalar(FillColor.B, FillColor.G, FillColor.R, FillColor.A), -1, LineTypes.AntiAlias);
                return Output;
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

        public static SoftwareBitmap RotateEffect(SoftwareBitmap Input, RotateFlags Flags)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            {
                switch (Flags)
                {
                    case RotateFlags.Rotate90Clockwise:
                    case RotateFlags.Rotate90Counterclockwise:
                        {
                            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelHeight, Input.PixelWidth, out SoftwareBitmap Output))
                            {
                                Cv2.Rotate(InputMat, OutputMat, RotateFlags.Rotate90Clockwise);
                                return Output;
                            }
                        }
                    default:
                        {
                            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
                            {
                                Cv2.Rotate(InputMat, OutputMat, RotateFlags.Rotate90Clockwise);
                                return Output;
                            }
                        }
                }
            }
        }

        public static SoftwareBitmap FlipEffect(SoftwareBitmap Input, FlipMode Mode)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                Cv2.Flip(InputMat, OutputMat, Mode);
                return Output;
            }
        }

        public static SoftwareBitmap AdjustBrightnessContrast(SoftwareBitmap Input, double Alpha, double Beta)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                InputMat.ConvertTo(OutputMat, MatType.CV_8UC4, Alpha, Beta);
                return Output;
            }
        }

        public static SoftwareBitmap AutoColorEnhancement(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
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
                                        Cv2.Log(Lg / Hg + (float)(10 * Convert.ToSingle(Math.Exp(Cv2.Sum(Lg_)[0] / Num))), Lout);
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

                                Mat[] BGRChannel = Cv2.Split(Temp).Select<Mat, Mat>((Channel) => (Gain.Mul(Channel + Gray) + Channel - Gray) * 0.5f).ToArray();

                                try
                                {
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
                }

                return Output;
            }
        }

        public static SoftwareBitmap AutoWhiteBalance(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
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

                    int Threshold = 766;

                    for (int Sum = 0; Threshold >= 0; Threshold--)
                    {
                        if ((Sum += HistRGB[Threshold]) > InputMat.Rows * InputMat.Cols * 0.1)
                        {
                            break;
                        }
                    }

                    int SumB = 0;
                    int SumG = 0;
                    int SumR = 0;
                    int Count = 0;

                    unsafe
                    {
                        TempMat.ForEachAsVec3b((value, position) =>
                        {
                            if (value->Item0 + value->Item1 + value->Item2 > Threshold)
                            {
                                Interlocked.Add(ref SumB, value->Item0);
                                Interlocked.Add(ref SumG, value->Item1);
                                Interlocked.Add(ref SumR, value->Item2);
                                Interlocked.Increment(ref Count);
                            }
                        });
                    }

                    int AvgB = SumB / Count;
                    int AvgG = SumG / Count;
                    int AvgR = SumR / Count;

                    unsafe
                    {
                        TempMat.ForEachAsVec3b((value, position) =>
                        {
                            value->Item0 = (byte)Math.Max(0, Math.Min(255, value->Item0 * MaxVal / AvgB));
                            value->Item1 = (byte)Math.Max(0, Math.Min(255, value->Item1 * MaxVal / AvgG));
                            value->Item2 = (byte)Math.Max(0, Math.Min(255, value->Item2 * MaxVal / AvgR));
                        });
                    }

                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.BGR2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap InvertEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.BitwiseNot(InputMat, TempMat);
                    TempMat.CopyTo(OutputMat);
                }

                return Output;
            }
        }

        public static SoftwareBitmap GenenateResizedThumbnail(SoftwareBitmap Input, int Height, int Width)
        {
            if (Width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Width), "Argument must be positive");
            }

            if (Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Height), "Argument must be positive");
            }

            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Width, Height, out SoftwareBitmap Output))
            {
                int MaxOffset = Math.Min(InputMat.Cols - Width, InputMat.Rows - Height);
                int CropWidth = Width + MaxOffset;
                int CropHeight = Height + MaxOffset;

                using (Mat CroppedMat = InputMat[new Rect((InputMat.Cols - CropWidth) / 2, (InputMat.Rows - CropHeight) / 2, CropWidth, CropHeight)])
                {
                    Cv2.Resize(CroppedMat, OutputMat, new Size(Width, Height), 0, 0, InterpolationFlags.Area);
                }

                return Output;
            }
        }

        public static SoftwareBitmap ThresholdEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2GRAY);
                    Cv2.Threshold(TempMat, TempMat, 128, 255, ThresholdTypes.Otsu);
                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.GRAY2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap SketchEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2GRAY);
                    Cv2.AdaptiveThreshold(TempMat, TempMat, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.Binary, 21, 4);
                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.GRAY2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap GaussianBlurEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                Cv2.GaussianBlur(InputMat, OutputMat, new Size(0, 0), 8);
                return Output;
            }
        }

        public static SoftwareBitmap SepiaEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2BGR);

                    using (Mat Kernel = new Mat<float>(3, 3, new float[9] { 0.273f, 0.534f, 0.131f, 0.349f, 0.686f, 0.168f, 0.393f, 0.769f, 0.189f }))
                    {
                        Cv2.Transform(TempMat, TempMat, Kernel);
                    }

                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.BGR2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap OilPaintingEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2BGR);
                    Cv2.PyrMeanShiftFiltering(TempMat, TempMat, 12, 60);
                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.BGR2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap GrayEffect(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2GRAY);
                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.GRAY2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap MosaicEffect(SoftwareBitmap Input, int Level = 20)
        {
            if (Level <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Level), "Argument must equals or larger than 0");
            }

            using (Mat InputMat = Input.SoftwareBitmapToMat())
            using (Mat OutputMat = CreateEmptyOutputBitmap(Input.PixelWidth, Input.PixelHeight, out SoftwareBitmap Output))
            {
                using (Mat TempMat = new Mat(InputMat.Rows, InputMat.Cols, MatType.CV_8UC4))
                {
                    Cv2.CvtColor(InputMat, TempMat, ColorConversionCodes.BGRA2BGR);

                    for (int i = 0; i < TempMat.Rows; i += Level)
                    {
                        for (int j = 0; j < TempMat.Cols; j += Level)
                        {
                            Vec3b Vec = TempMat.At<Vec3b>(i, j);
                            Rect MosaicRect = new Rect(j, i, Math.Min(TempMat.Cols - j, Level), Math.Min(TempMat.Rows - i, Level));
                            TempMat[MosaicRect] = new Mat(MosaicRect.Size, MatType.CV_8UC3, new Scalar(Vec.Item0, Vec.Item1, Vec.Item2));
                        }
                    }

                    Cv2.CvtColor(TempMat, OutputMat, ColorConversionCodes.BGR2BGRA);
                }

                return Output;
            }
        }

        public static SoftwareBitmap ResizeToActual(SoftwareBitmap Input)
        {
            using (Mat InputMat = Input.SoftwareBitmapToMat())
            {
                Mat[] Channels = Cv2.Split(InputMat);

                try
                {
                    if (Channels.Length != 4)
                    {
                        throw new ArgumentException("Input must be BGRA image");
                    }

                    Mat Contour = Channels.Last().FindNonZero();
                    Rect ActualArea = Cv2.BoundingRect(Contour);
                    Rect ExtraArea = new Rect(Math.Max(ActualArea.X - 5, 0), Math.Max(ActualArea.Y - 5, 0), Math.Min(ActualArea.Width + 10, InputMat.Width - ActualArea.X), Math.Min(ActualArea.Height + 10, InputMat.Height - ActualArea.Y));

                    using (Mat OutputMat = CreateEmptyOutputBitmap(ExtraArea.Width, ExtraArea.Height, out SoftwareBitmap Output))
                    {
                        using (Mat TempMat = InputMat[ExtraArea])
                        {
                            TempMat.CopyTo(OutputMat);
                        }

                        return Output;
                    }
                }
                finally
                {
                    Array.ForEach(Channels, (Channel) => Channel.Dispose());
                }
            }
        }

        private static Mat CreateEmptyOutputBitmap(int Width, int Height, out SoftwareBitmap Output)
        {
            Output = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Width, Height, BitmapAlphaMode.Premultiplied);
            return Output.SoftwareBitmapToMat();
        }

        private static unsafe Mat SoftwareBitmapToMat(this SoftwareBitmap Bitmap)
        {
            using (BitmapBuffer Buffer = Bitmap.LockBuffer(BitmapBufferAccessMode.ReadWrite))
            using (Windows.Foundation.IMemoryBufferReference Reference = Buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)Reference).GetBuffer(out byte* DataInBytes, out _);
                return new Mat(Bitmap.PixelHeight, Bitmap.PixelWidth, MatType.CV_8UC4, (IntPtr)DataInBytes);
            }
        }
    }
}
