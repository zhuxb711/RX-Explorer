using System;

namespace ShareClassLibrary
{
    public sealed class AssociationPackage : IEquatable<AssociationPackage>
    {
        public bool IsRecommanded { get; }

        public string ExecutablePath { get; }

        public string Extension { get; }

        public AssociationPackage(string Extension, string ExecutablePath, bool IsRecommanded)
        {
            this.Extension = Extension.ToLower();
            this.ExecutablePath = ExecutablePath;
            this.IsRecommanded = IsRecommanded;
        }

        public bool Equals(AssociationPackage other)
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
                    return other.ExecutablePath.Equals(ExecutablePath) && other.Extension.Equals(Extension);
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is AssociationPackage Item)
                {
                    return Item.ExecutablePath.Equals(ExecutablePath) && Item.Extension.Equals(Extension);
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return ExecutablePath.GetHashCode() + Extension.GetHashCode();
        }

        public override string ToString()
        {
            return $"Extension: {Extension}, ExecutablePath: {ExecutablePath}";
        }
    }
}
