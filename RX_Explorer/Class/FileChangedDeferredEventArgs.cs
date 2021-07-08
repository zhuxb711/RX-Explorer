using Microsoft.Toolkit.Deferred;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class FileChangedDeferredEventArgs : DeferredEventArgs
    {
        public string Path { get; }

        public FileChangedDeferredEventArgs(string Path)
        {
            this.Path = Path;
        }
    }
}
