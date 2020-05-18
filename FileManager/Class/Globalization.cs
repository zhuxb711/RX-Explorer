using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.Storage;
using Windows.System.UserProfile;
using Windows.UI.Core;

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
        public static LanguageEnum CurrentLanguage { get; private set; }

        private static ResourceLoader Loader;
        private static readonly Dictionary<string, string> ResourceCache = new Dictionary<string, string>();

        private static bool IsInitialized = false;

        public static void SwitchTo(LanguageEnum Language)
        {
            switch (Language)
            {
                case LanguageEnum.Chinese:
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 0;
                        break;
                    }
                case LanguageEnum.English:
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 1;
                        break;
                    }
                case LanguageEnum.French:
                    {
                        ApplicationLanguages.PrimaryLanguageOverride = "fr";
                        ApplicationData.Current.LocalSettings.Values["LanguageOverride"] = 2;
                        break;
                    }
            }
        }

        public async static Task Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Loader = ResourceLoader.GetForCurrentView();
            });

            if (ApplicationData.Current.LocalSettings.Values["LanguageOverride"] is int LanguageIndex)
            {
                switch (LanguageIndex)
                {
                    case 0:
                        {
                            CurrentLanguage = LanguageEnum.Chinese;
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
                }
            }
            else
            {
                string PrimaryLanguage = GlobalizationPreferences.Languages[0];

                if (PrimaryLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.Chinese;
                    ApplicationLanguages.PrimaryLanguageOverride = "zh-Hans";
                }
                else if (PrimaryLanguage.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
                {
                    CurrentLanguage = LanguageEnum.French;
                    ApplicationLanguages.PrimaryLanguageOverride = "fr";
                }
                else
                {
                    CurrentLanguage = LanguageEnum.English;
                    ApplicationLanguages.PrimaryLanguageOverride = "en-US";
                }
            }

            IsInitialized = true;
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
                        throw new Exception("Could not find the key");
                    }
                    else
                    {
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
    }
}
