using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class InstalledApplication
    {
        public string AppName { get; private set; }

        public string AppDescription { get; private set; }

        public string AppFamilyName { get; private set; }

        public BitmapImage Logo { get; private set; }

        private byte[] LogoData { get; set; }

        public async Task<IRandomAccessStream> CreateStreamFromLogoAsync()
        {
            if (LogoData.Length > 0)
            {
                return await Helper.CreateRandomAccessStreamAsync(LogoData);
            }

            throw new NotSupportedException("Could not generate the logo stream");
        }

        public static async Task<InstalledApplication> CreateAsync(InstalledApplicationPackage Pack)
        {
            InstalledApplication App = new InstalledApplication
            {
                AppName = Pack.AppName,
                AppDescription = Pack.AppDescription,
                AppFamilyName = Pack.AppFamilyName,
                LogoData = Pack.Logo
            };

            if (Pack.Logo.Length > 0)
            {
                using (IRandomAccessStream Stream = await Helper.CreateRandomAccessStreamAsync(Pack.Logo))
                {
                    App.Logo = await Helper.CreateBitmapImageAsync(Stream);
                }
            }

            return App;
        }

        private InstalledApplication()
        {

        }
    }
}
