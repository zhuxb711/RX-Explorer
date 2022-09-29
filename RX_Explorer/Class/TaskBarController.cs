using System;
using System.Threading.Tasks;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
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

        public static void SetBadge(uint Count)
        {
            if (Count > 0)
            {
                XmlDocument BadgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);

                if (BadgeXml.SelectSingleNode("/badge") is XmlElement Element)
                {
                    Element.SetAttribute("value", Convert.ToString(Count));
                }

                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(new BadgeNotification(BadgeXml));
            }
            else
            {
                BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
            }
        }

        public static async Task<bool> SetTaskBarProgressAsync(int Value)
        {
            if ((DateTimeOffset.Now - LastTaskBarUpdatedTime).TotalMilliseconds >= 1000 || Value is 100 or 0)
            {
                LastTaskBarUpdatedTime = DateTimeOffset.Now;

                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                {
                    return await Exclusive.Controller.SetTaskBarProgressAsync(Value);
                }
            }

            return true;
        }
    }
}
