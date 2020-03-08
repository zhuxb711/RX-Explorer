#include "pch.h"
#include "OpenCVLibrary.h"
#include "MemoryBuffer.h"
#include <opencv2\imgproc\types_c.h>
#include <iostream>
#include <string>  
#include <windows.h>
#define PI 3.1415926

using namespace OpenCV;
using namespace Platform;

static bool IsInitialized = false;

const float YCbCrYRF = 0.299F;              // RGB转YCbCr的系数(浮点类型）
const float YCbCrYGF = 0.587F;
const float YCbCrYBF = 0.114F;
const float YCbCrCbRF = -0.168736F;
const float YCbCrCbGF = -0.331264F;
const float YCbCrCbBF = 0.500000F;
const float YCbCrCrRF = 0.500000F;
const float YCbCrCrGF = -0.418688F;
const float YCbCrCrBF = -0.081312F;

const float RGBRYF = 1.00000F;            // YCbCr转RGB的系数(浮点类型）
const float RGBRCbF = 0.0000F;
const float RGBRCrF = 1.40200F;
const float RGBGYF = 1.00000F;
const float RGBGCbF = -0.34414F;
const float RGBGCrF = -0.71414F;
const float RGBBYF = 1.00000F;
const float RGBBCbF = 1.77200F;
const float RGBBCrF = 0.00000F;

const int Shift = 20;
const int HalfShiftValue = 1 << (Shift - 1);

const int YCbCrYRI = (int)(YCbCrYRF * (1 << Shift) + 0.5);         // RGB转YCbCr的系数(整数类型）
const int YCbCrYGI = (int)(YCbCrYGF * (1 << Shift) + 0.5);
const int YCbCrYBI = (int)(YCbCrYBF * (1 << Shift) + 0.5);
const int YCbCrCbRI = (int)(YCbCrCbRF * (1 << Shift) + 0.5);
const int YCbCrCbGI = (int)(YCbCrCbGF * (1 << Shift) + 0.5);
const int YCbCrCbBI = (int)(YCbCrCbBF * (1 << Shift) + 0.5);
const int YCbCrCrRI = (int)(YCbCrCrRF * (1 << Shift) + 0.5);
const int YCbCrCrGI = (int)(YCbCrCrGF * (1 << Shift) + 0.5);
const int YCbCrCrBI = (int)(YCbCrCrBF * (1 << Shift) + 0.5);

const int RGBRYI = (int)(RGBRYF * (1 << Shift) + 0.5);              // YCbCr转RGB的系数(整数类型）
const int RGBRCbI = (int)(RGBRCbF * (1 << Shift) + 0.5);
const int RGBRCrI = (int)(RGBRCrF * (1 << Shift) + 0.5);
const int RGBGYI = (int)(RGBGYF * (1 << Shift) + 0.5);
const int RGBGCbI = (int)(RGBGCbF * (1 << Shift) + 0.5);
const int RGBGCrI = (int)(RGBGCrF * (1 << Shift) + 0.5);
const int RGBBYI = (int)(RGBBYF * (1 << Shift) + 0.5);
const int RGBBCbI = (int)(RGBBCbF * (1 << Shift) + 0.5);
const int RGBBCrI = (int)(RGBBCrF * (1 << Shift) + 0.5);

OpenCVLibrary::OpenCVLibrary()
{
}

void OpenCV::OpenCVLibrary::Initialize()
{
	if (!useOptimized())
	{
		setUseOptimized(true);
	}
}

Mat OpenCV::OpenCVLibrary::RGB2YCbCr(Mat src)
{
	int row = src.rows;
	int col = src.cols;
	Mat dst(row, col, CV_8UC3);
	for (int i = 0; i < row; i++) 
	{
		for (int j = 0; j < col; j++) 
		{
			int Blue = src.at<Vec3b>(i, j)[0];
			int Green = src.at<Vec3b>(i, j)[1];
			int Red = src.at<Vec3b>(i, j)[2];
			dst.at<Vec3b>(i, j)[0] = (int)((YCbCrYRI * Red + YCbCrYGI * Green + YCbCrYBI * Blue + HalfShiftValue) >> Shift);
			dst.at<Vec3b>(i, j)[1] = (int)(128 + ((YCbCrCbRI * Red + YCbCrCbGI * Green + YCbCrCbBI * Blue + HalfShiftValue) >> Shift));
			dst.at<Vec3b>(i, j)[2] = (int)(128 + ((YCbCrCrRI * Red + YCbCrCrGI * Green + YCbCrCrBI * Blue + HalfShiftValue) >> Shift));
		}
	}
	return dst;
}

Mat OpenCV::OpenCVLibrary::YCbCr2RGB(Mat src)
{
	int row = src.rows;
	int col = src.cols;
	Mat dst(row, col, CV_8UC3);
	for (int i = 0; i < row; i++) 
	{
		for (int j = 0; j < col; j++) 
		{
			int Y = src.at<Vec3b>(i, j)[0];
			int Cb = src.at<Vec3b>(i, j)[1] - 128;
			int Cr = src.at<Vec3b>(i, j)[2] - 128;
			int Red = Y + ((RGBRCrI * Cr + HalfShiftValue) >> Shift);
			int Green = Y + ((RGBGCbI * Cb + RGBGCrI * Cr + HalfShiftValue) >> Shift);
			int Blue = Y + ((RGBBCbI * Cb + HalfShiftValue) >> Shift);
			if (Red > 255) Red = 255; else if (Red < 0) Red = 0;
			if (Green > 255) Green = 255; else if (Green < 0) Green = 0;    // 编译后应该比三目运算符的效率高
			if (Blue > 255) Blue = 255; else if (Blue < 0) Blue = 0;
			dst.at<Vec3b>(i, j)[0] = Blue;
			dst.at<Vec3b>(i, j)[1] = Green;
			dst.at<Vec3b>(i, j)[2] = Red;
		}
	}
	return dst;
}

void OpenCV::OpenCVLibrary::AutoColorLevel(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}
	double LowCut = 0.005; double HighCut = 0.005;

	Mat src;
	cvtColor(inputMat, src, COLOR_BGRA2RGB);

	int rows = src.rows;
	int cols = src.cols;
	int totalPixel = rows * cols;
	//统计每个通道的直方图
	uchar Pixel[256 * 3] = { 0 };
	vector <Mat> rgb;
	split(src, rgb);
	Mat HistBlue, HistGreen, HistRed;
	int histSize = 256;
	float range[] = { 0, 255 };
	const float* histRange = { range };
	bool uniform = true;
	bool accumulate = false;
	calcHist(&rgb[0], 1, 0, Mat(), HistRed, 1, &histSize, &histRange, uniform, accumulate);
	calcHist(&rgb[1], 1, 0, Mat(), HistGreen, 1, &histSize, &histRange, uniform, accumulate);
	calcHist(&rgb[2], 1, 0, Mat(), HistBlue, 1, &histSize, &histRange, uniform, accumulate);
	//分别计算各通道按照给定的参数所确定的上下限值
	int MinBlue = 0, MaxBlue = 0;
	int MinRed = 0, MaxRed = 0;
	int MinGreen = 0, MaxGreen = 0;

	//Blue Channel
	float sum = 0;
	sum = 0;
	for (int i = 0; i < 256; i++) 
	{
		sum += HistBlue.at<float>(i);
		if (sum >= totalPixel * LowCut * 0.01) 
		{
			MinBlue = i;
			break;
		}
	}
	sum = 0;
	for (int i = 255; i >= 0; i--) 
	{
		sum = sum + HistBlue.at<float>(i);
		if (sum >= totalPixel * HighCut * 0.01) 
		{
			MaxBlue = i;
			break;
		}
	}
	//Red channel
	for (int i = 0; i < 256; i++) 
	{
		sum += HistRed.at<float>(i);
		if (sum >= totalPixel * LowCut * 0.01)
		{
			MinRed = i;
			break;
		}
	}
	sum = 0;
	for (int i = 255; i >= 0; i--) 
	{
		sum = sum + HistRed.at<float>(i);
		if (sum >= totalPixel * HighCut * 0.01)
		{
			MaxRed = i;
			break;
		}
	}
	//Green channel
	sum = 0;
	for (int i = 0; i < 256; i++) {
		sum += HistGreen.at<float>(i);
		if (sum >= totalPixel * LowCut * 0.01) {
			MinGreen = i;
			break;
		}
	}
	sum = 0;
	for (int i = 255; i >= 0; i--) 
	{
		sum = sum + HistGreen.at<float>(i);
		if (sum >= totalPixel * HighCut * 0.01) 
		{
			MaxGreen = i;
			break;
		}
	}

	for (int i = 0; i < 256; i++) 
	{
		if (i <= MinBlue) 
		{
			Pixel[i * 3 + 2] = 0;
		}
		else 
		{
			if (i > MaxBlue) 
			{
				Pixel[i * 3 + 2] = 255;
			}
			else 
			{
				float temp = (float)(i - MinBlue) / (MaxBlue - MinBlue);
				Pixel[i * 3 + 2] = (uchar)(temp * 255);
			}
		}
		if (i <= MinGreen) 
		{
			Pixel[i * 3 + 1] = 0;
		}
		else 
		{
			if (i > MaxGreen) 
			{
				Pixel[i * 3 + 1] = 255;
			}
			else 
			{
				float temp = (float)(i - MinGreen) / (MaxGreen - MinGreen);
				Pixel[i * 3 + 1] = (uchar)(temp * 255);
			}
		}
		if (i <= MinRed) 
		{
			Pixel[i * 3] = 0;
		}
		else 
		{
			if (i > MaxRed) 
			{
				Pixel[i * 3] = 255;
			}
			else 
			{
				float temp = (float)(i - MinRed) / (MaxRed - MinRed);
				Pixel[i * 3] = (uchar)(temp * 255);
			}
		}
	}
	Mat TMP(1, 256, CV_8UC3, Pixel);
	Mat temp;
	LUT(src, TMP, temp);
	cvtColor(temp, outputMat, COLOR_RGB2BGRA);
}

void OpenCV::OpenCVLibrary::AutoWhiteBalance(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	Mat src;
	int row = inputMat.rows;
	int col = inputMat.cols;
	if (inputMat.channels() == 4) 
	{
		cvtColor(inputMat, src, CV_BGRA2BGR);
	}
	Mat in = RGB2YCbCr(src);
	Mat mark(row, col, CV_8UC1);
	int sum = 0;
	for (int i = 0; i < row; i += 100) 
	{
		for (int j = 0; j < col; j += 100) 
		{
			if (i + 100 < row && j + 100 < col) 
			{
				cv::Rect rect(j, i, 100, 100);
				Mat temp = in(rect);
				Scalar global_mean = mean(temp);
				double dr = 0, db = 0;
				for (int x = 0; x < 100; x++)
				{
					uchar* ptr = temp.ptr<uchar>(x) + 1;
					for (int y = 0; y < 100; y++) 
					{
						dr += pow(abs(*ptr - global_mean[1]), 2);
						ptr++;
						db += pow(abs(*ptr - global_mean[2]), 2);
						ptr++;
						ptr++;
					}
				}
				dr /= 10000;
				db /= 10000;
				double cr_left_criteria = 1.5 * global_mean[1] + dr * global_mean[1];
				double cr_right_criteria = 1.5 * dr;
				double cb_left_criteria = global_mean[2] + db * global_mean[2];
				double cb_right_criteria = 1.5 * db;
				for (int x = 0; x < 100; x++) 
				{
					uchar* ptr = temp.ptr<uchar>(x) + 1;
					for (int y = 0; y < 100; y++) 
					{
						uchar cr = *ptr;
						ptr++;
						uchar cb = *ptr;
						ptr++;
						ptr++;
						if ((cr - cb_left_criteria) < cb_right_criteria && (cb - cr_left_criteria) < cr_right_criteria) 
						{
							sum++;
							mark.at<uchar>(i + x, j + y) = 1;
						}
						else 
						{
							mark.at<uchar>(i + x, j + y) = 0;
						}
					}
				}
			}
		}
	}

	int Threshold = 0;
	int Ymax = 0;
	int Light[256] = { 0 };
	for (int i = 0; i < row; i++) 
	{
		for (int j = 0; j < col; j++)
		{
			if (mark.at<uchar>(i, j) == 1) 
			{
				Light[(int)(in.at<Vec3b>(i, j)[0])]++;
			}
			Ymax = max(Ymax, (int)(in.at<Vec3b>(i, j)[0]));
		}
	}

	int sum2 = 0;
	for (int i = 255; i >= 0; i--)
	{
		sum2 += Light[i];
		if (sum2 >= sum * 0.1) 
		{
			Threshold = i;
			break;
		}
	}

	double Blue = 0;
	double Green = 0;
	double Red = 0;
	int cnt2 = 0;
	for (int i = 0; i < row; i++) 
	{
		for (int j = 0; j < col; j++)
		{
			if (mark.at<uchar>(i, j) == 1 && (int)(in.at<Vec3b>(i, j)[0]) >= Threshold)
			{
				Blue += 1.0 * src.at<Vec3b>(i, j)[0];
				Green += 1.0 * src.at<Vec3b>(i, j)[1];
				Red += 1.0 * src.at<Vec3b>(i, j)[2];
				cnt2++;
			}
		}
	}
	Blue /= cnt2;
	Green /= cnt2;
	Red /= cnt2;

	Mat dst(row, col, CV_8UC3);
	double maxY = Ymax;
	for (int i = 0; i < row; i++) 
	{
		for (int j = 0; j < col; j++)
		{
			int B = (int)(maxY * src.at<Vec3b>(i, j)[0] / Blue);
			int G = (int)(maxY * src.at<Vec3b>(i, j)[1] / Green);
			int R = (int)(maxY * src.at<Vec3b>(i, j)[2] / Red);
			if (B > 255) B = 255; else if (B < 0) B = 0;
			if (G > 255) G = 255; else if (G < 0) G = 0;
			if (R > 255) R = 255; else if (R < 0) R = 0;
			dst.at<Vec3b>(i, j)[0] = B;
			dst.at<Vec3b>(i, j)[1] = G;
			dst.at<Vec3b>(i, j)[2] = R;
		}
	}
	cvtColor(dst, outputMat, COLOR_BGR2BGRA);
}

void OpenCV::OpenCVLibrary::MosaicEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	Mat src;
	cvtColor(inputMat, src, COLOR_BGRA2BGR);
	Mat temp = src.clone();

	int level = 15;

	for (int i = 0; i < src.rows; i += level)
	{
		for (int j = 0; j < src.cols; j += level)
		{
			Rect2i mosaicRect;

			if (j + level > src.cols && i + level > src.rows)
			{
				mosaicRect = Rect2i(j, i, src.cols - j, src.rows - i);
			}
			else if(j + level > src.cols)
			{
				mosaicRect = Rect2i(j, i, src.cols - j, level);
			}
			else if (i + level > src.rows)
			{
				mosaicRect = Rect2i(j, i, level, src.rows - i);
			}
			else
			{
				mosaicRect = Rect2i(j, i, level, level);
			}

			Mat roi = src(mosaicRect);

			Scalar scalar = Scalar(
				temp.at<Vec3b>(i, j)[0],
				temp.at<Vec3b>(i, j)[1],
				temp.at<Vec3b>(i, j)[2]);

			Mat roiCopy = Mat(mosaicRect.size(), CV_8UC3, scalar);
			roiCopy.copyTo(roi);
		}
	}
	cvtColor(src, outputMat, COLOR_BGR2BGRA);
}

void OpenCV::OpenCVLibrary::ExtendImageBorder(SoftwareBitmap^ input, SoftwareBitmap^ output, Color Colors, int Top, int Left, int Right, int Bottom)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}
	cvtColor(inputMat, inputMat, COLOR_BGRA2BGR);

	copyMakeBorder(inputMat, inputMat, Top, Bottom, Left, Right, BORDER_CONSTANT, Scalar(Colors.B,Colors.G,Colors.R));
	cvtColor(inputMat, outputMat, COLOR_BGR2BGRA);
}

void OpenCV::OpenCVLibrary::RotateEffect(SoftwareBitmap^ input, SoftwareBitmap^ output, int Angle)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	switch (Angle)
	{

	case 90:
	{
		transpose(inputMat, outputMat);
		flip(outputMat, outputMat, 1);
		break; 
	}

	case 180:
	{
		flip(inputMat, outputMat, -1);
		break;
	}

	case -90:
	{
		transpose(inputMat, outputMat);
		flip(outputMat, outputMat, 0);
		break;
	}

	default:
		throw ref new Platform::Exception(5, "Angle 仅支持90、180、-90度");
	}
}

void OpenCV::OpenCVLibrary::FlipEffect(SoftwareBitmap^ input, SoftwareBitmap^ output, bool FlipByX)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	if (FlipByX)
	{
		flip(inputMat, outputMat, 0);
	}
	else 
	{
		flip(inputMat, outputMat, 1);
	}
}

void OpenCV::OpenCVLibrary::AdjustBrightnessContrast(SoftwareBitmap^ input, SoftwareBitmap^ output, double Alpha, double Beta)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	inputMat.convertTo(outputMat, -1, Alpha, Beta);
}

void OpenCV::OpenCVLibrary::InvertEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	bitwise_not(inputMat, outputMat);
}

void OpenCV::OpenCVLibrary::GenenateResizedThumbnail(SoftwareBitmap^ input, SoftwareBitmap^ output, int Height, int Width)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	if (inputMat.cols > inputMat.rows)
	{
		int CropSize = inputMat.rows;
		int StartXPosition = (inputMat.cols - CropSize) / 2;
		int StartYPosition = 0;
		cv::Rect CropRect = cv::Rect(StartXPosition, StartYPosition, CropSize, CropSize);
		Mat CroppedMat = inputMat(CropRect);
		resize(CroppedMat, outputMat, cv::Size(Width, Height), 0, 0, CV_INTER_LINEAR);
	}
	else
	{
		int CropSize = inputMat.cols;
		int StartYPosition = (inputMat.rows - CropSize) / 2;
		int StartXPosition = 0;
		cv::Rect CropRect = cv::Rect(StartXPosition, StartYPosition, CropSize, CropSize);
		Mat CroppedMat = inputMat(CropRect);
		resize(CroppedMat, outputMat, cv::Size(Width, Height), 0, 0, CV_INTER_LINEAR);
	}
}

void OpenCV::OpenCVLibrary::ThresholdEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	threshold(inputMat, outputMat, 100, 255, THRESH_BINARY);
}

void OpenCV::OpenCVLibrary::SketchEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	medianBlur(inputMat, outputMat, 7);

	Laplacian(outputMat, outputMat, CV_8U, 5);

	threshold(outputMat, outputMat, 80, 255, THRESH_BINARY_INV);
}

void OpenCV::OpenCVLibrary::GaussianBlurEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	cv::GaussianBlur(inputMat, outputMat, cv::Size(0, 0), 8);
}

void OpenCV::OpenCVLibrary::SepiaEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	Mat temp = Mat(cv::Size(inputMat.cols, inputMat.rows), CV_8UC3);
	cvtColor(inputMat, temp, COLOR_BGRA2BGR);

	cv::Mat kernel =
		(cv::Mat_<float>(3, 3)
			<<
			0.272, 0.534, 0.131,
			0.349, 0.686, 0.168,
			0.393, 0.769, 0.189);

	cv::transform(temp, temp, kernel);
	cvtColor(temp, outputMat, COLOR_BGR2BGRA);
}

void OpenCV::OpenCVLibrary::OilPaintingEffect(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}

	Mat temp = Mat(cv::Size(inputMat.cols, inputMat.rows), CV_8UC3);
	cvtColor(inputMat, temp, COLOR_BGRA2BGR);

	pyrMeanShiftFiltering(temp, temp, 12, 60);

	cvtColor(temp, outputMat, COLOR_BGR2BGRA);
}

void OpenCV::OpenCVLibrary::CalculateHistogram(SoftwareBitmap^ input, SoftwareBitmap^ output)
{
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		throw ref new Platform::Exception(4, "在将SoftwareBitmap转换成Mat时出现问题");
	}
	Mat temp,addtemp;
	Mat RGBChannels[3],OutputRGB[3];
	int hissize[1] = { 512 };
	int histHeight = 512, histWidth = 512;
	float ranges[2] = { 0.0,256.0 };
	const float* range = &ranges[0];

	cvtColor(inputMat, temp, COLOR_BGRA2BGR);
	split(temp, RGBChannels);
	calcHist(&RGBChannels[0], 1, 0, Mat(), OutputRGB[0], 1, hissize, &range);
	calcHist(&RGBChannels[1], 1, 0, Mat(), OutputRGB[1], 1, hissize, &range);
	calcHist(&RGBChannels[2], 1, 0, Mat(), OutputRGB[2], 1, hissize, &range);

	normalize(OutputRGB[0], OutputRGB[0], 0, histHeight, NORM_MINMAX);
	normalize(OutputRGB[1], OutputRGB[1], 0, histHeight, NORM_MINMAX);
	normalize(OutputRGB[2], OutputRGB[2], 0, histHeight, NORM_MINMAX);

	Mat histImage(histHeight, histWidth, CV_8UC3, Scalar(20, 20, 20));
	int binStep = cvRound((float)histWidth / (float)hissize[0]);

	for (int i = 1; i < hissize[0]; i++)
	{
		line(histImage,
			cv::Point(binStep * (i - 1), histHeight - cvRound(OutputRGB[0].at<float>(i - 1))),
			cv::Point(binStep * (i), histHeight - cvRound(OutputRGB[0].at<float>(i))),
			Scalar(255, 0, 0));
		line(histImage,
			cv::Point(binStep * (i - 1), histHeight - cvRound(OutputRGB[1].at<float>(i - 1))),
			cv::Point(binStep * (i), histHeight - cvRound(OutputRGB[1].at<float>(i))),
			Scalar(0, 255, 0));
		line(histImage,
			cv::Point(binStep * (i - 1), histHeight - cvRound(OutputRGB[2].at<float>(i - 1))),
			cv::Point(binStep * (i), histHeight - cvRound(OutputRGB[2].at<float>(i))),
			Scalar(0, 0, 255));
	}

	cvtColor(histImage, outputMat, COLOR_BGR2BGRA);
}


bool OpenCVLibrary::GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity)
{
	BitmapBuffer^ bmpBuffer = bitmap->LockBuffer(BitmapBufferAccessMode::ReadWrite);
	IMemoryBufferReference^ reference = bmpBuffer->CreateReference();
	ComPtr<IMemoryBufferByteAccess> pBufferByteAccess;
	if ((reinterpret_cast<IInspectable*>(reference)->QueryInterface(IID_PPV_ARGS(&pBufferByteAccess))) != S_OK)
	{
		return false;
	}
	if (pBufferByteAccess->GetBuffer(pPixelData, capacity) != S_OK)
	{
		return false;
	}
	return true;
}

bool OpenCVLibrary::TryConvert(SoftwareBitmap^ from, Mat& convertedMat)
{
	if (!IsInitialized)
	{
		Initialize();
		IsInitialized = true;
	}

	unsigned char* pPixels = nullptr;
	unsigned int capacity = 0;
	if (!GetPointerToPixelData(from, &pPixels, &capacity))
	{
		return false;
	}
	Mat mat(from->PixelHeight, from->PixelWidth, CV_8UC4, (void*)pPixels);
	convertedMat = mat;
	return true;
}
