using System;

namespace ShareClassLibrary
{
    public sealed class LinkDataPackage
    {
        public string LinkPath { get; set; }

        public string LinkTargetPath { get; set; }

        public string[] Argument { get; set; }

        public string Comment { get; set; }

        public string WorkDirectory { get; set; }
        
        public WindowState WindowState { get; set; }

        public int HotKey { get; set; }

        public bool NeedRunAsAdmin { get; set; }

        public byte[] IconData { get; set; } 
    }
}
