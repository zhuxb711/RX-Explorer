using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.UserProfile;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 指示UI语言类型
    /// </summary>
    public static class Globalization
    {
        /// <summary>
        /// 当前使用的语言
        /// </summary>
        public static LanguageEnum CurrentLanguage { get; private set; }

        private static readonly ResourceLoader Loader;
        private static readonly Dictionary<string, string> ResourceCache;

        public static bool SwitchTo(LanguageEnum Language)
        {
            switch (Language)
            {
                case LanguageEnum.Chinese_Simplified:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打开";
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                        break;
                    }
                case LanguageEnum.English:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Open in RX-Explorer";
                        ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                        break;
                    }
                case LanguageEnum.French:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Ouvrir dans RX-Explorer";
                        ApplicationLanguages.PrimaryLanguageOverride = "fr-FR";
                        break;
                    }
                case LanguageEnum.Chinese_Traditional:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 3;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打開";
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hant";
                        break;
                    }
            }

            return Language != CurrentLanguage;
        }

        public static string GetString(string Key)
        {
            if (ResourceCache.TryGetValue(Key, out string Value))
            {
                return Value;
            }
            else
            {
                try
                {
                    Value = Loader.GetString(Key);

                    if (string.IsNullOrEmpty(Value))
                    {
                        throw new Exception("Value is empty");
                    }
                    else
                    {
                        Value = Value.Replace(@"\r", Environment.NewLine);
                        ResourceCache.Add(Key, Value);
                        return Value;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not find the key, return empty string instead");

                    if (Package.Current.IsDevelopmentMode)
                    {
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }
                        else
                        {
                            Debugger.Launch();
                        }
                    }

                    return string.Empty;
                }
            }
        }

        static Globalization()
        {
            if (ApplicationData.Current.LocalSettings.Values["LanguageOverride"] is int LanguageIndex)
            {
                switch (LanguageIndex)
                {
                    case 0:
                        {
                            CurrentLanguage = LanguageEnum.Chinese_Simplified;
                            break;
                        }
                    case 1:
                        {
                            CurrentLanguage = LanguageEnum.English;
                            break;
                        }
                    case 2:
                        {
                            CurrentLanguage = LanguageEnum.French;
                            break;
                        }
                    case 3:
                        {
                            CurrentLanguage = LanguageEnum.Chinese_Traditional;
                            break;
                        }
                }
            }
            else
            {
                string PrimaryLanguage = GlobalizationPreferences.Languages[0];

                if (PrimaryLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    if (PrimaryLanguage.Contains("Hant"))
                    {
                        CurrentLanguage = LanguageEnum.Chinese_Traditional;
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hant";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 3;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打開";
                    }
                    else if (PrimaryLanguage.Contains("Hans"))
                    {
                        CurrentLanguage = LanguageEnum.Chinese_Simplified;
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打开";
                    }
                    else
                    {
                        CurrentLanguage = LanguageEnum.English;
                        ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Open in RX-Explorer";
                    }
                }
                else if (PrimaryLanguage.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.French;
                    ApplicationLanguages.PrimaryLanguageOverride = "fr-FR";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Ouvrir dans RX-Explorer";
                }
                else
                {
                    CurrentLanguage = LanguageEnum.English;
                    ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Open in RX-Explorer";
                }
            }

            Loader = ResourceLoader.GetForViewIndependentUse();
            ResourceCache = new Dictionary<string, string>();
        }
    }
}
