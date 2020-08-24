using System;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class AppInstanceIdContainer
    {
        private readonly static object Locker = new object();

        private volatile static string Id;

        public static string CurrentId
        {
            get
            {
                lock (Locker)
                {
                    return Id;
                }
            }
            set
            {
                Id = value;
                LastActiveId = value;
            }
        }

        public static string LastActiveId
        {
            get
            {
                lock (Locker)
                {
                    return Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = value;
            }
        }
    }
}
