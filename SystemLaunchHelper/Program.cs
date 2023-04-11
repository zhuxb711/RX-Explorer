using CommandLine;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using SystemLaunchHelper.Class;
using Vanara.PInvoke;
using Windows.UI.Popups;
using WinRT.Interop;

namespace SystemLaunchHelper
{
    internal class Program
    {
        [STAThread]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandLineOptions))]
        static void Main(string[] args)
        {
            Parser ArgumentParser = new Parser((With) =>
            {
                With.AutoHelp = true;
                With.CaseInsensitiveEnumValues = true;
                With.IgnoreUnknownArguments = true;
                With.CaseSensitive = true;
            });

            Environment.Exit((int)ArgumentParser.ParseArguments<CommandLineOptions>(args.SkipWhile((Value) => Value.EndsWith(Path.GetFileName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase))).MapResult((Options) =>
            {
                try
                {
                    switch (Options.Command)
                    {
                        case "InterceptWinE":
                            {
                                using (Process CurrentProcess = Process.GetCurrentProcess())
                                {
                                    string CurrentPath = CurrentProcess.MainModule.FileName;
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
                                                Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"\\\"{CurrentPath.Replace(@"\", @"\\")}\\\" \\\"%L\\\""));
                                            }
                                        }

                                        using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                        {
                                            FileName = "regedit.exe",
                                            Verb = "runas",
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

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\opennewwindow\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!Convert.ToString(Key.GetValue(string.Empty)).Contains(CurrentPath, StringComparison.OrdinalIgnoreCase) || Key.GetValue("DelegateExecute") != null)
                                                {
                                                    return ExitCodeEnum.FailedOnRegistryCheck;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }
                                }

                                break;
                            }
                        case "InterceptFolder":
                            {
                                using (Process CurrentProcess = Process.GetCurrentProcess())
                                {
                                    string CurrentPath = CurrentProcess.MainModule.FileName;
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
                                                Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"\\\"{CurrentPath.Replace(@"\", @"\\")}\\\" \\\"%L\\\""));
                                            }
                                        }

                                        using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                        {
                                            FileName = "regedit.exe",
                                            Verb = "runas",
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

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\open\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!Convert.ToString(Key.GetValue(string.Empty)).Contains(CurrentPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    return ExitCodeEnum.FailedOnRegistryCheck;
                                                }
                                            }
                                        }

                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Drive\shell\open\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!Convert.ToString(Key.GetValue(string.Empty)).Contains(CurrentPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    return ExitCodeEnum.FailedOnRegistryCheck;
                                                }
                                            }
                                        }

                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\explore\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!Convert.ToString(Key.GetValue(string.Empty)).Contains(CurrentPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    return ExitCodeEnum.FailedOnRegistryCheck;
                                                }
                                            }
                                        }

                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\open\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!Convert.ToString(Key.GetValue(string.Empty)).Contains(CurrentPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    return ExitCodeEnum.FailedOnRegistryCheck;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }
                                }

                                break;
                            }
                        case "RestoreWinE":
                            {
                                using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "regedit.exe",
                                    Verb = "runas",
                                    UseShellExecute = true,
                                    Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_WIN_E.reg")}\"",
                                }))
                                {
                                    RegisterProcess.WaitForExit();
                                }

                                try
                                {
                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\opennewwindow\command", RegistryRights.ReadKey))
                                    {
                                        if (Key != null)
                                        {
                                            if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                            {
                                                return ExitCodeEnum.FailedOnRegistryCheck;
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    //No need to handle this exception
                                }

                                if (!Options.SuppressSelfDeletion)
                                {
                                    bool IsAnotherRegistryKeyExists = false;

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\Directory\open\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsAnotherRegistryKeyExists = true;
                                                }
                                            }
                                        }

                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Drive\shell\open\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (!string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsAnotherRegistryKeyExists = true;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }

                                    if (!IsAnotherRegistryKeyExists)
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            FileName = "powershell.exe",
                                            Arguments = $"-Command \"Wait-Process -Id {Environment.ProcessId} -Timeout 30;Stop-Process -Id {Environment.ProcessId} -Force;Remove-Item -Path '{AppDomain.CurrentDomain.BaseDirectory}' -Recurse -Force\"",
                                            CreateNoWindow = true,
                                            UseShellExecute = false
                                        }).Dispose();
                                    }
                                }

                                break;
                            }
                        case "RestoreFolder":
                            {
                                using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "regedit.exe",
                                    Verb = "runas",
                                    UseShellExecute = true,
                                    Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_Folder.reg")}\"",
                                }))
                                {
                                    RegisterProcess.WaitForExit();
                                }

                                try
                                {
                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\Directory\open\command", RegistryRights.ReadKey))
                                    {
                                        if (Key != null)
                                        {
                                            if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                            {
                                                return ExitCodeEnum.FailedOnRegistryCheck;
                                            }
                                        }
                                    }

                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Drive\shell\open\command", RegistryRights.ReadKey))
                                    {
                                        if (Key != null)
                                        {
                                            if (!string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                            {
                                                return ExitCodeEnum.FailedOnRegistryCheck;
                                            }
                                        }
                                    }

                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\open\command", RegistryRights.ReadKey))
                                    {
                                        if (Key != null)
                                        {
                                            if (Convert.ToString(Key.GetValue(string.Empty)) != Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Explorer.exe"))
                                            {
                                                return ExitCodeEnum.FailedOnRegistryCheck;
                                            }
                                        }
                                    }

                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\explore\command", RegistryRights.ReadKey))
                                    {
                                        if (Key != null)
                                        {
                                            if (!string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                            {
                                                return ExitCodeEnum.FailedOnRegistryCheck;
                                            }
                                        }
                                    }
                                }
                                catch (Exception)
                                {
                                    //No need to handle this exception
                                }

                                if (!Options.SuppressSelfDeletion)
                                {
                                    bool IsAnotherRegistryKeyExists = false;

                                    try
                                    {
                                        using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey(@"Folder\shell\opennewwindow\command", RegistryRights.ReadKey))
                                        {
                                            if (Key != null)
                                            {
                                                if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                                {
                                                    IsAnotherRegistryKeyExists = true;
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        //No need to handle this exception
                                    }

                                    if (!IsAnotherRegistryKeyExists)
                                    {
                                        Process.Start(new ProcessStartInfo
                                        {
                                            UseShellExecute = false,
                                            FileName = "powershell.exe",
                                            Arguments = $"-Command \"Wait-Process -Id {Environment.ProcessId} -Timeout 30;Stop-Process -Id {Environment.ProcessId} -Force;Remove-Item -Path '{AppDomain.CurrentDomain.BaseDirectory}' -Recurse -Force\"",
                                            CreateNoWindow = true,
                                        }).Dispose();
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (Options.PathList.Any())
                                {
                                    string StartupArguments = $"{string.Join(' ', Options.PathList.Select((Item) => $"\"{Item}\""))}";

                                    if (!string.IsNullOrEmpty(Helper.GetPackageFullNameFromPackageFamilyName("36186RuoFan.USB_q3e6crc0w375t")))
                                    {
                                        try
                                        {
                                            Process.Start(new ProcessStartInfo
                                            {
                                                UseShellExecute = false,
                                                FileName = "RX-Explorer.exe",
                                                Arguments = StartupArguments,
                                            }).Dispose();
                                        }
                                        catch (Exception)
                                        {
                                            return ExitCodeEnum.FailedOnLaunchExplorer;
                                        }
                                    }
                                    else
                                    {
                                        if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Notification.lock")))
                                        {
                                            if (string.IsNullOrEmpty(StartupArguments))
                                            {
                                                Process.Start(new ProcessStartInfo
                                                {
                                                    UseShellExecute = false,
                                                    FileName = "explorer.exe",
                                                }).Dispose();
                                            }
                                            else
                                            {
                                                Process.Start(new ProcessStartInfo
                                                {
                                                    UseShellExecute = false,
                                                    FileName = "explorer.exe",
                                                    Arguments = $"\"{StartupArguments}\"",
                                                }).Dispose();
                                            }
                                        }
                                        else
                                        {
                                            string CurrentCultureCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName.ToLower();

                                            string TipText = CurrentCultureCode switch
                                            {
                                                "zh" => "检测到RX文件管理器已被卸载，但卸载前已启用与系统集成相关功能，这可能导致Windows文件管理器无法正常使用，您是否希望还原为默认设置?",
                                                "fr" => "Il est détecté que le RX-Explorer a été désinstallé, mais les fonctions liées à l'intégration du système ont été activées avant la désinstallation, ce qui peut casser l'Explorateur Windows. Voulez-vous restaurer les paramètres par défaut?",
                                                "es" => "Se detecta que el RX-Explorer se ha desinstalado, pero las funciones relacionadas con la integración del sistema se han habilitado antes de la desinstalación, lo que puede dañar el Explorador de Windows. ¿Desea restaurar la configuración predeterminada?",
                                                "de" => "Es wurde festgestellt, dass der RX-Explorer deinstalliert wurde, aber die systemintegrationsbezogenen Funktionen vor der Deinstallation aktiviert wurden, was den Windows Explorer beschädigen kann. Möchten Sie die Standardeinstellungen wiederherstellen?",
                                                _ => "It is detected that the RX-Explorer has been uninstalled, but the system integration-related functions have been enabled before uninstalling, which may broken the Windows Explorer. Do you want to restore to the default settings?"
                                            };

                                            string TipHeader = CurrentCultureCode switch
                                            {
                                                "zh" => "警告",
                                                "fr" => "Avertissement",
                                                "es" => "Advertencia",
                                                "de" => "Warnung",
                                                _ => "Warning"
                                            };

                                            string FirstButtonText = CurrentCultureCode switch
                                            {
                                                "zh" => "确认",
                                                "fr" => "Confirmer",
                                                "es" => "Confirmar",
                                                "de" => "Bestätigen Sie",
                                                _ => "Confirm"
                                            };

                                            string SecondButtonText = CurrentCultureCode switch
                                            {
                                                "zh" => "取消",
                                                "fr" => "Annuler",
                                                "es" => "Cancelar",
                                                "de" => "Stornieren",
                                                _ => "Cancel"
                                            };

                                            string ThirdButtonText = CurrentCultureCode switch
                                            {
                                                "zh" => "不再提示",
                                                "fr" => "Ne rappelle pas",
                                                "es" => "No recordar",
                                                "de" => "Erinner dich nicht",
                                                _ => "Do not remind"
                                            };

                                            MessageDialog Dialog = new MessageDialog(TipText, TipHeader)
                                            {
                                                DefaultCommandIndex = 0,
                                                CancelCommandIndex = 1
                                            };
                                            Dialog.Commands.Add(new UICommand(FirstButtonText, null, 0));
                                            Dialog.Commands.Add(new UICommand(SecondButtonText, null, 1));
                                            Dialog.Commands.Add(new UICommand(ThirdButtonText, null, 2));

                                            InitializeWithWindow.Initialize(Dialog, User32.GetDesktopWindow().DangerousGetHandle());

                                            switch (Dialog.ShowAsync().AsTask().Result.Id)
                                            {
                                                case 0:
                                                    {
                                                        using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                                        {
                                                            FileName = "regedit.exe",
                                                            Verb = "runas",
                                                            UseShellExecute = true,
                                                            Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\RestoreAll.reg")}\"",
                                                        }))
                                                        {
                                                            RegisterProcess.WaitForExit();
                                                        }

                                                        Process.Start(new ProcessStartInfo
                                                        {
                                                            FileName = "powershell.exe",
                                                            Arguments = $"-Command \"Wait-Process -Id {Environment.ProcessId} -Timeout 30;Stop-Process -Id {Environment.ProcessId} -Force;Remove-Item -Path '{AppDomain.CurrentDomain.BaseDirectory}' -Recurse -Force\"",
                                                            CreateNoWindow = true,
                                                            UseShellExecute = false
                                                        }).Dispose();

                                                        goto case 1;
                                                    }
                                                case 1:
                                                    {
                                                        if (string.IsNullOrEmpty(StartupArguments))
                                                        {
                                                            Process.Start(new ProcessStartInfo
                                                            {
                                                                FileName = "explorer.exe",
                                                                UseShellExecute = false
                                                            }).Dispose();
                                                        }
                                                        else
                                                        {
                                                            Process.Start(new ProcessStartInfo
                                                            {
                                                                FileName = "explorer.exe",
                                                                Arguments = $"\"{StartupArguments}\"",
                                                                UseShellExecute = false
                                                            }).Dispose();
                                                        }

                                                        break;
                                                    }
                                                case 2:
                                                    {
                                                        File.OpenHandle(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Notification.lock"), FileMode.OpenOrCreate).Dispose();

                                                        goto case 1;
                                                    }
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                    }

                    return ExitCodeEnum.Success;
                }
                catch (Exception)
                {
                    return ExitCodeEnum.FailedOnUnknownReason;
                }
            },
            (ErrorList) =>
            {
                if (ErrorList.Any(e => e.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError))
                {
                    return ExitCodeEnum.Success;
                }

                return ExitCodeEnum.FailedOnParseArguments;
            }));
        }
    }
}
