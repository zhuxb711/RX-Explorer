using System;
using System.Collections.Generic;
using System.Linq;
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

        private static readonly ResourceLoader Loader = ResourceLoader.GetForViewIndependentUse();
        private static readonly Dictionary<string, string> ResourceCache = new Dictionary<string, string>();

        public static void SwitchTo(LanguageEnum Language)
        {
            switch (Language)
            {
                case LanguageEnum.Chinese:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                        break;
                    }
                case LanguageEnum.English:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                        break;
                    }
                case LanguageEnum.French:
                    {
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                        break;
                    }
            }
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
                catch
                {
                    throw new Exception("Could not find the key");
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
                            CurrentLanguage = LanguageEnum.Chinese;
                            ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                            break;
                        }
                    case 1:
                        {
                            CurrentLanguage = LanguageEnum.English;
                            ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                            break;
                        }
                    case 2:
                        {
                            CurrentLanguage = LanguageEnum.French;
                            ApplicationLanguages.PrimaryLanguageOverride = "fr-FR";
                            break;
                        }
                }
            }
            else
            {
                string PrimaryLanguage = GlobalizationPreferences.Languages[0];

                if (PrimaryLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.Chinese;
                    ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                }
                else if (PrimaryLanguage.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.French;
                    ApplicationLanguages.PrimaryLanguageOverride = "fr-FR";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                }
                else
                {
                    CurrentLanguage = LanguageEnum.English;
                    ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                    ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                }
            }
        }
    }
}
