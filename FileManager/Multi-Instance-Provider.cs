using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;

namespace FileManager
{
    public static class Multi_Instance_Provider
    {
        static void Main(string[] args)
        {
            if (ApplicationData.Current.LocalSettings.Values["EnableMultiInstanceSupport"] is bool Enable)
            {
                if (Enable)
                {
                    Application.Start((p) => new App());
                }
                else
                {
                    var Instances = AppInstance.FindOrRegisterInstanceForKey("RX_SingleInstanceFlag");
                    if(Instances.IsCurrentInstance)
                    {
                        Application.Start((p) => new App());
                    }
                    else
                    {
                        Instances.RedirectActivationTo();
                    }
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableMultiInstanceSupport"] = true;

                Application.Start((p) => new App());
            }
        }
    }
}
