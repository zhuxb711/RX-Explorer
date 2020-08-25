using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class AppInstanceIdContainer
    {
        public static string CurrentId { get; private set; }

        public static string LastActiveId
        {
            get
            {
                string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

                if (!string.IsNullOrEmpty(SavedInfo))
                {
                    List<string> Collection = JsonConvert.DeserializeObject<List<string>>(SavedInfo);
                    return Collection.LastOrDefault();
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public static void RegisterCurrentId(string Id)
        {
            CurrentId = Id;

            SetAsLastActivateId();
        }

        public static void SetAsLastActivateId()
        {
            string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

            if (!string.IsNullOrEmpty(SavedInfo))
            {
                List<string> Collection = JsonConvert.DeserializeObject<List<string>>(SavedInfo);

                if (Collection.Contains(CurrentId))
                {
                    Collection.Remove(CurrentId);
                }

                Collection.Add(CurrentId);

                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonConvert.SerializeObject(Collection);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonConvert.SerializeObject(new List<string>() { CurrentId });
            }
        }

        public static void UngisterCurrentId()
        {
            string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

            if (!string.IsNullOrEmpty(SavedInfo))
            {
                List<string> Collection = JsonConvert.DeserializeObject<List<string>>(SavedInfo);

                if (Collection.Contains(CurrentId))
                {
                    Collection.Remove(CurrentId);
                    ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonConvert.SerializeObject(Collection);
                }
            }
        }

        public static void ClearAll()
        {
            if(ApplicationData.Current.LocalSettings.Values.ContainsKey("LastActiveGuid"))
            {
                ApplicationData.Current.LocalSettings.Values.Remove("LastActiveGuid");
            }
        }
    }
}
