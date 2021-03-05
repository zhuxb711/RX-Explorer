using System;
using System.Runtime.InteropServices;

namespace RX_Explorer.Interface
{
    [ComImport]
    [Guid("5CA296B2-2C25-4D22-B785-B885C8201E6A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IStorageItemHandleAccess
    {
        uint Create(uint access, uint sharing, uint option, IntPtr opLockHandler, out IntPtr handle);
    };
}
