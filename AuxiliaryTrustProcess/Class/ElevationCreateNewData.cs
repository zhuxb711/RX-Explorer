using AuxiliaryTrustProcess.Interface;
using SharedLibrary;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class ElevationCreateNewData : IElevationData
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
