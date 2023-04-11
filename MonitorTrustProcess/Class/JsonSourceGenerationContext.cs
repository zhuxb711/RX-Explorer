using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MonitorTrustProcess.Class
{
    [JsonSerializable(typeof(IDictionary<string, string>))]
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class JsonSourceGenerationContext : JsonSerializerContext
    {

    }
}
