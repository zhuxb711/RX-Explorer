namespace RX_Explorer.Class
{
    public class TerminalProfile
    {
        public string Name { get; set; }

        public string Argument { get; set; }

        public string Path { get; set; }

        public bool RunAsAdmin { get; set; }

        public TerminalProfile(string Name, string Path, string Argument, bool RunAsAdmin)
        {
            this.Name = Name;
            this.Path = Path;
            this.Argument = Argument;
            this.RunAsAdmin = RunAsAdmin;
        }
    }
}
