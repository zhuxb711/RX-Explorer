using System;
using System.Linq;
using System.Text;
using SystemInformationProvider;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
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

                if (SystemInformation._3DNOW)
                {
                    _ = sb.Append("、3DNow");
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
            WindowsVersion = "Windows 10 " + Environment.OSVersion.Version.ToString();
            DeviceName = EAS.FriendlyName;
            DeviceModel = string.IsNullOrEmpty(EAS.SystemProductName) ? "Unknown" : EAS.SystemProductName;

            for (int i = 0; i < SystemInformation.GraphicAdapterInfo.Length; i++)
            {
                string GPUInfoGroup = SystemInformation.GraphicAdapterInfo[i];
                string[] GPUInfo = GPUInfoGroup.Split("||");
                int GPUMemory = Convert.ToInt32(GPUInfo[1]);
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
        }
    }
}
