using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;

namespace RX_Explorer
{
    public static class Program
    {
        static void Main(string[] args)
        {
            IActivatedEventArgs activatedArgs = AppInstance.GetActivatedEventArgs();

            if (activatedArgs is ToastNotificationActivatedEventArgs)
            {
                return;
            }
            if (args.Length != 0 && AppInstance.RecommendedInstance != null)
            {
                //ApplicationData.Current.LocalSettings.Values["Dir2Open"] = args[0];
                AppInstance.RecommendedInstance.RedirectActivationTo();
            }
            else if (args.Length != 0 && ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] != null &&
                !string.IsNullOrWhiteSpace(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] as string) &&
                AppInstance.FindOrRegisterInstanceForKey(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] as string) is AppInstance inst &&
                !inst.IsCurrentInstance)
            {
                //ApplicationData.Current.LocalSettings.Values["Dir2Open"] = args[0];
                inst.RedirectActivationTo();
            }
            else if (args.Length != 0 && AppInstance.GetInstances().Count != 0)
            {
                //ApplicationData.Current.LocalSettings.Values["Dir2Open"] = args[0];
                AppInstance.GetInstances()[0].RedirectActivationTo();
            }
            else 
            {
                string key = Guid.NewGuid().ToString();
                AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(key);
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = key;
                if (Instance.IsCurrentInstance)
                {
                    Application.Start((p) => new App());
                }
                else
                {
                    Instance.RedirectActivationTo();
                }
            }
        }
    }
}
