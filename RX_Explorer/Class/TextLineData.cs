using System;

namespace RX_Explorer.Class
{
    public sealed class TextLineData
    {
        public string LineBreak { get; }

        public string LineBreakDescription
        {
            get
            {
                return LineBreak switch
                {
                    "\r" => "CR",
                    "\r\n" => "CRLF",
                    "\n" => "LF",
                    _ => string.Empty
                };
            }
        }

        public string LineText { get; }

        public TextLineData(string LineBreak, string LineText)
        {
            this.LineBreak = LineBreak;
            this.LineText = LineText;
        }
    }
}
