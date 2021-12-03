using RX_Explorer.Class;
using System;
using System.Runtime.InteropServices;

namespace RX_Explorer.Interface
{
    [ComImport]
    [Guid("5CA296B2-2C25-4D22-B785-B885C8201E6A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IStorageItemHandleAccess
    {
        uint Create(UWP_HANDLE_ACCESS_OPTIONS Access, UWP_HANDLE_SHARING_OPTIONS Sharing, UWP_HANDLE_OPTIONS Option, IntPtr OpLockHandler, out IntPtr Handle);
    };
}
