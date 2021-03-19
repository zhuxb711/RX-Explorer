using System;

namespace ShareClassLibrary
{
    public sealed class LinkDataPackage
    {
        public string LinkPath { get; }

        public string LinkTargetPath { get; }

        public string[] Argument { get; }

        public string Comment { get; }

        public string WorkDirectory { get; }
        
        public WindowState WindowState { get; }

        public int HotKey { get; }

        public bool NeedRunAsAdmin { get; }

        public byte[] IconData { get; } 

        public LinkDataPackage(string LinkPath, string LinkTargetPath, string WorkDirectory, WindowState WindowState, int HotKey, string Comment, bool NeedRunAsAdmin, byte[]? IconData, params string[] Argument)
        {
            this.LinkPath = LinkPath;
            this.LinkTargetPath = LinkTargetPath;
            this.WorkDirectory = WorkDirectory;
            this.WindowState = WindowState;
            this.HotKey = HotKey;
            this.Argument = Argument ?? Array.Empty<string>();
            this.IconData = IconData ?? Array.Empty<byte>();
            this.Comment = Comment;
            this.NeedRunAsAdmin = NeedRunAsAdmin;
        }
    }
}
