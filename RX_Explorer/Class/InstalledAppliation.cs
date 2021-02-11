using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class InstalledAppliation
    {
        public BitmapImage Logo { get; private set; }

        public string AppName
        {
            get
            {
                return PackageObject?.DisplayName ?? string.Empty;
            }
        }

        public string AppDescription
        {
            get
            {
                return PackageObject?.Description ?? string.Empty;
            }
        }

        public string AppFamilyName
        {
            get
            {
                return PackageObject?.Id.FamilyName;
            }
        }

        public Package PackageObject { get; private set; }

        public static async Task<InstalledAppliation> CreateAsync(Package Pack)
        {
            try
            {
                RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(128, 128));

                using (IRandomAccessStreamWithContentType LogoStream = await Reference.OpenReadAsync())
                {
                    InstalledAppliation UWP = new InstalledAppliation
                    {
                        Logo = new BitmapImage(),
                        PackageObject = Pack
                    };

                    await UWP.Logo.SetSourceAsync(LogoStream);

                    return UWP;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw when loading uwp package: \"{Pack.DisplayName}\"");

                InstalledAppliation UWP = new InstalledAppliation
                {
                    Logo = new BitmapImage(),
                    PackageObject = Pack
                };

                return UWP;
            }
        }

        private InstalledAppliation()
        {

        }
    }
}
