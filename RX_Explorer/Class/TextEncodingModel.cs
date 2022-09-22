using System.Text;

namespace RX_Explorer.Class
{
    public sealed class TextEncodingModel
    {
        public string DisplayName { get; }

        public Encoding TextEncoding { get; }

        public TextEncodingModel(Encoding Encoding)
        {
            TextEncoding = Encoding;
            DisplayName = TextEncoding?.EncodingName ?? string.Empty;
        }
    }
}
