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

            if (activatedArgs is ToastNotificationActivatedEventArgs)
            {
                return;
            }

            if (AppInstance.GetInstances().Count == 0)
            {
                AppInstanceIdContainer.ClearAll();
            }

            if (activatedArgs is CommandLineActivatedEventArgs CmdActivate)
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
                        AppInstanceIdContainer.RegisterCurrentId(InstanceId);

                        Application.Start((p) => new App());
                    }
                }
                else
                {
                    if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("AlwaysStartNew"))
                    {
                        ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] = true;
                    }

                    bool AlwaysStartNew = Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"]);

                    if (AlwaysStartNew)
                    {
                        string InstanceId = Guid.NewGuid().ToString();
                        AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                        AppInstanceIdContainer.RegisterCurrentId(InstanceId);

                        Application.Start((p) => new App());
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
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
                            AppInstanceIdContainer.RegisterCurrentId(InstanceId);

                            Application.Start((p) => new App());
                        }
                    }
                }
            }
            else if (activatedArgs is FileActivatedEventArgs)
            {
                if (!string.IsNullOrWhiteSpace(AppInstanceIdContainer.LastActiveId))
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
                    AppInstanceIdContainer.RegisterCurrentId(InstanceId);

                    Application.Start((p) => new App());
                }
            }
            else
            {
                string InstanceId = Guid.NewGuid().ToString();
                AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(InstanceId);
                AppInstanceIdContainer.RegisterCurrentId(InstanceId);

                Application.Start((p) => new App());
            }
        }
    }
}
