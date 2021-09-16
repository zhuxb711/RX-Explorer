namespace ShareClassLibrary
{
    public sealed class VariableDataPackage
    {
        public string Variable { get; }

        public string Path { get; }

        public VariableDataPackage(string Variable, string Path)
        {
            this.Variable = Variable;
            this.Path = Path;
        }
    }
}
