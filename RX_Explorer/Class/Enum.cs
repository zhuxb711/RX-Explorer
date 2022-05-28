using System;

namespace RX_Explorer.Class
{
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

    public enum UWP_HANDLE_ACCESS_OPTIONS : uint
    {
        NONE = 0,
        READ = 0x120089,
        WRITE = 0x120116,
        DELETE = 0x10000
    }

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
        FullTrustBusy
    }

    public enum ColorTag
    {
        Transparent,
        Orange,
        Green,
        Purple,
        Blue
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
        Version_1_0_0 = 100,
        Version_1_1_0 = 110,
        Version_1_5_0 = 150
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
        Orange = 1,
        Green = 2,
        Purple = 4,
        Blue = 8
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

    public enum CreateOption
    {
        OpenIfExist,
        GenerateUniqueName,
        ReplaceExisting
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
        None = 0,
        OnlyFile = 1,
        All = 2,
        Unknown = 4
    }

    public enum SearchEngineFlyoutMode
    {
        AlwaysPopup,
        UseEverythingEngineAsDefault,
        UseBuildInEngineAsDefault
    }

    public enum ThumbnailStatus
    {
        Normal = 0,
        HalfOpacity = 1
    }

    public enum GroupDirection
    {
        Ascending = 0,
        Descending = 1
    }

    public enum GroupTarget
    {
        None = 0,
        Name = 1,
        Type = 2,
        ModifiedTime = 4,
        Size = 8
    }

    public enum SortDirection
    {
        Ascending = 0,
        Descending = 1
    }

    public enum SortTarget
    {
        Name = 0,
        Type = 1,
        ModifiedTime = 2,
        Size = 4,
        OriginPath = 8,
        Path = 16
    }

    public enum CompressionSortTarget
    {
        Name = 0,
        Type = 1,
        ModifiedTime = 2,
        Size = 4,
        CompressedSize = 8,
        CompressionRate = 16
    }

    public enum CompressionType
    {
        Zip = 0,
        Tar = 1,
        Gzip = 2,
        BZip2 = 4
    }

    /// <summary>
    /// 压缩等级枚举
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// 未指定
        /// </summary>
        Undefine = -1,

        /// <summary>
        /// 最大
        /// </summary>
        Max = 9,

        /// <summary>
        /// 标准
        /// </summary>
        Standard = 4,

        /// <summary>
        /// 仅打包
        /// </summary>
        PackageOnly = 0
    }

    /// <summary>
    /// 图片滤镜类型
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// 原图
        /// </summary>
        Origin = 0,
        /// <summary>
        /// 反色滤镜
        /// </summary>
        Invert = 1,
        /// <summary>
        /// 灰度滤镜
        /// </summary>
        Gray = 2,
        /// <summary>
        /// 二值化滤镜
        /// </summary>
        Threshold = 4,
        /// <summary>
        /// 素描滤镜
        /// </summary>
        Sketch = 8,
        /// <summary>
        /// 高斯模糊滤镜
        /// </summary>
        GaussianBlur = 16,
        /// <summary>
        /// 怀旧滤镜
        /// </summary>
        Sepia = 32,
        /// <summary>
        /// 马赛克滤镜
        /// </summary>
        Mosaic = 64,
        /// <summary>
        /// 油画滤镜
        /// </summary>
        OilPainting = 128
    }

    /// <summary>
    /// 提供对快速启动项状态或类型的枚举
    /// </summary>
    public enum QuickStartType
    {
        /// <summary>
        /// 应用区域的快速启动项
        /// </summary>
        Application = 1,
        /// <summary>
        /// 网站区域的快速启动项
        /// </summary>
        WebSite = 2,

        AddButton = 4
    }

    /// <summary>
    /// Windows Hello授权状态
    /// </summary>
    public enum AuthenticatorState
    {
        /// <summary>
        /// 注册成功
        /// </summary>
        RegisterSuccess = 0,
        /// <summary>
        /// 用户取消
        /// </summary>
        UserCanceled = 1,
        /// <summary>
        /// 凭据丢失
        /// </summary>
        CredentialNotFound = 2,
        /// <summary>
        /// 未知错误
        /// </summary>
        UnknownError = 4,
        /// <summary>
        /// 系统不支持Windows Hello
        /// </summary>
        WindowsHelloUnsupport = 8,
        /// <summary>
        /// 授权通过
        /// </summary>
        VerifyPassed = 16,
        /// <summary>
        /// 授权失败
        /// </summary>
        VerifyFailed = 32,
        /// <summary>
        /// 用户未注册
        /// </summary>
        UserNotRegistered = 64
    }

    /// <summary>
    /// 语言枚举
    /// </summary>
    public enum LanguageEnum
    {
        /// <summary>
        /// 界面使用简体中文
        /// </summary>
        Chinese_Simplified = 1,
        /// <summary>
        /// 界面使用英文
        /// </summary>
        English = 2,
        /// <summary>
        /// 界面使用法语
        /// </summary>
        French = 4,
        /// <summary>
        /// 界面使用繁体中文
        /// </summary>
        Chinese_Traditional = 8,

        Spanish = 16,

        German = 32
    }

    /// <summary>
    /// 背景图片类型的枚举
    /// </summary>
    public enum BackgroundBrushType
    {
        /// <summary>
        /// 使用亚克力背景
        /// </summary>
        DefaultAcrylic = 0,

        /// <summary>
        /// 使用亚克力背景
        /// </summary>
        CustomAcrylic = 1,

        /// <summary>
        /// 使用图片背景
        /// </summary>
        Picture = 2,

        /// <summary>
        /// 使用纯色背景
        /// </summary>
        SolidColor = 4,

        /// <summary>
        /// 使用Bing图片作为背景
        /// </summary>
        BingPicture = 8,

        Mica = 16
    }

    /// <summary>
    /// 指定文件夹和库是自带还是用户固定
    /// </summary>
    public enum LibraryType
    {
        Downloads = 0,
        Desktop = 1,
        Videos = 2,
        Pictures = 4,
        Document = 8,
        Music = 16,
        OneDrive = 32,
        UserCustom = 64
    }

    [Flags]
    public enum BasicFilters
    {
        File = 1,
        Folder = 2
    }
}
