using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace RX_Explorer.Class
{
    public static class FontFamilyController
    {
        public static FontFamily Current
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] is string SavedFontFamilyName)
                {
                    return new FontFamily(SavedFontFamilyName);
                }
                else
                {
                    return FontFamily.XamlAutoFontFamily;
                }
            }
        }

        public static bool SwitchTo(FontFamily NewFont)
        {
            if (ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] is string SavedFontFamilyName)
            {
                if (!SavedFontFamilyName.Equals(NewFont.Source, StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] = NewFont.Source;
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] = NewFont.Source;
            }

            return !NewFont.Source.Equals(Current.Source, StringComparison.OrdinalIgnoreCase);
        }

        public static void Initialize()
        {
            Application.Current.Resources["ContentControlThemeFontFamily"] = Current;
        }

        public static IReadOnlyList<string> GetExistingFontFamily()
        {
            string CurrentLocaleName = Globalization.CurrentLanguage switch
            {
                LanguageEnum.Chinese_Simplified => "zh-Hans",
                LanguageEnum.Chinese_Traditional => "zh-Hant",
                LanguageEnum.English => "en-US",
                LanguageEnum.French => "fr-FR",
                LanguageEnum.Spanish => "es",
                LanguageEnum.German => "de-DE",
                _ => throw new NotSupportedException()
            };

            return CanvasTextFormat.GetSystemFontFamilies(CurrentLocaleName == "en-US" ? new string[] { "en-US" } : new string[] { CurrentLocaleName, "en-US" });
        }
    }
}
