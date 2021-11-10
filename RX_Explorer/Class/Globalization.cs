using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        private static readonly ConcurrentDictionary<string, string> ResourceCache;

        public static bool SwitchTo(LanguageEnum Language)
        {
            switch (Language)
            {
                case LanguageEnum.Chinese_Simplified:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打开";
                        break;
                    }
                case LanguageEnum.English:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Open in RX-Explorer";
                        break;
                    }
                case LanguageEnum.French:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Ouvrir dans RX-Explorer";
                        break;
                    }
                case LanguageEnum.Chinese_Traditional:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 3;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打開";
                        break;
                    }
                case LanguageEnum.Spanish:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 4;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Abrir con RX-Explorer";
                        break;
                    }
                case LanguageEnum.German:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 5;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Öffnen Sie im RX-Explorer";
                        break;
                    }
            }

            if (Language != CurrentLanguage)
            {
                ApplicationData.Current.LocalSettings.Values["RefreshQuickStart"] = true;
                return true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values.Remove("RefreshQuickStart");
                return false;
            }
        }

        public static void Initialize()
        {
            ApplicationLanguages.PrimaryLanguageOverride = CurrentLanguage switch
            {
                LanguageEnum.Chinese_Simplified => "zh-Hans",
                LanguageEnum.English => "en-US",
                LanguageEnum.French => "fr-FR",
                LanguageEnum.Chinese_Traditional => "zh-Hant",
                LanguageEnum.Spanish => "es",
                LanguageEnum.German => "de-DE",
                _ => "en-US"
            };
        }

        public static string GetString(string Key)
        {
            if (ResourceCache.TryGetValue(Key, out string ExistingValue))
            {
                return ExistingValue;
            }
            else
            {
                try
                {
                    string TranslatedValue = Loader.GetString(Key).Replace(@"\r", Environment.NewLine);

                    if (string.IsNullOrEmpty(TranslatedValue))
                    {
                        throw new Exception("TranslatedValue is empty");
                    }
                    else
                    {
                        ResourceCache.TryAdd(Key, TranslatedValue);
                    }

                    return TranslatedValue;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not find the key, return empty string instead");

#if DEBUG
                    if (Debugger.IsAttached)
                    {
                        Debugger.Break();
                    }
                    else
                    {
                        Debugger.Launch();
                    }
#endif

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
                    case 4:
                        {
                            CurrentLanguage = LanguageEnum.Spanish;
                            break;
                        }
                    case 5:
                        {
                            CurrentLanguage = LanguageEnum.German;
                            break;
                        }
                }
            }
            else
            {
                string PrimaryLanguage = GlobalizationPreferences.Languages[0];

                if (PrimaryLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    if (PrimaryLanguage.Contains("Hant", StringComparison.OrdinalIgnoreCase))
                    {
                        CurrentLanguage = LanguageEnum.Chinese_Traditional;
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hant";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 3;
                        ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打開";
                    }
                    else if (PrimaryLanguage.Contains("Hans", StringComparison.OrdinalIgnoreCase))
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
                else if (PrimaryLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.Spanish;
                    ApplicationLanguages.PrimaryLanguageOverride = "es";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 4;
                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Abrir con RX-Explorer";
                }
                else if (PrimaryLanguage.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.German;
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 5;
                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Öffnen Sie im RX-Explorer";
                    ApplicationLanguages.PrimaryLanguageOverride = "de-DE";
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
            ResourceCache = new ConcurrentDictionary<string, string>();
        }
    }
}
