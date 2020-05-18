using FileManager.Class;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using SystemInformationProvider;
using Windows.ApplicationModel;
using Windows.Graphics.Display;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager.Dialog
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
                return GlobalizationPreferences.Languages[0];
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
                    return $"{Globalization.GetString("SystemInfo_Dialog_Memory_Total_Text")} " + MemoryGroup[0] + " (" + MemoryGroup[1] + $" {Globalization.GetString("SystemInfo_Dialog_Memory_Available_Text")})";
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
                return $"{Globalization.GetString("SystemInfo_Dialog_Memory_Usage_Text")}: " + (MemoryManager.AppMemoryUsage / 1048576f < 1024 ? Math.Round(MemoryManager.AppMemoryUsage / 1048576f, 2).ToString("0.00") + " MB"
                                                                                               : Math.Round(MemoryManager.AppMemoryUsage / 1073741824f, 2).ToString("0.00") + " GB");
            }
        }

        public string Resolution { get; private set; }

        public string ScreenSize { get; private set; }

        public string ResolutionScale { get; private set; }

        public string DisplayDpi { get; private set; }

        public string CurrentColorMode { get; private set; }

        public SystemInfoDialog()
        {
            InitializeComponent();

            DisplayInformation CurrentDisplay = DisplayInformation.GetForCurrentView();
            Resolution = $"{CurrentDisplay.ScreenWidthInRawPixels} × {CurrentDisplay.ScreenHeightInRawPixels}";
            ScreenSize = CurrentDisplay.DiagonalSizeInInches == null ? "Unknown" : $"{CurrentDisplay.DiagonalSizeInInches.GetValueOrDefault().ToString("F1")} inch";
            ResolutionScale = $"{Convert.ToInt16(CurrentDisplay.RawPixelsPerViewPixel * 100)}%";
            DisplayDpi = $"{Convert.ToInt16(CurrentDisplay.RawDpiX)} DPI";

            AdvancedColorInfo ColorInfo = CurrentDisplay.GetAdvancedColorInfo();
            switch (ColorInfo.CurrentAdvancedColorKind)
            {
                case AdvancedColorKind.HighDynamicRange:
                    {
                        CurrentColorMode = "HDR";
                        break;
                    }
                case AdvancedColorKind.StandardDynamicRange:
                    {
                        CurrentColorMode = "SDR";
                        break;
                    }
                case AdvancedColorKind.WideColorGamut:
                    {
                        CurrentColorMode = "WCG";
                        break;
                    }
            }

            if (ColorInfo.MaxLuminanceInNits != 0)
            {
                DisplayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

                TextBlock PeakLuminance = new TextBlock
                {
                    Text = Globalization.GetString("SystemInfo_Dialog_Monitor_PeakBrightness_Text"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                PeakLuminance.SetValue(Grid.RowProperty, DisplayGrid.RowDefinitions.Count - 1);
                PeakLuminance.SetValue(Grid.ColumnProperty, 0);

                TextBlock PeakLuminanceDescription = new TextBlock
                {
                    Text = $"{ColorInfo.MaxLuminanceInNits:F1} Nits",
                    VerticalAlignment = VerticalAlignment.Center
                };
                PeakLuminanceDescription.SetValue(Grid.RowProperty, DisplayGrid.RowDefinitions.Count - 1);
                PeakLuminanceDescription.SetValue(Grid.ColumnProperty, 1);

                DisplayGrid.Children.Add(PeakLuminance);
                DisplayGrid.Children.Add(PeakLuminanceDescription);
            }

            if (ColorInfo.MaxAverageFullFrameLuminanceInNits != 0)
            {
                DisplayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

                TextBlock MaxAverageLuminance = new TextBlock
                {
                    Text = Globalization.GetString("SystemInfo_Dialog_Monitor_MaxBrightness_Text"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                MaxAverageLuminance.SetValue(Grid.RowProperty, DisplayGrid.RowDefinitions.Count - 1);
                MaxAverageLuminance.SetValue(Grid.ColumnProperty, 0);

                TextBlock MaxAverageLuminanceDescription = new TextBlock
                {
                    Text = $"{ColorInfo.MaxAverageFullFrameLuminanceInNits:F1} Nits",
                    VerticalAlignment = VerticalAlignment.Center
                };
                MaxAverageLuminanceDescription.SetValue(Grid.RowProperty, DisplayGrid.RowDefinitions.Count - 1);
                MaxAverageLuminanceDescription.SetValue(Grid.ColumnProperty, 1);

                DisplayGrid.Children.Add(MaxAverageLuminance);
                DisplayGrid.Children.Add(MaxAverageLuminanceDescription);
            }

            string CoreInfo = SystemInformation.CPUCoreInfo;
            if (!string.IsNullOrEmpty(CoreInfo))
            {
                string[] CoreInfoGroup = CoreInfo.Split("||");
                CPUCoreCount = $"{CoreInfoGroup[0]} {Globalization.GetString("SystemInfo_Dialog_CPU_PhysicalCore_Text")} , {CoreInfoGroup[1]} {Globalization.GetString("SystemInfo_Dialog_CPU_LogicalCore_Text")}";
                
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
                CPUCache = $"{Globalization.GetString("SystemInfo_Dialog_CPU_L1Cache_Text")}: {L1SizeDescription}   {Globalization.GetString("SystemInfo_Dialog_CPU_L2Cache_Text")}: {L2SizeDescription}   {Globalization.GetString("SystemInfo_Dialog_CPU_L3Cache_Text")}: {L3SizeDescription}";
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
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    }
                    else
                    {
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                        GPUGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    }

                    TextBlock GPUNameDescriptionBlock = new TextBlock
                    {
                        Text = Globalization.GetString("SystemInfo_Dialog_GPU_Model_Text"),
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
                        Text = Globalization.GetString("SystemInfo_Dialog_GPU_Memory_Text"),
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

            var Interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(Inter => Inter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || Inter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

            for (int i = 0; i < Interfaces.Count(); i++)
            {
                var Interface = Interfaces.ElementAt(i);

                var IPProperties = Interface.GetIPProperties();
                var PhysicalAddress = Interface.GetPhysicalAddress();

                if (NetworkGrid.RowDefinitions.Count == 0)
                {
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                }
                else
                {
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                }

                TextBlock AdapterDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Globalization.GetString("SystemInfo_Dialog_NetworkAdapter_Text")
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
                    Text = Globalization.GetString("SystemInfo_Dialog_Gateway_Text")
                };
                GatewayDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 3);
                GatewayDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(GatewayDescriptionBlock);

                var Gateway = IPProperties.GatewayAddresses.FirstOrDefault((Address) => Address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                              ?? IPProperties.GatewayAddresses.FirstOrDefault((Address) => Address.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
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
                    Text = Globalization.GetString("SystemInfo_Dialog_Primary_DNS_Text")
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
                    Text = Globalization.GetString("SystemInfo_Dialog_Secondary_DNS_Text")
                };
                SecondaryDNSDescriptionBlock.SetValue(Grid.RowProperty, i * 8 + 6);
                SecondaryDNSDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(SecondaryDNSDescriptionBlock);

                var SDNS = IPProperties.DnsAddresses.Skip(1);
                TextBlock SecondaryDNSAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = !SDNS.Any() ? "Unknown" : SDNS.FirstOrDefault().ToString()
                };
                SecondaryDNSAddressBlock.SetValue(Grid.RowProperty, i * 8 + 6);
                SecondaryDNSAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(SecondaryDNSAddressBlock);
            }
        }
    }
}
