using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FileRemovedDeferredEventArgs : FileChangedDeferredEventArgs
    {
        public FileRemovedDeferredEventArgs(string Path) : base(Path)
        {

        }
    }
}
