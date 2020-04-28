using System;
using Windows.Globalization;
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

        static Globalization()
        {
            Language = GlobalizationPreferences.Languages[0].StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? LanguageEnum.Chinese : LanguageEnum.English;
            ApplicationLanguages.PrimaryLanguageOverride = Language == LanguageEnum.English ? "en-US" : "zh-Hans";
        }
    }
}
