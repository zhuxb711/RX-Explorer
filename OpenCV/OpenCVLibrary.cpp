#include "pch.h"
#include "OpenCVLibrary.h"
#include "MemoryBuffer.h"
#define PI 3.1415926

using namespace OpenCV;
using namespace Platform;

OpenCVLibrary::OpenCVLibrary()
{
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
