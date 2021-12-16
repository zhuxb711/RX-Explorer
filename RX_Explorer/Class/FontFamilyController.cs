using SharpDX;
using SharpDX.DirectWrite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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
                ApplicationData.Current.LocalSettings.Values.Remove("DefaultFontFamilyOverride");
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["DefaultFontFamilyOverride"] = JsonSerializer.Serialize(NewFont);
            }

            return Current != NewFont;
        }

        public static void Initialize()
        {
            Application.Current.Resources["ContentControlThemeFontFamily"] = new FontFamily(Current.Name);
        }

        public static InstalledFonts Transform(FontFamily Fonts)
        {
            return GetInstalledFontFamily().FirstOrDefault((Font) => Font.Name.Equals(Fonts.Source, StringComparison.OrdinalIgnoreCase));
        }

        public static IEnumerable<InstalledFonts> GetInstalledFontFamily()
        {
            return FontCache ??= new List<InstalledFonts>(GetInstalledFontFamilyCore());
        }

        private static IEnumerable<InstalledFonts> GetInstalledFontFamilyCore()
        {
            ConcurrentBag<InstalledFonts> FontList = new ConcurrentBag<InstalledFonts>();

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

                Parallel.For(0, Collection.FontFamilyCount, (FamilyIndex, _) =>
                {
                    try
                    {
                        using (SharpDX.DirectWrite.FontFamily Family = Collection.GetFontFamily(FamilyIndex))
                        {
                            for (int FontIndex = 0; FontIndex < Family.FontCount; FontIndex++)
                            {
                                using (Font Font = Family.GetFont(0))
                                using (FontFace Face = new FontFace(Font))
                                {
                                    if (!Face.IsSymbolFont)
                                    {
                                        string FontFilePath = null;

                                        foreach (FontFile FontFile in Face.GetFiles())
                                        {
                                            try
                                            {
                                                DataPointer ReferenceKey = FontFile.GetReferenceKey();

                                                if (FontFile.Loader is FontFileLoaderNative OriginalLoader)
                                                {
                                                    using (LocalFontFileLoader Loader = OriginalLoader.QueryInterface<LocalFontFileLoader>())
                                                    {
                                                        FontFilePath = Loader.GetFilePath(ReferenceKey);
                                                    }
                                                }

                                                break;
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, $"Could not get the font file path, family index: {FamilyIndex}, font index: {FontIndex}");
                                            }
                                            finally
                                            {
                                                FontFile.Dispose();
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(FontFilePath))
                                        {
                                            LocalizedStrings PreferLocalizedNames = null;
                                            LocalizedStrings FullLocalizedNames = null;

                                            if (Font.GetInformationalStrings(InformationalStringId.PreferRedFamilyNames, out LocalizedStrings OutPreferLocalizedNames))
                                            {
                                                PreferLocalizedNames = OutPreferLocalizedNames;
                                            }

                                            if (Font.GetInformationalStrings(InformationalStringId.FullName, out LocalizedStrings OutFullLocalizedNames))
                                            {
                                                FullLocalizedNames = OutFullLocalizedNames;
                                            }

                                            if (PreferLocalizedNames != null)
                                            {
                                                try
                                                {
                                                    string PreferDisplayName = null;
                                                    string FullDisplayName = null;

                                                    if (PreferLocalizedNames.FindLocaleName(CurrentLocaleName, out int PreferLocaleNameIndex))
                                                    {
                                                        PreferDisplayName = PreferLocalizedNames.GetString(PreferLocaleNameIndex);
                                                    }

                                                    if (string.IsNullOrEmpty(PreferDisplayName))
                                                    {
                                                        if (FullLocalizedNames != null)
                                                        {
                                                            try
                                                            {
                                                                if (FullLocalizedNames.FindLocaleName(CurrentLocaleName, out int FullLocaleNameIndex))
                                                                {
                                                                    FullDisplayName = FullLocalizedNames.GetString(FullLocaleNameIndex);
                                                                }

                                                                if (string.IsNullOrEmpty(FullDisplayName))
                                                                {
                                                                    if (PreferLocalizedNames.FindLocaleName("en-US", out int PreferEngNameIndex))
                                                                    {
                                                                        PreferDisplayName = PreferLocalizedNames.GetString(PreferEngNameIndex);
                                                                    }

                                                                    if (string.IsNullOrEmpty(PreferDisplayName))
                                                                    {
                                                                        if (FullLocalizedNames.FindLocaleName("en-US", out int FullEngNameIndex))
                                                                        {
                                                                            FullDisplayName = FullLocalizedNames.GetString(FullEngNameIndex);
                                                                        }

                                                                        if (string.IsNullOrEmpty(FullDisplayName))
                                                                        {
                                                                            if (PreferLocalizedNames.Count > 0)
                                                                            {
                                                                                PreferDisplayName = PreferLocalizedNames.GetString(0);
                                                                            }
                                                                            else if (FullLocalizedNames.Count > 0)
                                                                            {
                                                                                FullDisplayName = FullLocalizedNames.GetString(0);
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                            finally
                                                            {
                                                                FullLocalizedNames.Dispose();
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (PreferLocalizedNames.FindLocaleName("en-US", out int PreferEngNameIndex))
                                                            {
                                                                PreferDisplayName = PreferLocalizedNames.GetString(PreferEngNameIndex);
                                                            }

                                                            if (string.IsNullOrEmpty(PreferDisplayName) && PreferLocalizedNames.Count > 0)
                                                            {
                                                                PreferDisplayName = PreferLocalizedNames.GetString(0);
                                                            }
                                                        }
                                                    }

                                                    if (!string.IsNullOrEmpty(PreferDisplayName))
                                                    {
                                                        FontList.Add(new InstalledFonts(PreferDisplayName, FontFilePath));
                                                        break;
                                                    }
                                                    else if (!string.IsNullOrEmpty(FullDisplayName))
                                                    {
                                                        FontList.Add(new InstalledFonts(FullDisplayName, FontFilePath));
                                                        break;
                                                    }
                                                }
                                                finally
                                                {
                                                    PreferLocalizedNames.Dispose();
                                                }
                                            }
                                            else if (FullLocalizedNames != null)
                                            {
                                                try
                                                {
                                                    string FullDisplayName = null;

                                                    if (FullLocalizedNames.FindLocaleName(CurrentLocaleName, out int FullLocaleNameIndex))
                                                    {
                                                        FullDisplayName = FullLocalizedNames.GetString(FullLocaleNameIndex);
                                                    }

                                                    if (string.IsNullOrEmpty(FullDisplayName))
                                                    {
                                                        if (FullLocalizedNames.FindLocaleName("en-US", out int FullEngNameIndex))
                                                        {
                                                            FullDisplayName = FullLocalizedNames.GetString(FullEngNameIndex);
                                                        }
                                                    }

                                                    if (!string.IsNullOrEmpty(FullDisplayName))
                                                    {
                                                        FontList.Add(new InstalledFonts(FullDisplayName, FontFilePath));
                                                        break;
                                                    }
                                                    else if (FullLocalizedNames.Count > 0)
                                                    {
                                                        FontList.Add(new InstalledFonts(FullLocalizedNames.GetString(0), FontFilePath));
                                                        break;
                                                    }
                                                }
                                                finally
                                                {
                                                    FullLocalizedNames.Dispose();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not load the font family index: {FamilyIndex}");
                    }
                });
            }

            return FontList.Distinct().OrderByLikeFileSystem((Fonts) => Fonts.Name, SortDirection.Ascending);
        }

        static FontFamilyController()
        {
            Default = Transform(FontFamily.XamlAutoFontFamily) ?? Transform(new FontFamily("Segoe UI"));

            if (ApplicationData.Current.LocalSettings.Values["DefaultFontFamilyOverride"] is string OverrideString)
            {
                Current = JsonSerializer.Deserialize<InstalledFonts>(OverrideString);

                if (!GetInstalledFontFamily().Contains(Current))
                {
                    Current = Default;
                    ApplicationData.Current.LocalSettings.Values.Remove("DefaultFontFamilyOverride");
                }
            }
            else
            {
                Current = Default;
            }
        }
    }
}
