using System;

namespace ShareClassLibrary
{
    public sealed class HyperlinkPackage
    {
        public string LinkPath { get; }

        public string LinkTargetPath { get; }

        public string[] Argument { get; }

        public string Description { get; }
        
        public bool NeedRunAsAdmin { get; }

        public HyperlinkPackage(string LinkPath, string LinkTargetPath, string[] Argument, string Description, bool NeedRunAsAdmin)
        {
            this.LinkPath = LinkPath;
            this.LinkTargetPath = LinkTargetPath;
            this.Argument = Argument ?? Array.Empty<string>();
            this.Description = Description;
            this.NeedRunAsAdmin = NeedRunAsAdmin;
        }
    }
}
