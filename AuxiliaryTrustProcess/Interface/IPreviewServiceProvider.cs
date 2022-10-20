namespace AuxiliaryTrustProcess.Interface
{
    internal interface IPreviewServiceProvider
    {
        public bool CheckServiceAvailable();

        public bool CheckWindowVisible();

        public bool ToggleServiceWindow(string Path);

        public bool SwitchServiceWindow(string Path);

        public bool CloseServiceWindow();
    }
}
