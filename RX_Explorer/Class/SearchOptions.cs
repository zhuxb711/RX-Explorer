namespace RX_Explorer.Class
{
    public sealed class SearchOptions
    {
        public FileSystemStorageFolder SearchFolder { get; set; }

        public string SearchText { get; set; }

        public bool IgnoreCase { get; set; }

        public bool UseRegexExpression { get; set; }

        public bool DeepSearch { get; set; }

        public bool? UseAQSExpression { get; set; }

        public uint NumLimit { get; set; } = 300;

        public SearchCategory EngineCategory { get; set; }
    }
}
