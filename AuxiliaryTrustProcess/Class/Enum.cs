namespace AuxiliaryTrustProcess.Class
{
    internal enum SystemLaunchHelperExitCodeEnum
    {
        Success = 0,
        FailedOnUnknownReason = -1,
        FailedOnRegistryCheck = 1,
        FailedOnParseArguments = 2,
        FailedOnLaunchExplorer = 3,
    }
}
