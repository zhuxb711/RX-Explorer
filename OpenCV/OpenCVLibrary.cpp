#include "pch.h"
#include "OpenCVLibrary.h"
#include "MemoryBuffer.h"

using namespace OpenCV;
using namespace Platform;

OpenCVLibrary::OpenCVLibrary()
{
}

void OpenCV::OpenCVLibrary::ExtendBitmapBorder(SoftwareBitmap^ input, SoftwareBitmap^ output, Color Colors, int Top, int Left, int Right, int Bottom)
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
