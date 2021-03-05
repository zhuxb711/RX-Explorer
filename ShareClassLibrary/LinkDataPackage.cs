using System;

namespace ShareClassLibrary
{
    public sealed class LinkDataPackage
    {
        public string LinkPath { get; }

        public string LinkTargetPath { get; }

        public string[] Argument { get; }

        public string Description { get; }
        
        public bool NeedRunAsAdmin { get; }

        public byte[] IconData { get; } 

        public LinkDataPackage(string LinkPath, string LinkTargetPath, string Description, bool NeedRunAsAdmin, byte[]? IconData, params string[] Argument)
        {
            this.LinkPath = LinkPath;
            this.LinkTargetPath = LinkTargetPath;
            this.Argument = Argument ?? Array.Empty<string>();
            this.IconData = IconData ?? Array.Empty<byte>();
            this.Description = Description;
            this.NeedRunAsAdmin = NeedRunAsAdmin;
        }
    }
}
