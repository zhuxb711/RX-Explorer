using RX_Explorer.Class;
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

            if (AppInstance.GetInstances().Count == 0)
            {
                AppInstanceIdContainer.ClearAll();
            }

            switch (activatedArgs)
            {
                case ToastNotificationActivatedEventArgs ToastActivate:
                    {
                        switch (ToastActivate.Argument)
                        {
                            case "EnterBackgroundTips":
                                {
                                    if (AppInstance.RecommendedInstance != null)
                                    {
                                        AppInstance.RecommendedInstance.RedirectActivationTo();
                                    }
                                    else if (!string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
                                    {
                                        do
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
                                        while (!string.IsNullOrEmpty(AppInstanceIdContainer.LastActiveId));
                                    }

                                    break;
                                }
                            case "Restart":
                                {
                                    string InstanceId = Guid.NewGuid().ToString();
                                    AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                                    AppInstanceIdContainer.RegisterId(InstanceId);

                                    Application.Start((p) => new App());
                                    break;
                                }
                        }

                        break;
                    }
                case CommandLineActivatedEventArgs CmdActivate:
                    {
                        if (CmdActivate.Operation.Arguments.StartsWith("RX-Explorer.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (AppInstance.RecommendedInstance != null)
                            {
                                AppInstance.RecommendedInstance.RedirectActivationTo();
                            }
                            else if (!string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
                            {
                                do
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
                                while (!string.IsNullOrEmpty(AppInstanceIdContainer.LastActiveId));
                            }
                            else
                            {
                                string InstanceId = Guid.NewGuid().ToString();
                                AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                                AppInstanceIdContainer.RegisterId(InstanceId);

                                Application.Start((p) => new App());
                            }
                        }
                        else
                        {
                            bool AlwaysStartNew = true;

                            if (ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] is bool StartNew)
                            {
                                AlwaysStartNew = StartNew;
                            }

                            if (AlwaysStartNew)
                            {
                                string InstanceId = Guid.NewGuid().ToString();
                                AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                                AppInstanceIdContainer.RegisterId(InstanceId);

                                Application.Start((p) => new App());
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
                                {
                                    string InstanceId = Guid.NewGuid().ToString();
                                    AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                                    AppInstanceIdContainer.RegisterId(InstanceId);

                                    Application.Start((p) => new App());
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

                        break;
                    }
                default:
                    {
                        string InstanceId = Guid.NewGuid().ToString();

                        AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                        AppInstanceIdContainer.RegisterId(InstanceId);
                        Application.Start((p) => new App());
                        break;
                    }
            }
        }
    }
}
