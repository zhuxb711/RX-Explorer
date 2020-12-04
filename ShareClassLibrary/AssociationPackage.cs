using System;

namespace ShareClassLibrary
{
    public class AssociationPackage
    {
        public bool IsRecommanded { get; }

        public string ExecutablePath { get; }

        public AssociationPackage(string ExecutablePath, bool IsRecommanded)
        {
            this.ExecutablePath = ExecutablePath;
            this.IsRecommanded = IsRecommanded;
        }
    }
}
