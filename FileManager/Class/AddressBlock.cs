namespace FileManager.Class
{
    public sealed class AddressBlock
    {
        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        public AddressBlock(string Name)
        {
            this.Name = Name;
        }
    }
}
