using MediaDevices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class MTPDeviceManager : IDisposable
    {
        private readonly static object Locker = new object();
        private readonly List<MediaDevice> ConnectedDevices = new List<MediaDevice>();

        public MediaDevice GetDeviceFromId(string DeviceId)
        {
            lock (Locker)
            {
                if (ConnectedDevices.SingleOrDefault((Device) => Device.DeviceId.Equals(DeviceId, StringComparison.OrdinalIgnoreCase)) is MediaDevice ExistDevice)
                {
                    return MakeSureDeviceConnected(ExistDevice);
                }
                else if (MediaDevice.GetDevices().SingleOrDefault((Device) => Device.DeviceId.Equals(DeviceId, StringComparison.OrdinalIgnoreCase)) is MediaDevice NewDevice)
                {
                    if (MakeSureDeviceConnected(NewDevice) is MediaDevice ConnectedDevice)
                    {
                        ConnectedDevices.Add(ConnectedDevice);
                        return ConnectedDevice;
                    }
                }

                return null;
            }
        }

        private static MediaDevice MakeSureDeviceConnected(MediaDevice Device)
        {
            if (!Device.IsConnected)
            {
                for (int Retry = 0; Retry < 3; Retry++)
                {
                    try
                    {
                        Device.Connect();

                        if (Device.IsConnected)
                        {
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        //No need to handle this exception
                    }

                    Thread.Sleep(500);
                }

                if (!Device.IsConnected)
                {
                    return null;
                }
            }

            return Device;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ConnectedDevices.ForEach((Device) =>
            {
                try
                {
                    Device.Disconnect();
                    Device.Dispose();
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }
            });
            ConnectedDevices.Clear();
        }

        ~MTPDeviceManager()
        {
            Dispose();
        }
    }
}
