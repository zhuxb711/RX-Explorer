using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SystemInformationProvider;
using Windows.Graphics.Display;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class SystemInfoDialog : QueueContentDialog
    {
        public string WindowsVersion { get; }

        public string SystemManufacturer { get; }

        public string DeviceName { get; }

        public string DeviceModel { get; }

        public string SystemLanguage => GlobalizationPreferences.Languages[0];

        public string CPUName => SystemInformation.CPUName;

        public string CPUArchitecture => "X64";

        public string CPUCoreCount { get; }

        public string CPUCache { get; }

        public string CPUFeature
        {
            get
            {
                List<string> Features = new List<string>(14);

                if (SystemInformation.MMX)
                {
                    Features.Add("MMX");
                }

                if (SystemInformation.SSE)
                {
                    Features.Add("SSE");
                }

                if (SystemInformation.SSE2)
                {
                    Features.Add("SSE2");
                }

                if (SystemInformation.SSE3)
                {
                    Features.Add("SSE3");
                }

                if (SystemInformation.SSSE3)
                {
                    Features.Add("SSSE3");
                }

                if (SystemInformation.SSE41)
                {
                    Features.Add("SSE4.1");
                }

                if (SystemInformation.SSE42)
                {
                    Features.Add("SSE4.2");
                }

                if (SystemInformation.AVX)
                {
                    Features.Add("AVX");
                }

                if (SystemInformation.AVX2)
                {
                    Features.Add("AVX2");
                }

                if (SystemInformation.AVX512)
                {
                    Features.Add("AVX512");
                }

                if (SystemInformation.AES)
                {
                    Features.Add("AES-NI");
                }

                if (SystemInformation.FMA)
                {
                    Features.Add("FMA3");
                }

                if (SystemInformation.SEP)
                {
                    Features.Add("SEP");
                }

                if (SystemInformation.SHA)
                {
                    Features.Add("SHA");
                }

                return string.Join(", ", Features);
            }
        }

        public string MemoryInfo
        {
            get
            {
                if (!string.IsNullOrEmpty(SystemInformation.MemoryInfo))
                {
                    string[] MemoryGroup = SystemInformation.MemoryInfo.Split("||");
                    return $"{Globalization.GetString("SystemInfo_Dialog_Memory_Total_Text")} " + MemoryGroup[0] + " (" + MemoryGroup[1] + $" {Globalization.GetString("SystemInfo_Dialog_Memory_Available_Text")})";
                }
                else
                {
                    return Globalization.GetString("UnknownText");
                }
            }
        }

        public string CurrentMemoryUsage => $"{Globalization.GetString("SystemInfo_Dialog_Memory_Usage_Text")}: " + MemoryManager.AppMemoryUsage.GetFileSizeDescription();

        public string Resolution { get; }

        public string ScreenSize { get; }

        public string ResolutionScale { get; }

        public string DisplayDpi { get; }

        public string CurrentColorMode { get; }

        public SystemInfoDialog()
        {
            InitializeComponent();

            DisplayInformation CurrentDisplay = DisplayInformation.GetForCurrentView();
            Resolution = $"{CurrentDisplay.ScreenWidthInRawPixels} × {CurrentDisplay.ScreenHeightInRawPixels}";
            ScreenSize = $"{CurrentDisplay.DiagonalSizeInInches?.ToString("F1") ?? Globalization.GetString("UnknownText")} inch";
            ResolutionScale = $"{Convert.ToInt16(CurrentDisplay.RawPixelsPerViewPixel * 100)}%";
            DisplayDpi = $"{Convert.ToInt16(CurrentDisplay.RawDpiX)} DPI";

            AdvancedColorInfo ColorInfo = CurrentDisplay.GetAdvancedColorInfo();
            CurrentColorMode = ColorInfo.CurrentAdvancedColorKind switch
            {
                AdvancedColorKind.HighDynamicRange => "HDR",
                AdvancedColorKind.StandardDynamicRange => "SDR",
                AdvancedColorKind.WideColorGamut => "WCG",
                _ => throw new NotSupportedException()
            };

            if (ColorInfo.MaxLuminanceInNits > 0)
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

            if (ColorInfo.MaxAverageFullFrameLuminanceInNits > 0)
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

            string[] CoreInfoGroup = SystemInformation.CPUCoreInfo.Split("||", StringSplitOptions.RemoveEmptyEntries);

            if (CoreInfoGroup.Length == 5)
            {
                CPUCoreCount = $"{CoreInfoGroup[0]} {Globalization.GetString("SystemInfo_Dialog_CPU_PhysicalCore_Text")} , {CoreInfoGroup[1]} {Globalization.GetString("SystemInfo_Dialog_CPU_LogicalCore_Text")}";
                CPUCache = $"{Globalization.GetString("SystemInfo_Dialog_CPU_L1Cache_Text")}: {Convert.ToInt64(CoreInfoGroup[2]).GetFileSizeDescription()}   {Globalization.GetString("SystemInfo_Dialog_CPU_L2Cache_Text")}: {Convert.ToInt64(CoreInfoGroup[3]).GetFileSizeDescription()}   {Globalization.GetString("SystemInfo_Dialog_CPU_L3Cache_Text")}: {Convert.ToInt64(CoreInfoGroup[4]).GetFileSizeDescription()}";
            }

            EasClientDeviceInformation EAS = new EasClientDeviceInformation();

            ulong Version = ulong.Parse(AnalyticsInfo.VersionInfo.DeviceFamilyVersion);
            WindowsVersion = $"Windows {(((Version >> 16) & 0xFFFF) >= 22000 ? "11" : "10")} {(Version >> 48) & 0xFFFF}.{(Version >> 32) & 0xFFFF}.{(Version >> 16) & 0xFFFF}.{Version & 0xFFFF}";

            SystemManufacturer = EAS.SystemManufacturer;
            DeviceName = EAS.FriendlyName;
            DeviceModel = string.IsNullOrEmpty(EAS.SystemProductName) ? Globalization.GetString("UnknownText") : EAS.SystemProductName;

            for (int i = 0; i < SystemInformation.GraphicAdapterInfo.Length; i++)
            {
                string[] GPUInfo = SystemInformation.GraphicAdapterInfo[i].Split("||");

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
                    Text = GPUInfo[0],
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
                    Text = Convert.ToInt64(GPUInfo[1]).GetFileSizeDescription(),
                    VerticalAlignment = VerticalAlignment.Center
                };
                GPUMemoryBlock.SetValue(Grid.RowProperty, i * 3 + 1);
                GPUMemoryBlock.SetValue(Grid.ColumnProperty, 1);
                GPUGrid.Children.Add(GPUMemoryBlock);
            }

            IReadOnlyList<NetworkInterface> Interfaces = NetworkInterface.GetAllNetworkInterfaces()
                                                                         .Where((Network) => Network.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                                                                         .ToArray();

            for (int Index = 0; Index < Interfaces.Count; Index++)
            {
                NetworkInterface Interface = Interfaces[Index];

                IPInterfaceProperties IPProperties = Interface.GetIPProperties();
                UnicastIPAddressInformation IPv4 = IPProperties.UnicastAddresses.FirstOrDefault((IP) => IP.Address.AddressFamily == AddressFamily.InterNetwork);
                UnicastIPAddressInformation IPv6 = IPProperties.UnicastAddresses.FirstOrDefault((IP) => IP.Address.AddressFamily == AddressFamily.InterNetworkV6);
                GatewayIPAddressInformation Gateway = IPProperties.GatewayAddresses.FirstOrDefault((Address) => Address.Address.AddressFamily == AddressFamily.InterNetwork)
                                                      ?? IPProperties.GatewayAddresses.FirstOrDefault((Address) => Address.Address.AddressFamily == AddressFamily.InterNetworkV6);

                if (NetworkGrid.RowDefinitions.Count == 0)
                {
                    for (int RowNumer = 0; RowNumer < 7; RowNumer++)
                    {
                        NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    }
                }
                else
                {
                    for (int RowNumer = 0; RowNumer < 8; RowNumer++)
                    {
                        NetworkGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
                    }
                }

                TextBlock AdapterDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Globalization.GetString("SystemInfo_Dialog_NetworkAdapter_Text")
                };
                AdapterDescriptionBlock.SetValue(Grid.RowProperty, Index * 8);
                AdapterDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(AdapterDescriptionBlock);

                TextBlock AdapterBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = string.IsNullOrEmpty(Interface.Description) ? Globalization.GetString("UnknownText") : Interface.Description
                };
                AdapterBlock.SetValue(Grid.RowProperty, Index * 8);
                AdapterBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(AdapterBlock);

                TextBlock IPv4DescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "IPv4"
                };
                IPv4DescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 1);
                IPv4DescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(IPv4DescriptionBlock);

                TextBlock IPv4AddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPv4?.Address.ToString() ?? Globalization.GetString("UnknownText")
                };
                IPv4AddressBlock.SetValue(Grid.RowProperty, Index * 8 + 1);
                IPv4AddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(IPv4AddressBlock);

                TextBlock IPv6DescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "IPv6"
                };
                IPv6DescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 2);
                IPv6DescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(IPv6DescriptionBlock);

                TextBlock IPv6AddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPv6?.Address.ToString() ?? Globalization.GetString("UnknownText")
                };
                IPv6AddressBlock.SetValue(Grid.RowProperty, Index * 8 + 2);
                IPv6AddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(IPv6AddressBlock);

                TextBlock GatewayDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Globalization.GetString("SystemInfo_Dialog_Gateway_Text")
                };
                GatewayDescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 3);
                GatewayDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(GatewayDescriptionBlock);

                TextBlock GatewayAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Gateway?.Address.ToString() ?? Globalization.GetString("UnknownText")
                };
                GatewayAddressBlock.SetValue(Grid.RowProperty, Index * 8 + 3);
                GatewayAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(GatewayAddressBlock);

                TextBlock MACDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = "MAC"
                };
                MACDescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 4);
                MACDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(MACDescriptionBlock);

                TextBlock MACAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = string.Join(":", Interface.GetPhysicalAddress()?.ToString().Split(2) ?? new string[] { Globalization.GetString("UnknownText") })
                };
                MACAddressBlock.SetValue(Grid.RowProperty, Index * 8 + 4);
                MACAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(MACAddressBlock);

                TextBlock PrimaryDNSDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Globalization.GetString("SystemInfo_Dialog_Primary_DNS_Text")
                };
                PrimaryDNSDescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 5);
                PrimaryDNSDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(PrimaryDNSDescriptionBlock);

                TextBlock PrimaryDNSAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPProperties.DnsAddresses.FirstOrDefault()?.ToString() ?? Globalization.GetString("UnknownText")
                };
                PrimaryDNSAddressBlock.SetValue(Grid.RowProperty, Index * 8 + 5);
                PrimaryDNSAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(PrimaryDNSAddressBlock);

                TextBlock SecondaryDNSDescriptionBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = Globalization.GetString("SystemInfo_Dialog_Secondary_DNS_Text")
                };
                SecondaryDNSDescriptionBlock.SetValue(Grid.RowProperty, Index * 8 + 6);
                SecondaryDNSDescriptionBlock.SetValue(Grid.ColumnProperty, 0);
                NetworkGrid.Children.Add(SecondaryDNSDescriptionBlock);

                TextBlock SecondaryDNSAddressBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = IPProperties.DnsAddresses.Skip(1).FirstOrDefault()?.ToString() ?? Globalization.GetString("UnknownText")
                };
                SecondaryDNSAddressBlock.SetValue(Grid.RowProperty, Index * 8 + 6);
                SecondaryDNSAddressBlock.SetValue(Grid.ColumnProperty, 1);
                NetworkGrid.Children.Add(SecondaryDNSAddressBlock);
            }
        }
    }
}
