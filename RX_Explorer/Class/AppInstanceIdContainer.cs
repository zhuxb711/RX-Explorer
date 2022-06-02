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

                if (string.IsNullOrEmpty(SavedInfo))
                {
                    return string.Empty;
                }
                else
                {
                    return JsonSerializer.Deserialize<IEnumerable<string>>(SavedInfo).LastOrDefault() ?? string.Empty;
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

            if (string.IsNullOrEmpty(SavedInfo))
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(new string[] { CurrentId });
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<IEnumerable<string>>(SavedInfo).Except(new string[] { CurrentId }).Append(CurrentId));
            }
        }

        public static void UngisterId(string Id)
        {
            string SavedInfo = Convert.ToString(ApplicationData.Current.LocalSettings.Values["LastActiveGuid"]);

            if (!string.IsNullOrEmpty(SavedInfo))
            {
                ApplicationData.Current.LocalSettings.Values["LastActiveGuid"] = JsonSerializer.Serialize(JsonSerializer.Deserialize<IEnumerable<string>>(SavedInfo).Except(new string[] { CurrentId }));
            }
        }

        public static void ClearAll()
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("LastActiveGuid"))
            {
                ApplicationData.Current.LocalSettings.Values.Remove("LastActiveGuid");
            }
        }
    }
}
