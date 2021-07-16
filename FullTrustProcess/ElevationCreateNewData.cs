using ShareClassLibrary;

namespace FullTrustProcess
{
    public sealed class ElevationCreateNewData : IElevationData
    {
        public CreateType Type { get; }

        public string Path { get; }

        public ElevationCreateNewData(CreateType Type, string Path)
        {
            this.Type = Type;
            this.Path = Path;
        }
    }
}
