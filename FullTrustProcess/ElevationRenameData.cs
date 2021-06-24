using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FullTrustProcess
{
    public sealed class ElevationRenameData : ElevationDataBase
    {
        public string DesireName { get; }

        public ElevationRenameData(string Source, string DesireName) : this(new string[] { Source }, DesireName)
        {

        }

        [JsonConstructor]
        public ElevationRenameData(IEnumerable<string> Source, string DesireName) : base(Source, null)
        {
            this.DesireName = DesireName;
        }
    }
}
