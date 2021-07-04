using System;
using System.Collections.Generic;
using System.Text;

namespace ShareClassLibrary
{
    public sealed class PipeCommand
    {
        public string CommandText { get; set; }

        public Dictionary<string, string> ExtraData { get; set; }
    }
}
