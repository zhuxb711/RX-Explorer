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
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is InstalledFonts Item)
                {
                    return Item.FamilyName == FamilyName && Item.Path == Path;
                }
                else
                {
                    return false;
                }
            }
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
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.FamilyName == right.FamilyName && left.Path == right.Path;
                }
            }
        }

        public static bool operator !=(InstalledFonts left, InstalledFonts right)
        {
            if (left is null)
            {
                return right is object;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return left.FamilyName != right.FamilyName || left.Path != right.Path;
                }
            }
        }
    }
}
