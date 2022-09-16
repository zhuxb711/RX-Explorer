namespace SharedLibrary
{
    public enum AccountType
    {
        Group,
        User,
        Unknown
    }

    public enum Permissions
    {
        FullControl,
        Modify,
        ListDirectory,
        ReadAndExecute,
        Read,
        Write
    }

    public enum CreateType
    {
        File,
        Folder
    }

    public enum CollisionOptions
    {
        Skip,
        RenameOnCollision,
        OverrideOnCollision
    }

    public enum ModifyAttributeAction
    {
        Add,
        Remove
    }

    public enum WindowState
    {
        Normal = 0,
        Minimized = 1,
        Maximized = 2
    }

    public enum AccessMode
    {
        Read,
        Write,
        ReadWrite,
        Exclusive
    }

    public enum OptimizeOption
    {
        None,
        Sequential,
        RandomAccess,
    }

    public enum MonitorCommandType
    {
        SetRecoveryData,
        RegisterRestartRequest,
        StartMonitor,
        StopMonitor,
        EnableFeature,
        DisableFeature
    }

    public enum MonitorFeature
    {
        CrashMonitor,
        FreezeMonitor
    }

    public enum AuxiliaryTrustProcessCommandType
    {
        Test,
        GetFriendlyTypeName,
        GetPermissions,
        SetDriveLabel,
        SetDriveIndexStatus,
        GetDriveIndexStatus,
        SetDriveCompressionStatus,
        GetDriveCompressionStatus,
        DetectEncoding,
        GetAllEncodings,
        RunExecutable,
        ToggleQuicklook,
        SwitchQuicklook,
        CheckQuicklook,
        ToggleSeer,
        SwitchSeer,
        CheckSeer,
        GetAssociation,
        Default_Association,
        GetRecycleBinItems,
        RestoreRecycleItem,
        DeleteRecycleItem,
        InterceptWinE,
        InterceptFolder,
        RestoreWinEInterception,
        RestoreFolderInterception,
        GetLinkData,
        GetUrlData,
        CreateNew,
        Copy,
        Move,
        Delete,
        Rename,
        EmptyRecycleBin,
        UnlockOccupy,
        EjectUSB,
        GetVariablePath,
        CreateLink,
        UpdateLink,
        UpdateUrl,
        PasteRemoteFile,
        GetContextMenuItems,
        InvokeContextMenuItem,
        CheckIfEverythingAvailable,
        SearchByEverything,
        GetThumbnail,
        SetFileAttribute,
        GetMIMEContentType,
        GetAllInstalledUwpApplication,
        CheckPackageFamilyNameExist,
        GetSpecificInstalledUwpApplication,
        GetDocumentProperties,
        LaunchUWP,
        GetThumbnailOverlay,
        SetAsTopMostWindow,
        RemoveTopMostWindow,
        GetNativeHandle,
        GetTooltipText,
        GetVariablePathList,
        GetDirectoryMonitorHandle,
        MapToUNCPath,
        SetTaskBarProgress,
        GetProperties,
        MTPGetItem,
        MTPCheckExists,
        MTPGetChildItems,
        MTPGetDriveVolumnData,
        MTPCreateSubItem,
        MTPDownloadAndGetHandle,
        MTPReplaceWithNewFile,
        OrderByNaturalStringSortAlgorithm,
        GetSizeOnDisk,
        GetAvailableWslDrivePathList,
        GetRemoteClipboardRelatedData,
        CreateTemporaryFileHandle,
        ConvertToLongPath,
        GetRecyclePathFromOriginPath,
        GetFileAttribute,
        GetProcessHandle,
        SetWallpaperImage
    }
}
