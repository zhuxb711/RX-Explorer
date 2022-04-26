using System;

namespace RX_Explorer.Class
{
    public sealed class InstalledFonts : IEquatable<InstalledFonts>
    {
        public string DisplayName { get; }

        public string Path { get; }

        public string FamilyName { get; }

        public InstalledFonts(string DisplayName, string FamilyName, string Path)
        {
            this.DisplayName = DisplayName;
            this.FamilyName = FamilyName;
            this.Path = Path;
        }

        public override bool Equals(object obj)
        {
            return obj is InstalledFonts Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return FamilyName.GetHashCode() + Path.GetHashCode();
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public bool Equals(InstalledFonts Item)
        {
            if (ReferenceEquals(this, Item))
            {
                return true;
            }
            else
            {
                if (Item == null)
                {
                    return false;
                }
                else
                {
                    return Item.FamilyName == FamilyName && Item.Path == Path;
                }
            }
        }

        public static bool operator ==(InstalledFonts left, InstalledFonts right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InstalledFonts left, InstalledFonts right)
        {
            return !left.Equals(right);
        }
    }
}
