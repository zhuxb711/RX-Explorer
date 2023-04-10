using AuxiliaryTrustProcess.Class;
using System.Text.Json.Serialization;

namespace AuxiliaryTrustProcess.Interface
{
    [JsonDerivedType(typeof(ElevationCopyData), nameof(ElevationCopyData))]
    [JsonDerivedType(typeof(ElevationCreateNewData), nameof(ElevationCreateNewData))]
    [JsonDerivedType(typeof(ElevationDeleteData), nameof(ElevationDeleteData))]
    [JsonDerivedType(typeof(ElevationMoveData), nameof(ElevationMoveData))]
    [JsonDerivedType(typeof(ElevationRemoteCopyData), nameof(ElevationRemoteCopyData))]
    [JsonDerivedType(typeof(ElevationRenameData), nameof(ElevationRenameData))]
    [JsonDerivedType(typeof(ElevationSetDriveCompressStatusData), nameof(ElevationSetDriveCompressStatusData))]
    [JsonDerivedType(typeof(ElevationSetDriveIndexStatusData), nameof(ElevationSetDriveIndexStatusData))]
    [JsonDerivedType(typeof(ElevationSetDriveLabelData), nameof(ElevationSetDriveLabelData))]
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$TypeDiscriminator", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    internal interface IElevationData
    {

    }
}
