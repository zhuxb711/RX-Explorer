using RX_Explorer.Class;
using RX_Explorer.View;
using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;

namespace RX_Explorer
{
    public static class Program
    {
        static void Main(string[] args)
        {
            if (AppInstance.GetInstances().Count == 0)
            {
                AppInstanceIdContainer.ClearAll();
            }

            if (AppInstance.GetActivatedEventArgs() is ToastNotificationActivatedEventArgs ToastActivate)
            {
                switch (ToastActivate.Argument)
                {
                    case "RecoveryRestartTips":
                    case "EnterBackgroundTips":
                        {
                            while (!string.IsNullOrEmpty(AppInstanceIdContainer.LastActiveId))
                            {
                                if (AppInstance.GetInstances().Any((Ins) => Ins.Key == AppInstanceIdContainer.LastActiveId))
                                {
                                    if (AppInstance.FindOrRegisterInstanceForKey(AppInstanceIdContainer.LastActiveId) is AppInstance TargetInstance)
                                    {
                                        TargetInstance.RedirectActivationTo();
                                    }

                                    break;
                                }
                                else
                                {
                                    AppInstanceIdContainer.UngisterId(AppInstanceIdContainer.LastActiveId);
                                }
                            }

                            break;
                        }
                    case "Restart":
                        {
                            AppInstanceIdContainer.RegisterId(AppInstance.FindOrRegisterInstanceForKey(Guid.NewGuid().ToString()).Key);
                            Application.Start((_) => new App());
                            break;
                        }
                }
            }
            else
            {
                if (SettingPage.IsAlwaysLaunchNewProcessEnabled || string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
                {
                    AppInstanceIdContainer.RegisterId(AppInstance.FindOrRegisterInstanceForKey(Guid.NewGuid().ToString()).Key);
                    Application.Start((_) => new App());
                }
                else
                {
                    do
                    {
                        if (AppInstance.GetInstances().Any((Ins) => Ins.Key == AppInstanceIdContainer.LastActiveId))
                        {
                            if (AppInstance.FindOrRegisterInstanceForKey(AppInstanceIdContainer.LastActiveId) is AppInstance TargetInstance)
                            {
                                TargetInstance.RedirectActivationTo();
                            }
                            else
                            {
                                AppInstanceIdContainer.RegisterId(AppInstance.FindOrRegisterInstanceForKey(Guid.NewGuid().ToString()).Key);
                                Application.Start((_) => new App());
                            }

                            break;
                        }
                        else
                        {
                            AppInstanceIdContainer.UngisterId(AppInstanceIdContainer.LastActiveId);
                        }
                    }
                    while (!string.IsNullOrEmpty(AppInstanceIdContainer.LastActiveId));
                }
            }
        }
    }
}
