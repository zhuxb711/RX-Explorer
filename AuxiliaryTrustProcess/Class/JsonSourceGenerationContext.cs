using AuxiliaryTrustProcess.Interface;
using SharedLibrary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace AuxiliaryTrustProcess.Class
{
    [JsonSerializable(typeof(IElevationData))]
    [JsonSerializable(typeof(IEnumerable<int>))]
    [JsonSerializable(typeof(IEnumerable<string>))]
    [JsonSerializable(typeof(IEnumerable<byte>))]
    [JsonSerializable(typeof(IEnumerable<MTPFileData>))]
    [JsonSerializable(typeof(IEnumerable<StringNaturalAlgorithmData>))]
    [JsonSerializable(typeof(IEnumerable<PermissionDataPackage>))]
    [JsonSerializable(typeof(IEnumerable<VariableDataPackage>))]
    [JsonSerializable(typeof(IEnumerable<InstalledApplicationPackage>))]
    [JsonSerializable(typeof(IEnumerable<AssociationPackage>))]
    [JsonSerializable(typeof(IEnumerable<RecycleBinItemDataPackage>))]
    [JsonSerializable(typeof(IEnumerable<ContextMenuPackage>))]
    [JsonSerializable(typeof(IDictionary<ModifyAttributeAction, FileAttributes>))]
    [JsonSerializable(typeof(IDictionary<string, string>))]
    [JsonSerializable(typeof(MTPDriveVolumnData))]
    [JsonSerializable(typeof(LinkFileData))]
    [JsonSerializable(typeof(UrlFileData))]
    [JsonSerializable(typeof(RemoteClipboardRelatedData))]
    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    internal partial class JsonSourceGenerationContext : JsonSerializerContext
    {

    }
}
