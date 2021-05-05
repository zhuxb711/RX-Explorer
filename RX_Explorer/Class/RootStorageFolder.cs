using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class RootStorageFolder : FileSystemStorageFolder
    {
        private static RootStorageFolder instance;

        public static RootStorageFolder Instance
        {
            get
            {
                return instance ??= new RootStorageFolder();
            }
        }

        public override string Name
        {
            get
            {
                return Globalization.GetString("MainPage_PageDictionary_ThisPC_Label");
            }
        }

        public override string DisplayName
        {
            get
            {
                return Globalization.GetString("MainPage_PageDictionary_ThisPC_Label");
            }
        }

        protected override bool CheckIfPropertiesLoaded()
        {
            return true;
        }

        protected override Task LoadMorePropertiesCore(bool ForceUpdate)
        {
            return Task.CompletedTask;
        }

        private RootStorageFolder() : base("RootFolderUniquePath", default)
        {

        }
    }
}
