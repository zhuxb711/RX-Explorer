using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class InstalledApplication
    {
        public string AppName { get; private set; }

        public string AppDescription { get; private set; }

        public string AppFamilyName { get; private set; }

        public BitmapImage Logo { get; private set; } = new BitmapImage();

        private byte[] LogoData { get; set; }

        public Stream CreateStreamFromLogoData()
        {
            return LogoData.Length > 0 ? new MemoryStream(LogoData) : null;
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
                using (MemoryStream Stream = new MemoryStream(Pack.Logo))
                {
                    await App.Logo.SetSourceAsync(Stream.AsRandomAccessStream());
                }
            }

            return App;
        }

        private InstalledApplication()
        {

        }
    }
}
