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
                    _ = AppInstance.FindOrRegisterInstanceForKey("RX_SingleInstanceFlag");
                    Application.Start((p) => new App());
                }
                else
                {
                    try
                    {
                        var Instances = AppInstance.FindOrRegisterInstanceForKey("RX_SingleInstanceFlag");
                        if (Instances.IsCurrentInstance)
                        {
                            Application.Start((p) => new App());
                        }
                        else
                        {
                            if (AppInstance.RecommendedInstance == null)
                            {
                                Instances.RedirectActivationTo();
                            }
                            else
                            {
                                AppInstance.RecommendedInstance.RedirectActivationTo();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        Application.Start((p) => new App());
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
