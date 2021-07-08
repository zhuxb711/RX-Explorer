using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FileAddedDeferredEventArgs : FileChangedDeferredEventArgs
    {
        public FileAddedDeferredEventArgs(string Path) : base(Path)
        {

        }
    }
}
