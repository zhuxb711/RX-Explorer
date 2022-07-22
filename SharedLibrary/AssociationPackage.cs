using System;

namespace SharedLibrary
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
                    return other.ExecutablePath.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase) && other.Extension.Equals(Extension, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is AssociationPackage Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return ExecutablePath.GetHashCode() + Extension.GetHashCode();
        }

        public override string ToString()
        {
            return $"Extension: {Extension}, ExecutablePath: {ExecutablePath}";
        }

        public static bool operator ==(AssociationPackage left, AssociationPackage right)
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
                    return left.ExecutablePath.Equals(right.ExecutablePath, StringComparison.OrdinalIgnoreCase) && left.Extension.Equals(right.Extension, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator !=(AssociationPackage left, AssociationPackage right)
        {
            if (left is null)
            {
                return right is not null;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return !left.ExecutablePath.Equals(right.ExecutablePath, StringComparison.OrdinalIgnoreCase) && left.Extension.Equals(right.Extension, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
