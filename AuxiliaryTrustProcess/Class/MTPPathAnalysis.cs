using System;
using System.Linq;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class MTPPathAnalysis
    {
        public string DeviceId { get; }

        public string RelativePath { get; }

        public MTPPathAnalysis(string Path)
        {
            string[] SplitArray = new string(Path.Skip(4).ToArray()).Split(@"\", StringSplitOptions.RemoveEmptyEntries);

            if (SplitArray.Length > 0)
            {
                DeviceId = @$"\\?\{SplitArray[0]}";
                RelativePath = @$"\{string.Join('\\', SplitArray.Skip(1))}";
            }
        }
    }
}
