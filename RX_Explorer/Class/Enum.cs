using System;

namespace RX_Explorer.Class
{
    public enum RecoveryReason
    {
        None,
        Crash,
        Freeze,
        Restart
    }

    public enum SecureAreaLockMode
    {
        InstantLockMode,
        RestartLockMode
    }

    public enum SLEOriginType
    {
        File,
        Folder,
    }

    public enum UIStyle
    {
        Normal,
        Clearly
    }

    public enum DragBehaivor
    {
        Copy,
        Move,
        None
    }

    public enum ProgramPriority
    {
        InnerViewer,
        SystemDefault
    }

    public enum SpecialPathEnum
    {
        OneDrive,
        Dropbox
    }

    public enum PriorityLevel
    {
        High = 0,
        Normal = 1,
        Low = 2,
    }

    public enum ListViewLocation
    {
        Presenter,
        RecycleBin,
        Search,
        Compression
    }

    public enum ShutdownBehaivor
    {
        CloseApplication,
        CloseInnerViewer,
        AskEveryTime
    }

    public enum BluetoothPanelMode
    {
        None = 0,
        PairMode = 1,
        TextMode = 2,
        TransferMode = 4,
    }

    public enum BluetoothEventKind
    {
        Aborted,
        Connected,
        TransferSuccess,
        TransferFailure,
        ConnectionFailure
    }

    public enum DriveContextMenuType
    {
        Locked,
        Portable,
        Normal
    }

    public enum StateChangeType
    {
        Unknown_Action = 0,
        Added_Action = 1,
        Removed_Action = 2,
        Modified_Action = 3,
        Rename_Action_OldName = 4,
        Rename_Action_NewName = 5
    }

    [Flags]
    public enum UWP_HANDLE_ACCESS_OPTIONS : uint
    {
        NONE = 0,
        READ = 0x120089,
        WRITE = 0x120116,
        DELETE = 0x10000
    }

    [Flags]
    public enum UWP_HANDLE_OPTIONS : uint
    {
        NONE = 0,
        OPEN_REQUIRING_OPLOCK = 0x40000,
        DELETE_ON_CLOSE = 0x4000000,
        SEQUENTIAL_SCAN = 0x8000000,
        RANDOM_ACCESS = 0x10000000,
        NO_BUFFERING = 0x20000000,
        OVERLAPPED = 0x40000000,
        WRITE_THROUGH = 0x80000000
    }

    [Flags]
    public enum UWP_HANDLE_SHARING_OPTIONS : uint
    {
        SHARE_NONE = 0,
        SHARE_READ = 0x1,
        SHARE_WRITE = 0x2,
        SHARE_DELETE = 0x4
    }

    public enum InfoTipType
    {
        UpdateAvailable,
        MandatoryUpdateAvailable,
        LanguageRestartRequired,
        FontFamilyRestartRequired,
        ConfigRestartRequired,
        FullTrustBusy,
        UIStyleRestartRequired
    }

    public enum LabelKind
    {
        None,
        PredefineLabel1,
        PredefineLabel2,
        PredefineLabel3,
        PredefineLabel4
    }

    public enum CommonChangeType
    {
        Added,
        Removed
    }

    public enum NewCompressionItemType
    {
        File,
        Directory
    }

    public enum SLEVersion
    {
        SLE100 = 100,
        SLE110 = 110,
        SLE150 = 150,
        SLE200 = 200,
        SLE210 = 210
    }

    public enum SLEKeySize
    {
        AES128 = 128,
        AES256 = 256
    }

    public enum SyncStatus
    {
        Unknown,
        AvailableOnline,
        AvailableOffline,
        Sync,
        Excluded
    }

    public enum CompressionAlgorithm
    {
        None,
        GZip,
        BZip2,
        Deflated
    }

    public enum AddressBlockType
    {
        Normal,
        Gray
    }

    public enum OperationStatus
    {
        Waiting,
        Preparing,
        Processing,
        Error,
        Completed,
        Cancelling,
        Cancelled,
        NeedAttention
    }

    public enum Version
    {
        Windows10_1809 = 7,
        Windows10_1903 = 8,
        Windows10_1909 = 9,
        Windows10_2004 = 10,
        Windows11 = 11
    }

    public enum StartupMode
    {
        CreateNewTab,
        LastOpenedTab,
        SpecificTab
    }

    public enum ShellLinkType
    {
        Normal,
        UWP
    }

    [Flags]
    public enum ColorFilterCondition
    {
        None = 0,
        PredefineLabel1 = 1,
        PredefineLabel2 = 2,
        PredefineLabel3 = 4,
        PredefineLabel4 = 8
    }

    [Flags]
    public enum NameFilterCondition
    {
        None = 0,
        From_A_To_G = 1,
        From_H_To_N = 2,
        From_O_To_T = 4,
        From_U_To_Z = 8,
        Other = 16,
        Regex = 32
    }

    [Flags]
    public enum ModTimeFilterCondition
    {
        None = 0,
        Range = 1,
        One_Month_Ago = 2,
        Three_Month_Ago = 4,
        Long_Ago = 8
    }

    [Flags]
    public enum SizeFilterCondition
    {
        None = 0,
        Smaller = 1,
        Medium = 2,
        Larger = 4,
        Huge = 8
    }

    public enum SearchCategory
    {
        BuiltInEngine,
        EverythingEngine
    }

    public enum JumpListGroup
    {
        Library,
        Recent
    }

    public enum LoadMode
    {
        None,
        OnlyFile,
        All,
        Unknown
    }

    public enum SearchEngineFlyoutMode
    {
        AlwaysPopup,
        UseEverythingEngineAsDefault,
        UseBuildInEngineAsDefault
    }

    public enum ThumbnailStatus
    {
        Normal,
        HalfOpacity
    }

    public enum GroupDirection
    {
        Ascending,
        Descending
    }

    public enum GroupTarget
    {
        None,
        Name,
        Type,
        ModifiedTime,
        Size
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public enum SortTarget
    {
        Name,
        Type,
        ModifiedTime,
        Size,
        OriginPath,
        RecycleDate,
        Path,
        CompressedSize,
        CompressionRate
    }

    public enum SortStyle
    {
        None,
        UseFileSystemStyle
    }

    public enum CompressionType
    {
        Zip,
        Tar,
        Gzip,
        BZip2
    }

    public enum CompressionLevel
    {
        Max = 9,
        Standard = 4,
        PackageOnly = 0
    }

    public enum FilterType
    {
        Origin,
        Invert,
        Gray,
        Threshold,
        Sketch,
        GaussianBlur,
        Sepia,
        Mosaic,
        OilPainting
    }

    public enum QuickStartType
    {
        Application,
        WebSite,
        AddButton
    }

    public enum AuthenticatorState
    {
        RegisterSuccess,
        UserCanceled,
        CredentialNotFound,
        UnknownError,
        WindowsHelloUnsupport,
        VerifyPassed,
        VerifyFailed,
        UserNotRegistered
    }

    public enum LanguageEnum
    {
        Chinese_Simplified,
        English,
        French,
        Chinese_Traditional,
        Spanish,
        German
    }

    public enum BackgroundBrushType
    {
        DefaultAcrylic,
        CustomAcrylic,
        Picture,
        SolidColor,
        BingPicture,
        Mica
    }

    public enum LibraryType
    {
        Downloads,
        Desktop,
        Videos,
        Pictures,
        Document,
        Music,
        OneDrive,
        UserCustom
    }

    [Flags]
    public enum BasicFilters
    {
        File = 1,
        Folder = 2
    }
}
