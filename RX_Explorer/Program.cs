using System;
using System.Linq;
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

            if (activatedArgs is CommandLineActivatedEventArgs CmdActivate)
            {
                if (CmdActivate.Operation.Arguments.StartsWith("RX-Explorer.exe"))
                {
                    if (AppInstance.RecommendedInstance != null)
                    {
                        AppInstance.RecommendedInstance.RedirectActivationTo();
                    }
                    else if (ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] is string LastGuid
                             && !string.IsNullOrWhiteSpace(LastGuid)
                             && AppInstance.FindOrRegisterInstanceForKey(LastGuid) is AppInstance TargetInstance
                             && !TargetInstance.IsCurrentInstance)
                    {
                        TargetInstance.RedirectActivationTo();
                    }
                    else if (AppInstance.GetInstances().FirstOrDefault() is AppInstance ExistInstance)
                    {
                        ExistInstance.RedirectActivationTo();
                    }
                    else
                    {
                        string InstanceId = Guid.NewGuid().ToString();
                        AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                        ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = InstanceId;

                        Application.Start((p) => new App());
                    }
                }
                else
                {
                    string InstanceId = Guid.NewGuid().ToString();
                    AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                    ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = InstanceId;

                    Application.Start((p) => new App());
                }
            }
            else
            {
                string InstanceId = Guid.NewGuid().ToString();
                AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = InstanceId;

                Application.Start((p) => new App());
            }
        }
    }
}
