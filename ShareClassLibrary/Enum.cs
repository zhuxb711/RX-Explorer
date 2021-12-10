namespace ShareClassLibrary
{
    public enum CreateType
    {
        File,
        Folder
    }

    public enum CollisionOptions
    {
        None,
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
        Optimize_Sequential,
        Optimize_RandomAccess,
    }

    public enum CommandType
    {
        Test,
        RunExecutable,
        ToggleQuicklook,
        SwitchQuicklook,
        Check_Quicklook,
        Get_Association,
        Default_Association,
        Get_RecycleBinItems,
        Restore_RecycleItem,
        Delete_RecycleItem,
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
        GetHiddenItemData,
        GetThumbnail,
        SetFileAttribute,
        GetMIMEContentType,
        GetUrlTargetPath,
        GetAllInstalledApplication,
        CheckPackageFamilyNameExist,
        GetInstalledApplication,
        GetDocumentProperties,
        LaunchUWP,
        GetThumbnailOverlay,
        SetAsTopMostWindow,
        RemoveTopMostWindow,
        GetFileHandle,
        GetTooltipText,
        GetVariablePathSuggestion,
        GetDirectoryMonitorHandle,
        MapToUNCPath,
        SetTaskBarProgress,
        GetProperties
    }
}
