#pragma once
#include <opencv2\core\core.hpp>
#include <opencv2\imgproc\imgproc.hpp>
#include <vector>
using namespace Windows::Graphics::Imaging;
using namespace Windows::UI;
using namespace std;
using namespace cv;
using namespace Platform;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;

namespace OpenCV
{
	public ref class OpenCVLibrary sealed
	{
	public:
		static void ExtendBitmapBorder(SoftwareBitmap^ input, SoftwareBitmap^ output, Color Colors, int Top, int Left, int Right, int Bottom);

	private:
		OpenCVLibrary();

		static bool GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity);

		static bool TryConvert(SoftwareBitmap^ from, Mat& convertedMat);
	};
}