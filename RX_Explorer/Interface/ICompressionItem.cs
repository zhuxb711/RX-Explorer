namespace RX_Explorer.Interface
{
    public interface ICompressionItem : IStorageItemBaseProperties
    {
        public long CompressedSize { get; }

        public float CompressionRate { get; }
    }
}
