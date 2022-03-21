using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Vanara.PInvoke;
using Windows.UI.Notifications;

namespace SystemLaunchHelper
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
                {
                    using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "regedit.exe",
                        Verb = "runas",
                        CreateNoWindow = true,
                        UseShellExecute = true,
                        Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\RestoreAll.reg")}\"",
                    }))
                    {
                        RegisterProcess.WaitForExit();
                    }

                    bool AllSuccess = true;

                    foreach (string Path in Directory.EnumerateFiles(AppDomain.CurrentDomain.BaseDirectory, "*", SearchOption.AllDirectories).Concat(Directory.EnumerateDirectories(AppDomain.CurrentDomain.BaseDirectory, "*", SearchOption.AllDirectories)))
                    {
                        AllSuccess &= Kernel32.MoveFileEx(Path, null, Kernel32.MOVEFILE.MOVEFILE_DELAY_UNTIL_REBOOT);
                    }

                    AllSuccess &= Kernel32.MoveFileEx(AppDomain.CurrentDomain.BaseDirectory, null, Kernel32.MOVEFILE.MOVEFILE_DELAY_UNTIL_REBOOT);

                    if (!AllSuccess)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-Command \"Start-Sleep -Seconds 5;Remove-Item -Path \"{AppDomain.CurrentDomain.BaseDirectory}\" -Recurse -Force\"",
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }).Dispose();
                    }
                }
                else
                {
                    string AliasLocation = null;

                    try
                    {
                        using (Process Pro = Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = "-Command \"Get-Command RX-Explorer | Format-List -Property Source\"",
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }))
                        {
                            try
                            {
                                string OutputString = Pro.StandardOutput.ReadToEnd();

                                if (!string.IsNullOrWhiteSpace(OutputString))
                                {
                                    string Path = OutputString.Replace(Environment.NewLine, string.Empty).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                                    if (File.Exists(Path))
                                    {
                                        AliasLocation = Path;
                                    }
                                }
                            }
                            finally
                            {
                                if (!Pro.WaitForExit(5000))
                                {
                                    Pro.Kill();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not get alias location by Powershell, message: {ex.Message}");
                    }

                    if (string.IsNullOrEmpty(AliasLocation))
                    {
                        string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)
                                                                   .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                                                                   .Concat(Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)
                                                                                      .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                                                                   .Distinct()
                                                                   .ToArray();

                        if (EnvironmentVariables.Where((Var) => Var.Contains("WindowsApps")).Select((Var) => Path.Combine(Var, "RX-Explorer.exe")).FirstOrDefault((Path) => File.Exists(Path)) is string Location)
                        {
                            AliasLocation = Location;
                        }
                        else
                        {
                            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                            if (!string.IsNullOrEmpty(AppDataPath) && Directory.Exists(AppDataPath))
                            {
                                string WindowsAppsPath = Path.Combine(AppDataPath, "Microsoft", "WindowsApps");

                                if (Directory.Exists(WindowsAppsPath))
                                {
                                    string RXPath = Path.Combine(WindowsAppsPath, "RX-Explorer.exe");

                                    if (File.Exists(RXPath))
                                    {
                                        AliasLocation = RXPath;
                                    }
                                }
                            }
                        }
                    }

                    if (args.FirstOrDefault() == "-Command")
                    {
                        switch (args.LastOrDefault())
                        {
                            case "InterceptWinE":
                                {
                                    if (!string.IsNullOrEmpty(AliasLocation))
                                    {
                                        string TempFilePath = Path.Combine(Path.GetTempPath(), @$"{Guid.NewGuid()}.reg");

                                        try
                                        {
                                            using (FileStream TempFileStream = File.Open(TempFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                                            using (FileStream RegStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Intercept_WIN_E.reg"), FileMode.Open, FileAccess.Read, FileShare.Read))
                                            using (StreamReader Reader = new StreamReader(RegStream))
                                            {
                                                string Content = Reader.ReadToEnd();

                                                using (StreamWriter Writer = new StreamWriter(TempFileStream, Encoding.Unicode))
                                                {
                                                    Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                                }
                                            }

                                            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                            {
                                                FileName = "regedit.exe",
                                                Verb = "runas",
                                                CreateNoWindow = true,
                                                UseShellExecute = true,
                                                Arguments = $"/s \"{TempFilePath}\"",
                                            }))
                                            {
                                                RegisterProcess.WaitForExit();
                                            }
                                        }
                                        finally
                                        {
                                            if (File.Exists(TempFilePath))
                                            {
                                                File.Delete(TempFilePath);
                                            }
                                        }

                                        bool IsRegistryCheckingSuccess = true;

                                        try
                                        {
                                            using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false))
                                            {
                                                if (Key != null)
                                                {
                                                    if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase) || Key.GetValue("DelegateExecute") != null)
                                                    {
                                                        IsRegistryCheckingSuccess = false;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"Registry checking failed, message: {ex.Message}");
                                        }

                                        if (!IsRegistryCheckingSuccess)
                                        {
                                            Environment.ExitCode = 2;
                                        }
                                    }
                                    else
                                    {
                                        Environment.ExitCode = 1;
                                    }

                                    break;
                                }
                            case "InterceptFolder":
                                {
                                    if (!string.IsNullOrEmpty(AliasLocation))
                                    {
                                        string TempFilePath = Path.Combine(Path.GetTempPath(), @$"{Guid.NewGuid()}.reg");

                                        try
                                        {
                                            using (FileStream TempFileStream = File.Open(TempFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                                            using (FileStream RegStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Intercept_Folder.reg"), FileMode.Open, FileAccess.Read, FileShare.Read))
                                            using (StreamReader Reader = new StreamReader(RegStream))
                                            {
                                                string Content = Reader.ReadToEnd();

                                                using (StreamWriter Writer = new StreamWriter(TempFileStream, Encoding.Unicode))
                                                {
                                                    Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                                }
                                            }

                                            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                            {
                                                FileName = "regedit.exe",
                                                Verb = "runas",
                                                CreateNoWindow = true,
                                                UseShellExecute = true,
                                                Arguments = $"/s \"{TempFilePath}\"",
                                            }))
                                            {
                                                RegisterProcess.WaitForExit();
                                            }
                                        }
                                        finally
                                        {
                                            if (File.Exists(TempFilePath))
                                            {
                                                File.Delete(TempFilePath);
                                            }
                                        }

                                        bool IsRegistryCheckingSuccess = true;

                                        try
                                        {
                                            using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Directory", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                            {
                                                if (Key != null)
                                                {
                                                    if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        IsRegistryCheckingSuccess = false;
                                                    }
                                                }
                                            }

                                            using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Drive", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                            {
                                                if (Key != null)
                                                {
                                                    if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        IsRegistryCheckingSuccess = false;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"Registry checking failed, message: {ex.Message}");
                                        }

                                        if (!IsRegistryCheckingSuccess)
                                        {
                                            Environment.ExitCode = 2;
                                        }
                                    }
                                    else
                                    {
                                        Environment.ExitCode = 1;
                                    }

                                    break;
                                }
                            case "RestoreWinE":
                                {
                                    using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "regedit.exe",
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true,
                                        Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_WIN_E.reg")}\"",
                                    }))
                                    {
                                        RegisterProcess.WaitForExit();
                                    }

                                    bool IsRegistryCheckingSuccess = true;

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false))
                                        {
                                            if (Key != null)
                                            {
                                                if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsRegistryCheckingSuccess = false;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Registry checking failed, message: {ex.Message}");
                                    }

                                    if (!IsRegistryCheckingSuccess)
                                    {
                                        Environment.ExitCode = 2;
                                    }

                                    break;
                                }
                            case "RestoreFolder":
                                {
                                    using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "regedit.exe",
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true,
                                        Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_Folder.reg")}\"",
                                    }))
                                    {
                                        RegisterProcess.WaitForExit();
                                    }

                                    bool IsRegistryCheckingSuccess = true;

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("Directory", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                        {
                                            if (Key != null)
                                            {
                                                if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsRegistryCheckingSuccess = false;
                                                }
                                            }
                                        }

                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Drive", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                        {
                                            if (Key != null)
                                            {
                                                if (!string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsRegistryCheckingSuccess = false;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"Registry checking failed, message: {ex.Message}");
                                    }

                                    if (!IsRegistryCheckingSuccess)
                                    {
                                        Environment.ExitCode = 2;
                                    }

                                    break;
                                }
                        }
                    }
                    else
                    {
                        string TargetPath = args.FirstOrDefault() ?? string.Empty;

                        if (string.IsNullOrEmpty(AliasLocation))
                        {
                            Process.Start("explorer.exe", $"\"{TargetPath}\"").Dispose();

                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                              .SetToastScenario(ToastScenario.Reminder)
                                                              .AddToastActivationInfo("UnregisterActivate", ToastActivationType.Foreground)
                                                              .AddText("Test")
                                                              .AddText("Test1111111111111111111111111111111")
                                                              .AddButton(new ToastButton("Now", "UnregisterActivate"))
                                                              .AddButton(new ToastButtonDismiss("Later"));

                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                            {
                                Tag = "UnregisterActivate",
                                Priority = ToastNotificationPriority.Default
                            });
                        }
                        else
                        {
                            Process.Start(AliasLocation, $"\"{TargetPath}\"").Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected exception was thew, message: {ex.Message}");
                Environment.ExitCode = 3;
            }
        }
    }
}