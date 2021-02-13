namespace ShareClassLibrary
{
    public sealed class InstalledApplicationPackage
    {
        public byte[] Logo { get; private set; }

        public string AppName { get; private set; }

        public string AppDescription { get; private set; }

        public string AppFamilyName { get; private set; }

        public InstalledApplicationPackage(string AppName, string AppDescription, string AppFamilyName, byte[] Logo)
        {
            this.AppName = AppName;
            this.AppDescription = AppDescription;
            this.AppFamilyName = AppFamilyName;
            this.Logo = Logo;
        }
    }
}
