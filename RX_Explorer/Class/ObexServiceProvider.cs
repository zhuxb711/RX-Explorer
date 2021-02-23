using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供蓝牙OBEX协议服务
    /// </summary>
    public sealed class ObexServiceProvider
    {
        /// <summary>
        /// 蓝牙设备
        /// </summary>
        public static BluetoothDevice BlueToothDevice { get; private set; }

        public static string DeviceName { get; private set; }

        /// <summary>
        /// OBEX协议服务
        /// </summary>
        public static ObexService GetObexInstance()
        {
            if (BlueToothDevice != null)
            {
                return ObexService.GetDefaultForBluetoothDevice(BlueToothDevice);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 设置Obex对象的实例
        /// </summary>
        /// <param name="obex">OBEX对象</param>
        public static void SetObexInstance(BluetoothDevice BT, string DeviceDisplayName)
        {
            BlueToothDevice = BT;
            DeviceName = DeviceDisplayName;
        }
    }
}
