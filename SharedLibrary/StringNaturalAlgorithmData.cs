namespace SharedLibrary
{
    public sealed class StringNaturalAlgorithmData
    {
        public string UniqueId { get; }

        public string Value { get; }

        public StringNaturalAlgorithmData(string UniqueId, string Value)
        {
            this.UniqueId = UniqueId;
            this.Value = Value;
        }
    }
}
