using System;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.UserProfile;

namespace FileManager.Class
{
    /// <summary>
    /// 指示UI语言类型
    /// </summary>
    public static class Globalization
    {
        /// <summary>
        /// 当前使用的语言
        /// </summary>
        public static LanguageEnum Language { get; private set; }

        public static void SwitchTo(LanguageEnum Language)
        {
            if (Language == LanguageEnum.Chinese)
            {
                ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
            }
            else
            {
                ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
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
                            Language = LanguageEnum.Chinese;
                            break;
                        }
                    case 1:
                        {
                            Language = LanguageEnum.English;
                            break;
                        }
                }
            }
            else
            {
                Language = GlobalizationPreferences.Languages[0].StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? LanguageEnum.Chinese : LanguageEnum.English;
                ApplicationLanguages.PrimaryLanguageOverride = Language == LanguageEnum.English ? "en-US" : "zh-Hans";
            }
        }
    }
}
