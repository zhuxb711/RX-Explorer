using System;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace RX_Explorer.Class
{
    public static class TaskBarController
    {
        private static readonly object Locker = new object();
        private static DateTimeOffset LastTaskBarUpdatedTime = DateTimeOffset.Now;

        public static void SetText(string Text)
        {
            lock (Locker)
            {
                ApplicationView.GetForCurrentView().Title = Text ?? string.Empty;
            }
        }

        public static async Task<bool> SetTaskBarProgressAsync(int Value)
        {
            if ((DateTimeOffset.Now - LastTaskBarUpdatedTime).TotalMilliseconds >= 1000 || Value is 100 or 0)
            {
                LastTaskBarUpdatedTime = DateTimeOffset.Now;

                using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetAvailableControllerAsync(PriorityLevel.High))
                {
                    return await Exclusive.Controller.SetTaskBarProgressAsync(Value);
                }
            }

            return true;
        }
    }
}
