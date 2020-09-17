using Windows.UI.ViewManagement;

namespace RX_Explorer.Class
{
    public sealed class TaskBarController
    {
        private static readonly object Locker = new object();

        public static void SetText(string Text)
        {
            lock (Locker)
            {
                ApplicationView.GetForCurrentView().Title = string.IsNullOrEmpty(Text) ? string.Empty : Text;
            }
        }
    }
}
