namespace SharedLibrary
{
    public sealed class VariableDataPackage
    {
        public string Path { get; }

        public string Variable { get; }

        public VariableDataPackage(string Path, string Variable)
        {
            this.Path = Path;
            this.Variable = Variable;
        }
    }
}
