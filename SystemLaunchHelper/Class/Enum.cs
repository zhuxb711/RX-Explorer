namespace SystemLaunchHelper.Class
{
    internal enum ExitCodeEnum
    {
        Success = 0,
        FailedOnUnknownReason = -1,
        FailedOnRegistryCheck = 1,
        FailedOnParseArguments = 2,
        FailedOnLaunchExplorer = 3,
    }
}
