using System;

namespace RX_Explorer.Class
{
    public sealed class InstalledFonts : IEquatable<InstalledFonts>
    {
        public string Name { get; }

        public string Path { get; }

        public InstalledFonts(string Name, string Path)
        {
            this.Name = Name;
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
                    return string.IsNullOrEmpty(Path) ? Item.Name == Name : Item.Path == Path;
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() + Path.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
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
                    return Item.Name == Name && Item.Path == Path;
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
                    return left.Name == right.Name && left.Path == right.Path;
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
                    return left.Name != right.Name || left.Path != right.Path;
                }
            }
        }
    }
}
