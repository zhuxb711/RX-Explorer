namespace ShareClassLibrary
{
    public sealed class ContextMenuPackage
    {
        public string Description { get; }

        public string Verb { get; }

        public byte[] IconData { get; }

        public ContextMenuPackage(string Description, string Verb, byte[] IconData)
        {
            this.Description = Description;
            this.Verb = Verb;
            this.IconData = IconData;
        }
    }
}
