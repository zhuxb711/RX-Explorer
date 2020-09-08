namespace RX_Explorer.Class
{
    public class TerminalProfile
    {
        public string Name { get; set; }

        public string Argument { get; set; }

        public string Path { get; set; }

        public TerminalProfile(string Name, string Path, string Argument)
        {
            this.Name = Name;
            this.Path = Path;
            this.Argument = Argument;
        }
    }
}
