using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace ComputerVision
{
    public static class ComputerVision
    {
        [ComImport]
        [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private unsafe interface IMemoryBufferByteAccess
        {
            void GetBuffer(out byte* buffer, out uint capacity);
        }

        public static SoftwareBitmap ExtendImageBorder(SoftwareBitmap Input, Color Colors, int Top, int Left, int Right, int Bottom)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.CopyMakeBorder(inputMat, outputMat, Top, Bottom, Left, Right, BorderTypes.Constant, new Scalar(Colors.B, Colors.G, Colors.R));

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap RotateEffect(SoftwareBitmap Input, int Angle)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                switch (Angle)
                {
                    case 90:
                        {
                            Cv2.Transpose(inputMat, outputMat);
                            Cv2.Flip(outputMat, outputMat, FlipMode.Y);
                            break;
                        }
                    case 180:
                        {
                            Cv2.Flip(outputMat, outputMat, FlipMode.XY);
                            break;
                        }
                    case -90:
                        {
                            Cv2.Transpose(inputMat, outputMat);
                            Cv2.Flip(outputMat, outputMat, FlipMode.X);
                            break;
                        }
                    default:
                        {
                            throw new Exception("Angle 仅支持90、180、-90度");
                        }
                }

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap FlipEffect(SoftwareBitmap Input, bool FlipByX)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                if (FlipByX)
                {
                    Cv2.Flip(inputMat, outputMat, FlipMode.X);
                }
                else
                {
                    Cv2.Flip(inputMat, outputMat, FlipMode.Y);
                }

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap AdjustBrightnessContrast(SoftwareBitmap Input, double Alpha, double Beta)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                inputMat.ConvertTo(outputMat, MatType.CV_8UC4, Alpha, Beta);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap InvertEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.BitwiseNot(inputMat, outputMat);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap GenenateResizedThumbnail(SoftwareBitmap Input, int Height, int Width)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                if (inputMat.Cols > inputMat.Rows)
                {
                    int CropSize = inputMat.Rows;
                    int StartXPosition = (inputMat.Cols - CropSize) / 2;
                    int StartYPosition = 0;
                    Rect CropRect = new Rect(StartXPosition, StartYPosition, CropSize, CropSize);
                    Mat CroppedMat = new Mat(inputMat, CropRect);
                    Cv2.Resize(CroppedMat, outputMat, new Size(Width, Height), 0, 0, InterpolationFlags.Linear);
                }
                else
                {
                    int CropSize = inputMat.Cols;
                    int StartYPosition = (inputMat.Rows - CropSize) / 2;
                    int StartXPosition = 0;
                    Rect CropRect = new Rect(StartXPosition, StartYPosition, CropSize, CropSize);
                    Mat CroppedMat = new Mat(inputMat, CropRect);
                    Cv2.Resize(CroppedMat, outputMat, new Size(Width, Height), 0, 0, InterpolationFlags.Linear);
                }

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap ThresholdEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.Threshold(inputMat, outputMat, 100, 255, ThresholdTypes.Binary);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap SketchEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.MedianBlur(inputMat, outputMat, 7);
                Cv2.Laplacian(outputMat, outputMat, MatType.CV_8U, 5);
                Cv2.Threshold(outputMat, outputMat, 80, 255, ThresholdTypes.Binary);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap GaussianBlurEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.GaussianBlur(inputMat, outputMat, new Size(0, 0), 8);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap SepiaEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Mat Kernel = new Mat<float>(3, 3)
                {
                    0.272, 0.534, 0.131,
                    0.349, 0.686, 0.168,
                    0.393, 0.769, 0.189
                };

                Cv2.Transform(inputMat, outputMat, Kernel);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap OilPaintingEffect(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Cv2.PyrMeanShiftFiltering(inputMat, outputMat, 12, 60);

                return outputMat.MatToSoftwareBitmap();
            }
        }

        public static SoftwareBitmap CalculateHistogram(SoftwareBitmap Input)
        {
            using (Mat inputMat = Input.SoftwareBitmapToMat())
            using (Mat outputMat = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC4))
            {
                Mat temp = new Mat(inputMat.Rows, inputMat.Cols, MatType.CV_8UC3);
                Mat[] RGBChannels = new Mat[3];
                Mat[] OutputRGB = new Mat[3];
                int[] hissize = new int[1] { 512 };
                int histHeight = 512, histWidth = 512;
                float[] ranges = new float[2] { 0.0f, 256.0f };

                Cv2.CvtColor(inputMat, temp, ColorConversionCodes.BGRA2BGR);
                Cv2.Split(temp, out RGBChannels);
                Cv2.CalcHist(new Mat[1] { RGBChannels[0] }, new int[1] { 0 }, null, OutputRGB[0], 1, hissize);

                return outputMat.MatToSoftwareBitmap();
            }
        }



        private static unsafe Mat SoftwareBitmapToMat(this SoftwareBitmap softwareBitmap)
        {
            using (BitmapBuffer buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var dataInBytes, out var capacity);

                return new Mat(softwareBitmap.PixelHeight, softwareBitmap.PixelWidth, MatType.CV_8UC4, (IntPtr)dataInBytes);
            }
        }

        private static unsafe SoftwareBitmap MatToSoftwareBitmap(this Mat input)
        {
            SoftwareBitmap output = new SoftwareBitmap(BitmapPixelFormat.Bgra8, input.Width, input.Height);

            using (BitmapBuffer buffer = output.LockBuffer(BitmapBufferAccessMode.ReadWrite))
            using (var reference = buffer.CreateReference())
            {
                ((IMemoryBufferByteAccess)reference).GetBuffer(out var dataInBytes, out var capacity);
                BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);

                for (int i = 0; i < bufferLayout.Height; i++)
                {
                    for (int j = 0; j < bufferLayout.Width; j++)
                    {
                        dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 0] =
                            input.DataPointer[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 0];
                        dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 1] =
                            input.DataPointer[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 1];
                        dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 2] =
                            input.DataPointer[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 2];
                        dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 3] =
                            input.DataPointer[bufferLayout.StartIndex + bufferLayout.Stride * i + 4 * j + 3];
                    }
                }
            }

            return output;
        }
    }
}
