using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public static class USBController
    {
        private const string GUID_DEVINTERFACE_VOLUME = "53f5630d-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_DISK = "53f56307-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_FLOPPY = "53f56311-b6bf-11d0-94f2-00a0c91efb8b";
        private const string GUID_DEVINTERFACE_CDROM = "53f56308-b6bf-11d0-94f2-00a0c91efb8b";

        private static int GetDeviceNumber(HFILE DeviceHandle)
        {
            IntPtr Buffer = Marshal.AllocCoTaskMem(1024);

            try
            {
                if (Kernel32.DeviceIoControl(DeviceHandle, Kernel32.IOControlCode.IOCTL_STORAGE_GET_DEVICE_NUMBER, IntPtr.Zero, 0, Buffer, 1024, out uint bytesReturned))
                {
                    if (bytesReturned > 0)
                    {
                        return Convert.ToInt32(Marshal.PtrToStructure<Kernel32.STORAGE_DEVICE_NUMBER>(Buffer).DeviceNumber);
                    }
                    else
                    {
                        return -1;
                    }
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
                Marshal.FreeCoTaskMem(Buffer);
            }
        }

        private static uint GetDrivesDevInstByDeviceNumber(long DeviceNumber, Kernel32.DRIVE_TYPE DriveType)
        {
            Guid Type_Guid;

            switch (DriveType)
            {
                case Kernel32.DRIVE_TYPE.DRIVE_REMOVABLE:
                case Kernel32.DRIVE_TYPE.DRIVE_FIXED:
                    {
                        Type_Guid = new Guid(GUID_DEVINTERFACE_DISK);
                        break;
                    }
                case Kernel32.DRIVE_TYPE.DRIVE_CDROM:
                    {
                        Type_Guid = new Guid(GUID_DEVINTERFACE_CDROM);
                        break;
                    }
                default:
                    {
                        throw new ArgumentException("Parameter is invalid", nameof(DriveType));
                    }
            }

            using (SetupAPI.SafeHDEVINFO hDevInfo = SetupAPI.SetupDiGetClassDevs(Type_Guid, Flags: SetupAPI.DIGCF.DIGCF_PRESENT | SetupAPI.DIGCF.DIGCF_DEVICEINTERFACE))
            {
                if (hDevInfo.IsInvalid || hDevInfo.IsNull)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                for (uint Index = 0; ; Index++)
                {
                    SetupAPI.SP_DEVICE_INTERFACE_DATA Interface_Data = new SetupAPI.SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(SetupAPI.SP_DEVICE_INTERFACE_DATA)))
                    };

                    if (!SetupAPI.SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, Type_Guid, Index, ref Interface_Data))
                    {
                        int Error = Marshal.GetLastWin32Error();

                        if (Error != Win32Error.ERROR_NO_MORE_ITEMS)
                        {
                            throw new Win32Exception(Error);
                        }

                        break;
                    }

                    SetupAPI.SP_DEVINFO_DATA devData = new SetupAPI.SP_DEVINFO_DATA
                    {
                        cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(SetupAPI.SP_DEVINFO_DATA)))
                    };

                    if (!SetupAPI.SetupDiGetDeviceInterfaceDetail(hDevInfo, Interface_Data, IntPtr.Zero, 0, out uint Size, ref devData))
                    {
                        int Error = Marshal.GetLastWin32Error();

                        if (Error != Win32Error.ERROR_INSUFFICIENT_BUFFER)
                        {
                            throw new Win32Exception(Error);
                        }
                    }

                    IntPtr Buffer = Marshal.AllocCoTaskMem(Convert.ToInt32(Size));

                    try
                    {
                        SetupAPI.SP_DEVICE_INTERFACE_DETAIL_DATA Interface_Detail_Data = new SetupAPI.SP_DEVICE_INTERFACE_DETAIL_DATA
                        {
                            cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(SetupAPI.SP_DEVICE_INTERFACE_DETAIL_DATA)))
                        };

                        Marshal.StructureToPtr(Interface_Detail_Data, Buffer, false);

                        if (!SetupAPI.SetupDiGetDeviceInterfaceDetail(hDevInfo, Interface_Data, Buffer, Size, out _, ref devData))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        string DevicePath = Marshal.PtrToStringAuto((IntPtr)(Buffer.ToInt64() + Marshal.SizeOf(typeof(int))));

                        using (Kernel32.SafeHFILE hDrive = Kernel32.CreateFile(DevicePath, 0, FileShare.ReadWrite, null, FileMode.Open, FileFlagsAndAttributes.SECURITY_ANONYMOUS))
                        {
                            if (!hDrive.IsInvalid && !hDrive.IsNull && DeviceNumber == GetDeviceNumber(hDrive))
                            {
                                return devData.DevInst;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(Buffer);
                    }
                }
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
                int DeviceNumber = 0;

                using (Kernel32.SafeHFILE hVolume = Kernel32.CreateFile($@"\\.\{Path.Substring(0, 2)}", 0, FileShare.ReadWrite, null, FileMode.Open, FileFlagsAndAttributes.SECURITY_ANONYMOUS))
                {
                    if (hVolume.IsNull || hVolume.IsInvalid)
                    {
                        return false;
                    }

                    DeviceNumber = GetDeviceNumber(hVolume);
                }

                if (DeviceNumber >= 0)
                {
                    uint DevInst = GetDrivesDevInstByDeviceNumber(DeviceNumber, Kernel32.GetDriveType(Path));

                    if (DevInst == 0)
                    {
                        return false;
                    }

                    if (CfgMgr32.CM_Get_Parent(out uint DevInstParent, DevInst) == CfgMgr32.CONFIGRET.CR_SUCCESS)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (CfgMgr32.CM_Request_Device_Eject(DevInstParent, out CfgMgr32.PNP_VETO_TYPE Result, null, 0) == CfgMgr32.CONFIGRET.CR_SUCCESS)
                            {
                                return true;
                            }
                            else
                            {
                                LogTracer.Log($"Could not reject the USB device, PNP_VETO reason: {Enum.GetName(typeof(CfgMgr32.PNP_VETO_TYPE), Result)}");
                            }

                            Thread.Sleep(300);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not reject the USB device, exception was threw, reason: {ex.Message}");
                return false;
            }
        }
    }
}
