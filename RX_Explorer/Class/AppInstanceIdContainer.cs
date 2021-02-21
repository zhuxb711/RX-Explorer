using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
                    return JsonSerializer.Deserialize<List<string>>(SavedInfo).LastOrDefault();
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public static void RegisterId(string Id)
        {
            CurrentId = Id;

            SetCurrentIdAsLastActivateId();
        }

        public static void SetCurrentIdAsLastActivateId()
        {
            string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

            if (!string.IsNullOrEmpty(SavedInfo))
            {
                List<string> Collection = JsonSerializer.Deserialize<List<string>>(SavedInfo);

                if (Collection.Contains(CurrentId))
                {
                    Collection.Remove(CurrentId);
                }

                Collection.Add(CurrentId);

                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(Collection);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(new List<string>() { CurrentId });
            }
        }

        public static void UngisterId(string Id)
        {
            string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

            if (!string.IsNullOrEmpty(SavedInfo))
            {
                List<string> Collection = JsonSerializer.Deserialize<List<string>>(SavedInfo);

                if (Collection.Contains(Id))
                {
                    Collection.Remove(Id);
                    ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(Collection);
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
