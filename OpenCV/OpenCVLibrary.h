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
		static void ExtendImageBorder(SoftwareBitmap^ input, SoftwareBitmap^ output, Color Colors, int Top, int Left, int Right, int Bottom);

		static void RotateEffect(SoftwareBitmap^ input, SoftwareBitmap^ output, int Angle);

		static void FlipEffect(SoftwareBitmap^ input, SoftwareBitmap^ output, bool FlipByX);

		static void AdjustBrightnessContrast(SoftwareBitmap^ input, SoftwareBitmap^ output, double Alpha, double Beta);

		static void InvertEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);

		static void GenenateResizedThumbnail(SoftwareBitmap^ input, SoftwareBitmap^ output, int Height, int Width);

		static void ThresholdEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);

		static void SketchEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);

		static void GaussianBlurEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);

		static void SepiaEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);

		static void OilPaintingEffect(SoftwareBitmap^ input, SoftwareBitmap^ output);
	private:
		OpenCVLibrary();

		static bool GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity);

		static bool TryConvert(SoftwareBitmap^ from, Mat& convertedMat);
	};
}