using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;

namespace RX_Explorer
{
    public static class Program
    {
        static void Main(string[] args)
        {
            IActivatedEventArgs activatedArgs = AppInstance.GetActivatedEventArgs();

            AppInstance Instance = AppInstance.FindOrRegisterInstanceForKey(Guid.NewGuid().ToString());

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
