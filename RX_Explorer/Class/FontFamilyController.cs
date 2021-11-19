using SharpDX.DirectWrite;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;
using Windows.UI.Xaml;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace RX_Explorer.Class
{
    public static class FontFamilyController
    {
        public static InstalledFonts Current { get; }

        public static InstalledFonts Default { get; }

        private static IReadOnlyList<InstalledFonts> FontCache;

        public static bool SwitchTo(InstalledFonts NewFont)
        {
            if (NewFont == Default)
            {
                ApplicationData.Current.LocalSettings.Values.Remove("FontFamilyOverride");
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] = JsonSerializer.Serialize(NewFont);
            }

            return Current != NewFont;
        }

        public static void Initialize()
        {
            Application.Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(Current.Name);
        }

        public static InstalledFonts Transform(FontFamily Fonts)
        {
            using (Factory FontFactory = new Factory())
            using (FontCollection Collection = FontFactory.GetSystemFontCollection(false))
            {
                if (Collection.FindFamilyName(Fonts.Source, out int FontIndex))
                {
                    using (SharpDX.DirectWrite.FontFamily Family = Collection.GetFontFamily(FontIndex))
                    using (LocalizedStrings LocalizedNames = Family.FamilyNames)
                    {
                        for (int FamilyIndex = 0; FamilyIndex < LocalizedNames.Count; FamilyIndex++)
                        {
                            if (LocalizedNames.GetString(FamilyIndex).Equals(Fonts.Source, StringComparison.OrdinalIgnoreCase))
                            {
                                return new InstalledFonts(FontIndex, FamilyIndex, Fonts.Source);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static IEnumerable<InstalledFonts> GetInstalledFontFamily()
        {
            return FontCache ??= GetInstalledFontFamilyCore();
        }

        private static IReadOnlyList<InstalledFonts> GetInstalledFontFamilyCore()
        {
            List<InstalledFonts> FontList = new List<InstalledFonts>();

            using (Factory FontFactory = new Factory())
            using (FontCollection Collection = FontFactory.GetSystemFontCollection(false))
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

                for (int FontIndex = 0; FontIndex < Collection.FontFamilyCount; FontIndex++)
                {
                    try
                    {
                        using (SharpDX.DirectWrite.FontFamily Family = Collection.GetFontFamily(FontIndex))
                        using (Font Font = Family.GetFont(0))
                        using (FontFace Face = new FontFace(Font))
                        {
                            if (!Face.IsSymbolFont)
                            {
                                using (LocalizedStrings LocalizedNames = Family.FamilyNames)
                                {
                                    int FamilyIndex = 0;

                                    if (LocalizedNames.FindLocaleName(CurrentLocaleName, out int LocaleNameIndex))
                                    {
                                        FamilyIndex = LocaleNameIndex;
                                    }
                                    else if (LocalizedNames.FindLocaleName("en-US", out int EngNameIndex))
                                    {
                                        FamilyIndex = EngNameIndex;
                                    }

                                    string DisplayName = LocalizedNames.GetString(FamilyIndex);

                                    if (!string.IsNullOrEmpty(DisplayName))
                                    {
                                        FontList.Add(new InstalledFonts(FontIndex, FamilyIndex, DisplayName));
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not load the fontfamily, index: {FontIndex}");
                    }
                }
            }

            return FontList;
        }

        static FontFamilyController()
        {
            Default = Transform(FontFamily.XamlAutoFontFamily) ?? Transform(new FontFamily("Segoe UI"));

            if (ApplicationData.Current.LocalSettings.Values["FontFamilyOverride"] is string OverrideString)
            {
                Current = JsonSerializer.Deserialize<InstalledFonts>(OverrideString);
            }
            else
            {
                Current = Default;
            }
        }
    }
}
