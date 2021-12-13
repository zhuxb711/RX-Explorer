using System;

namespace RX_Explorer.Class
{
    public sealed class InstalledFonts : IEquatable<InstalledFonts>
    {
        public int FontIndex { get; }

        public int FamilyIndex { get; }

        public string Name { get; }

        public InstalledFonts(int FontIndex, int FamilyIndex, string Name)
        {
            this.FontIndex = FontIndex;
            this.FamilyIndex = FamilyIndex;
            this.Name = Name;
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
                    return Item.Name == Name;
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }

        public bool Equals(InstalledFonts other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    return other.Name == Name;
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
                    return left.Name == right.Name;
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
                    return left.Name != right.Name;
                }
            }
        }
    }
}
