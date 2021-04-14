using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IIndirectLaunchStorageItem
    {
        public Task LaunchAsync();
    }
}
