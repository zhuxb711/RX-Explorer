using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System.Collections.Generic;
using Windows.Globalization;

namespace RX_Explorer.Class
{
    public sealed class SystemFontFamilyController
    {
        public static string CurrentFontFamily { get; set; }

        public static IEnumerable<string> GetAllFontFamilyName()
        {
            using (Factory Fact = new Factory())
            using (FontCollection Collection = Fact.GetSystemFontCollection(new RawBool(false)))
            {
                for (int Index = 0; Index < Collection.FontFamilyCount; Index++)
                {
                    using (FontFamily Font = Collection.GetFontFamily(Index))
                    {
                        if (Font.FamilyNames.FindLocaleName(ApplicationLanguages.PrimaryLanguageOverride, out int NameStringIndex))
                        {
                            yield return Font.FamilyNames.GetString(NameStringIndex);
                        }
                        else if (Font.FamilyNames.FindLocaleName("en-us", out int NameStringIndex1))
                        {
                            yield return Font.FamilyNames.GetString(NameStringIndex1);
                        }
                        else
                        {
                            yield return Font.FamilyNames.GetString(0);
                        }
                    }
                }
            }
        }
    }
}
