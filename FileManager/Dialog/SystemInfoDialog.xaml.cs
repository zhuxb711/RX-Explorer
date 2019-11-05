using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using SystemInformationProvider;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class SystemInfoDialog : QueueContentDialog
    {
        public string WindowsVersion { get; private set; }

        public string SystemManufacturer { get; private set; }

        public string DeviceName { get; private set; }

        public string DeviceModel { get; private set; }

        public string SystemLanguage
        {
            get
            {
                return GlobalizationPreferences.Languages.FirstOrDefault();
            }
        }

        public string CPUName
        {
            get => SystemInformation.CPUName;
        }

        public string CPUArchitecture { get; private set; }

        public string CPUCoreCount { get; private set; }

        public string CPUCache { get; private set; }

        public string CPUFeature
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (SystemInformation.MMX)
                {
                    _ = sb.Append("MMX");
                }

                if (SystemInformation.SSE)
                {
                    _ = sb.Append("、SSE");
                }

                if (SystemInformation.SSE2)
                {
                    _ = sb.Append("、SSE2");
                }

                if (SystemInformation.SSE3)
                {
                    _ = sb.Append("、SSE3");
                }

                if (SystemInformation.SSSE3)
                {
                    _ = sb.Append("、SSSE3");
                }

                if (SystemInformation.SSE41)
                {
                    _ = sb.Append("、SSE4.1");
                }

                if (SystemInformation.SSE42)
                {
                    _ = sb.Append("、SSE4.2");
                }

                if (SystemInformation.AVX)
                {
                    _ = sb.Append("、AVX");
                }

                if (SystemInformation.AVX2)
                {
                    _ = sb.Append("、AVX2");
                }

                if (SystemInformation.AVX512)
                {
                    _ = sb.Append("、AVX512");
                }

                if (SystemInformation.AES)
                {
                    _ = sb.Append("、AES-NI");
                }

                if (SystemInformation.FMA)
                {
                    _ = sb.Append("、FMA3");
                }

                if (SystemInformation.SEP)
                {
                    _ = sb.Append("、SEP");
                }

                if (SystemInformation.SHA)
                {
                    _ = sb.Append("、SHA");
                }

                return sb.ToString();
            }
        }

        public string MemoryInfo
        {
            get
            {
                if (!string.IsNullOrEmpty(SystemInformation.MemoryInfo))
                {
                    var MemoryGroup = SystemInformation.MemoryInfo.Split("||");
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        return "共 " + MemoryGroup[0] + " (" + MemoryGroup[1] + " 可用)";
                    }
                    else
                    {
                        return "Total " + MemoryGroup[0] + " (" + MemoryGroup[1] + " available)";
                    }
                }
                else
                {
                    return "Unknown";
                }
            }
        }

        public string CurrentMemoryUsage
        {
            get
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    return "此软件内存占用: " + (MemoryManager.AppMemoryUsage / 1048576f < 1024 ? Math.Round(MemoryManager.AppMemoryUsage / 1048576f, 2).ToString("0.00") + " MB"
                                                                                                : Math.Round(MemoryManager.AppMemoryUsage / 1073741824f, 2).ToString("0.00") + " GB");
                }
                else
                {
                    return "This software memory usage: " + (MemoryManager.AppMemoryUsage / 1048576f < 1024 ? Math.Round(MemoryManager.AppMemoryUsage / 1048576f, 2).ToString("0.00") + " MB"
                                                                                                : Math.Round(MemoryManager.AppMemoryUsage / 1073741824f, 2).ToString("0.00") + " GB");
                }
            }
        }

        public SystemInfoDialog()
        {
            InitializeComponent();
            string CoreInfo = SystemInformation.CPUCoreInfo;
            if (!string.IsNullOrEmpty(CoreInfo))
            {
                var CoreInfoGroup = CoreInfo.Split("||");
                CPUCoreCount = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? (CoreInfoGroup[0] + "个物理核心  ,  " + CoreInfoGroup[1] + "个逻辑核心")
                    : (CoreInfoGroup[0] + " physical core  ,  " + CoreInfoGroup[1] + " logical core");
                float L1Size = Convert.ToSingle(CoreInfoGroup[2]);
                float L2Size = Convert.ToSingle(CoreInfoGroup[3]);
                float L3Size = Convert.ToSingle(CoreInfoGroup[4]);
                string L1SizeDescription = L1Size / 1024f < 1024 ? Convert.ToUInt16(Math.Round(L1Size / 1024f, 2)) + " KB"
                                                                 : Convert.ToUInt16(Math.Round(L1Size / 1048576f, 2)) + " MB";
                string L2SizeDescription = L2Size / 1024f < 1024 ? Convert.ToUInt16(Math.Round(L2Size / 1024f, 2)) + " KB"
                                                                 : Convert.ToUInt16(Math.Round(L2Size / 1048576f, 2)) + " MB";
                string L3SizeDescription = L3Size / 1024f < 1024 ? Convert.ToUInt16(Math.Round(L3Size / 1024f, 2)) + " KB"
                                                                 : Convert.ToUInt16(Math.Round(L3Size / 1048576f, 2)) + " MB";
                CPUArchitecture = (Package.Current.Id.Architecture == ProcessorArchitecture.X86
                                    ? "X86"
                                    : "X64");
                CPUCache = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? ("L1缓存: " + L1SizeDescription + "   L2缓存: " + L2SizeDescription + "   L3缓存: " + L3SizeDescription)
                    : ("L1 cache: " + L1SizeDescription + "   L2 cache: " + L2SizeDescription + "   L3 cache: " + L3SizeDescription);
            }

            EasClientDeviceInformation EAS = new EasClientDeviceInformation();
            SystemManufacturer = EAS.SystemManufacturer;

            ulong Version = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
            WindowsVersion = "Windows 10  " + $"{Version >> 48 & 0xFFFF}.{Version >> 32 & 0xFFFF}.{Version >> 16 & 0xFFFF}.{Version & 0xFFFF}";

            DeviceName = EAS.FriendlyName;
            DeviceModel = string.IsNullOrEmpty(EAS.SystemProductName) ? "Unknown" : EAS.SystemProductName;

            for (int i = 0; i < SystemInformation.GraphicAdapterInfo.Length; i++)
            {
                string GPUInfoGroup = SystemInformation.GraphicAdapterInfo[i];
                string[] GPUInfo = GPUInfoGroup.Split("||");
                long GPUMemory = Convert.ToInt64(GPUInfo[1]);
                if (GPUMemory != 0)
                {
                    string GPUName = GPUInfo[0];
                    string GPUVideoMemory = GPUMemory / 1024f < 1024 ? Math.Round(GPUMemory / 1024f, 2).ToString("0.00") + " KB"
                                                              : (GPUMemory / 1048576f < 1024 ? Math.Round(GPUMemory / 1048576f, 2).ToString("0.00") + " MB"
                                                              : Math.Round(GPUMemory / 1073741824f, 2).ToString("0.00") + " GB");
                    if (GPUGrid.RowDefinitions.Count == 0)
                    {
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    }
                    else
                    {
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    }

                    TextBlock GPUNameDescriptionBlock = new TextBlock
                    {
                        Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "GPU型号" : "GPU model",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    GPUNameDescriptionBlock.SetValue(Grid.RowProperty, i * 3);
                    GPUNameDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                    GPUGrid.Children.Add(GPUNameDescriptionBlock);

                    TextBlock GPUNameBlock = new TextBlock
                    {
                        Text = GPUName,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    GPUNameBlock.SetValue(Grid.RowProperty, i * 3);
                    GPUNameBlock.SetValue(Grid.ColumnProperty, 1);
                    GPUGrid.Children.Add(GPUNameBlock);

                    TextBlock GPUMemoryDescriptionBlock = new TextBlock
                    {
                        Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "GPU内存" : "GPU memory",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    GPUMemoryDescriptionBlock.SetValue(Grid.RowProperty, i * 3 + 1);
                    GPUMemoryDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                    GPUGrid.Children.Add(GPUMemoryDescriptionBlock);

                    TextBlock GPUMemoryBlock = new TextBlock
                    {
                        Text = GPUVideoMemory,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    GPUMemoryBlock.SetValue(Grid.RowProperty, i * 3 + 1);
                    GPUMemoryBlock.SetValue(Grid.ColumnProperty, 1);
                    GPUGrid.Children.Add(GPUMemoryBlock);
                }
            }

            var Interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(Inter => (Inter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || Inter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                                                                       && Inter.OperationalStatus == OperationalStatus.Up);
            for (int i = 0; i < Interfaces.Count(); i++)
            {
                var Interface = Interfaces.ElementAt(i);

                var IPProperties = Interface.GetIPProperties();
                var PhysicalAddress = Interface.GetPhysicalAddress();

                if (NetworkGrid.RowDefinitions.Count == 0)
                {
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                }
                else
                {
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
                }

                TextBlock AdapterDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "网络适配器" : "Network Adapter"
                };
                AdapterDescriptionBlock.SetValue(Grid.RowProperty, i * 8);
                AdapterDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(AdapterDescriptionBlock);

                TextBlock AdapterBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = string.IsNullOrEmpty(Interface.Description) ? "Unknown" : Interface.Description
                };
                AdapterBlock.SetValue(Grid.RowProperty, i * 8);
                AdapterBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(AdapterBlock);

                TextBlock IPv4DescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "IPv4"
                };
                IPv4DescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 1);
                IPv4DescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(IPv4DescriptionBlock);

                var IPv4 = IPProperties.UnicastAddresses.FirstOrDefault((IP) => IP.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                TextBlock IPv4AddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPv4 == null ? "Unknown" : IPv4.Address.ToString()
                };
                IPv4AddressBlock.SetValue(Grid.RowProperty, i * 8 + 1);
                IPv4AddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(IPv4AddressBlock);

                TextBlock IPv6DescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "IPv6"
                };
                IPv6DescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 2);
                IPv6DescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(IPv6DescriptionBlock);

                var IPv6 = IPProperties.UnicastAddresses.FirstOrDefault((IP) => IP.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
                TextBlock IPv6AddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPv6 == null ? "Unknown" : IPv6.Address.ToString()
                };
                IPv6AddressBlock.SetValue(Grid.RowProperty, i * 8 + 2);
                IPv6AddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(IPv6AddressBlock);

                TextBlock GatewayDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "网关" : "Gateway"
                };
                GatewayDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 3);
                GatewayDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(GatewayDescriptionBlock);

                var Gateway = IPProperties.GatewayAddresses.FirstOrDefault();
                TextBlock GatewayAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Gateway == null ? "Unknown" : Gateway.Address.ToString()
                };
                GatewayAddressBlock.SetValue(Grid.RowProperty, i * 8 + 3);
                GatewayAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(GatewayAddressBlock);

                TextBlock MACDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "MAC"
                };
                MACDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 4);
                MACDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(MACDescriptionBlock);

                TextBlock MACAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = PhysicalAddress == null ? "Unknown" : string.Join(":", Enumerable.Range(0, 6).Select(j => PhysicalAddress.ToString().Substring(j * 2, 2)))
                };
                MACAddressBlock.SetValue(Grid.RowProperty, i * 8 + 4);
                MACAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(MACAddressBlock);

                TextBlock PrimaryDNSDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "主DNS服务器" : "Primary DNS Server"
                };
                PrimaryDNSDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 5);
                PrimaryDNSDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(PrimaryDNSDescriptionBlock);

                var PDNS = IPProperties.DnsAddresses.FirstOrDefault();
                TextBlock PrimaryDNSAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = PDNS == null ? "Unknown" : PDNS.ToString()
                };
                PrimaryDNSAddressBlock.SetValue(Grid.RowProperty, i * 8 + 5);
                PrimaryDNSAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(PrimaryDNSAddressBlock);

                TextBlock SecondaryDNSDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "副DNS服务器" : "Secondary DNS server"
                };
                SecondaryDNSDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 6);
                SecondaryDNSDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(SecondaryDNSDescriptionBlock);

                var SDNS = IPProperties.DnsAddresses.Skip(1);
                TextBlock SecondaryDNSAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = SDNS.Count() == 0 ? "Unknown" : SDNS.FirstOrDefault().ToString()
                };
                SecondaryDNSAddressBlock.SetValue(Grid.RowProperty, i * 8 + 6);
                SecondaryDNSAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(SecondaryDNSAddressBlock);
            }
        }
    }
}
