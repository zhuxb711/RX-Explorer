using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public static class USBController
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct STORAGE_DEVICE_NUMBER
        {
            public int DeviceType;
            public int DeviceNumber;
            public int PartitionNumber;
        };

        private enum DriveType : uint
        {
            DRIVE_UNKNOWN = 0,
            DRIVE_NO_ROOT_DIR = 1,
            DRIVE_REMOVABLE = 2,
            DRIVE_FIXED = 3,
            DRIVE_REMOTE = 4,
            DRIVE_CDROM = 5,
            DRIVE_RAMDISK = 6
        }

        private const string GUID_DEVINTERFACE_VOLUME = "53f5630d-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_DISK = "53f56307-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_FLOPPY = "53f56311-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_CDROM = "53f56308-b6bf-11d0-94f2-00a0c91efb8b";

        private const int INVALID_HANDLE_VALUE = -1;
        private const int GENERIC_READ = unchecked((int)0x80000000);
        private const int GENERIC_WRITE = unchecked((int)0x40000000);
        private const int FILE_SHARE_READ = unchecked((int)0x00000001);
        private const int FILE_SHARE_WRITE = unchecked((int)0x00000002);
        private const int OPEN_EXISTING = unchecked((int)3);
        private const int FSCTL_LOCK_VOLUME = unchecked((int)0x00090018);
        private const int FSCTL_DISMOUNT_VOLUME = unchecked((int)0x00090020);
        private const int IOCTL_STORAGE_EJECT_MEDIA = unchecked((int)0x002D4808);
        private const int IOCTL_STORAGE_MEDIA_REMOVAL = unchecked((int)0x002D4804);
        private const int IOCTL_STORAGE_GET_DEVICE_NUMBER = unchecked((int)0x002D1080);

        private const int ERROR_NO_MORE_ITEMS = 259;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_INVALID_DATA = 13;

        [DllImport("kernel32.dll")]
        private static extern DriveType GetDriveType([MarshalAs(UnmanagedType.LPStr)] string lpRootPathName);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr lpSecurityAttributes,
            int dwCreationDisposition,
            int dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            int dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out int lpBytesReturned,
            IntPtr lpOverlapped);

        // from setupapi.h
        private const int DIGCF_PRESENT = (0x00000002);
        private const int DIGCF_DEVICEINTERFACE = (0x00000010);

        [StructLayout(LayoutKind.Sequential)]
        private class SP_DEVINFO_DATA
        {
            public int cbSize = Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
            public Guid classGuid = Guid.Empty; // temp
            public int devInst = 0; // dumy
            public int reserved = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public int cbSize;
            public short devicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DATA));
            public Guid interfaceClassGuid = Guid.Empty; // temp
            public int flags = 0;
            public int reserved = 0;
        }

        [DllImport("setupapi.dll")]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            int enumerator,
            IntPtr hwndParent,
            int flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            SP_DEVINFO_DATA deviceInfoData,
            ref Guid interfaceClassGuid,
            int memberIndex,
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            int deviceInterfaceDetailDataSize,
            ref int requiredSize,
            SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll")]
        private static extern uint SetupDiDestroyDeviceInfoList(
            IntPtr deviceInfoSet);

        [DllImport("setupapi.dll")]
        private static extern int CM_Get_Parent(
            ref int pdnDevInst,
            int dnDevInst,
            int ulFlags);

        [DllImport("setupapi.dll")]
        private static extern int CM_Request_Device_Eject(
            int dnDevInst,
            out PNP_VETO_TYPE pVetoType,
            StringBuilder pszVetoName,
            int ulNameLength,
            int ulFlags);

        [DllImport("setupapi.dll", EntryPoint = "CM_Request_Device_Eject")]
        private static extern int CM_Request_Device_Eject_NoUi(
            int dnDevInst,
            IntPtr pVetoType,
            StringBuilder pszVetoName,
            int ulNameLength,
            int ulFlags);

        private enum PNP_VETO_TYPE
        {
            Ok,
            TypeUnknown,
            LegacyDevice,
            PendingClose,
            WindowsApp,
            WindowsService,
            OutstandingOpen,
            Device,
            Driver,
            IllegalDeviceRequest,
            InsufficientPower,
            NonDisableable,
            LegacyDriver,
            InsufficientRights
        }

        private static long GetDeviceNumber(IntPtr handle)
        {
            IntPtr buffer = Marshal.AllocHGlobal(0x400);

            try
            {
                DeviceIoControl(handle, IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, buffer, 0x400, out int bytesReturned, IntPtr.Zero);

                if (bytesReturned > 0)
                {
                    return ((STORAGE_DEVICE_NUMBER)Marshal.PtrToStructure(buffer, typeof(STORAGE_DEVICE_NUMBER))).DeviceNumber;
                }
                else
                {
                    return -1;
                }
            }
            catch
            {
                return -1;
            }
            finally
            {
                CloseHandle(handle);
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static long GetDrivesDevInstByDeviceNumber(long DeviceNumber, DriveType DriveType)
        {
            Guid Type_Guid;

            switch (DriveType)
            {
                case DriveType.DRIVE_REMOVABLE:
                    Type_Guid = new Guid(GUID_DEVINTERFACE_DISK);
                    break;
                case DriveType.DRIVE_FIXED:
                    Type_Guid = new Guid(GUID_DEVINTERFACE_DISK);
                    break;
                case DriveType.DRIVE_CDROM:
                    Type_Guid = new Guid(GUID_DEVINTERFACE_CDROM);
                    break;
                default:
                    return 0;
            }

            IntPtr hDevInfo = SetupDiGetClassDevs(ref Type_Guid, 0, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (hDevInfo.ToInt32() == INVALID_HANDLE_VALUE)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                for (int i = 0; ; i++)
                {
                    SP_DEVICE_INTERFACE_DATA Interface_Data = new SP_DEVICE_INTERFACE_DATA();
                    if (!SetupDiEnumDeviceInterfaces(hDevInfo, null, ref Type_Guid, i, Interface_Data))
                    {
                        int Error = Marshal.GetLastWin32Error();
                        if (Error != ERROR_NO_MORE_ITEMS)
                        {
                            throw new Win32Exception(Error);
                        }
                        break;
                    }

                    SP_DEVINFO_DATA devData = new SP_DEVINFO_DATA();

                    int Size = 0;
                    if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, Interface_Data, IntPtr.Zero, 0, ref Size, devData))
                    {
                        int Error = Marshal.GetLastWin32Error();
                        if (Error != ERROR_INSUFFICIENT_BUFFER)
                        {
                            throw new Win32Exception(Error);
                        }
                    }

                    IntPtr Buffer = Marshal.AllocHGlobal(Size);

                    try
                    {
                        SP_DEVICE_INTERFACE_DETAIL_DATA Interface_Detail_Data = new SP_DEVICE_INTERFACE_DETAIL_DATA
                        {
                            cbSize = Marshal.SizeOf(typeof(SP_DEVICE_INTERFACE_DETAIL_DATA))
                        };
                        Marshal.StructureToPtr(Interface_Detail_Data, Buffer, false);

                        if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, Interface_Data, Buffer, Size, ref Size, devData))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        string DevicePath = Marshal.PtrToStringAuto((IntPtr)((int)Buffer + Marshal.SizeOf(typeof(int))));

                        IntPtr hDrive = CreateFile(DevicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                        if (hDrive.ToInt32() != INVALID_HANDLE_VALUE && DeviceNumber == GetDeviceNumber(hDrive))
                        {
                            return devData.devInst;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(Buffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            return 0;
        }

        public static bool EjectDevice(string Path)
        {
            if (string.IsNullOrEmpty(Path) || string.IsNullOrEmpty(System.IO.Path.GetPathRoot(Path)))
            {
                throw new ArgumentNullException(nameof(Path), "Argument is not legal");
            }

            try
            {
                IntPtr hVolume = CreateFile($@"\\.\{Path.Substring(0, 2)}", 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hVolume.ToInt32() == -1)
                {
                    return false;
                }

                long DeviceNumber = GetDeviceNumber(hVolume);
                if (DeviceNumber == -1)
                {
                    return false;
                }

                long DevInst = GetDrivesDevInstByDeviceNumber(DeviceNumber, GetDriveType(Path));
                if (DevInst == 0)
                {
                    return false;
                }

                int DevInstParent = 0;
                CM_Get_Parent(ref DevInstParent, (int)DevInst, 0);

                for (int i = 0; i < 3; i++)
                {
                    if (CM_Request_Device_Eject_NoUi(DevInstParent, IntPtr.Zero, null, 0, 0) == 0)
                    {
                        return true;
                    }

                    Thread.Sleep(500);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
