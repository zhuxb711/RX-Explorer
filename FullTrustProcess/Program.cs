using Microsoft.Win32;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Size = System.Drawing.Size;
using Timer = System.Threading.Timer;

namespace FullTrustProcess
{
    class Program
    {
        private static AppServiceConnection Connection;

        private static readonly Dictionary<string, NamedPipeServerStream> PipeServers = new Dictionary<string, NamedPipeServerStream>();

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        private static readonly object Locker = new object();

        private static Timer AliveCheckTimer;

        private static Process ExplorerProcess;

        static async Task Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = Package.Current.Id.FamilyName
                };
                Connection.RequestReceived += Connection_RequestReceived;
                Connection.ServiceClosed += Connection_ServiceClosed;

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    AliveCheckTimer = new Timer(AliveCheck, null, 10000, 10000);

                    try
                    {
                        //Loading the menu in advance can speed up the re-generation speed and ensure the stability of the number of menu items
                        string TempFolderPath = Environment.GetEnvironmentVariable("TMP");

                        if (Directory.Exists(TempFolderPath))
                        {
                            await ContextMenu.FetchContextMenuItemsAsync(TempFolderPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Load menu in advance threw an exception, message: {ex.Message}");
                    }
                }
                else
                {
                    ExitLocker.Set();
                }

                ExitLocker.WaitOne();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"FullTrustProcess threw an exception, message: {ex.Message}");
            }
            finally
            {
                Connection?.Dispose();
                ExitLocker?.Dispose();
                AliveCheckTimer?.Dispose();

                try
                {
                    PipeServers.Values.ToList().ForEach((Item) => Item.Dispose());
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Error when dispose PipeLine");
                }

                PipeServers.Clear();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception Ex)
            {
                LogTracer.Log(Ex, "UnhandledException");
            }
        }

        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            ExitLocker.Set();
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExecuteType"])
                {
                    case "Execute_LaunchUWP":
                        {
                            string[] PathArray = JsonSerializer.Deserialize<string[]>(Convert.ToString(args.Request.Message["LaunchPathArray"]));

                            if (args.Request.Message.TryGetValue("PackageFamilyName", out object PFN))
                            {
                                ValueSet Value = new ValueSet();

                                string PackageFamilyName = Convert.ToString(PFN);

                                if (string.IsNullOrEmpty(PackageFamilyName))
                                {
                                    Value.Add("Error", "PackageFamilyName could not empty");
                                }
                                else
                                {
                                    if (await Helper.LaunchApplicationFromPackageFamilyNameAsync(PackageFamilyName, PathArray))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not launch the UWP");
                                    }
                                }

                                await args.Request.SendResponseAsync(Value);
                            }
                            else if (args.Request.Message.TryGetValue("AppUserModelId", out object AUMID))
                            {
                                ValueSet Value = new ValueSet();

                                string AppUserModelId = Convert.ToString(AUMID);

                                if (string.IsNullOrEmpty(AppUserModelId))
                                {
                                    Value.Add("Error", "AppUserModelId could not empty");
                                }
                                else
                                {
                                    if (await Helper.LaunchApplicationFromAUMIDAsync(AppUserModelId, PathArray))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not launch the UWP");
                                    }
                                }

                                await args.Request.SendResponseAsync(Value);
                            }

                            break;
                        }
                    case "Execute_GetDocumentProperties":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                Dictionary<string, string> PropertiesDic = new Dictionary<string, string>(9);

                                using (ShellItem Item = new ShellItem(ExecutePath))
                                {
                                    if (Item.IShellItem is Shell32.IShellItem2 IShell2)
                                    {
                                        try
                                        {
                                            string LastAuthor = IShell2.GetString(Ole32.PROPERTYKEY.System.Document.LastAuthor);

                                            if (string.IsNullOrEmpty(LastAuthor))
                                            {
                                                PropertiesDic.Add("LastAuthor", string.Empty);
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("LastAuthor", LastAuthor);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("LastAuthor", string.Empty);
                                        }

                                        try
                                        {
                                            string Version = IShell2.GetString(Ole32.PROPERTYKEY.System.Document.Version);

                                            if (string.IsNullOrEmpty(Version))
                                            {
                                                PropertiesDic.Add("Version", string.Empty);
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("Version", Version);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("Version", string.Empty);
                                        }

                                        try
                                        {
                                            string RevisionNumber = IShell2.GetString(Ole32.PROPERTYKEY.System.Document.RevisionNumber);

                                            if (string.IsNullOrEmpty(RevisionNumber))
                                            {
                                                PropertiesDic.Add("RevisionNumber", string.Empty);
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("RevisionNumber", RevisionNumber);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("RevisionNumber", string.Empty);
                                        }

                                        try
                                        {
                                            string Template = IShell2.GetString(Ole32.PROPERTYKEY.System.Document.Template);

                                            if (string.IsNullOrEmpty(Template))
                                            {
                                                PropertiesDic.Add("Template", string.Empty);
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("Template", Template);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("Template", string.Empty);
                                        }

                                        try
                                        {
                                            int PageCount = IShell2.GetInt32(Ole32.PROPERTYKEY.System.Document.PageCount);

                                            if (PageCount > 0)
                                            {
                                                PropertiesDic.Add("PageCount", Convert.ToString(PageCount));
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("PageCount", string.Empty);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("PageCount", string.Empty);
                                        }

                                        try
                                        {
                                            int WordCount = IShell2.GetInt32(Ole32.PROPERTYKEY.System.Document.WordCount);

                                            if (WordCount > 0)
                                            {
                                                PropertiesDic.Add("WordCount", Convert.ToString(WordCount));
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("WordCount", string.Empty);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("WordCount", string.Empty);
                                        }

                                        try
                                        {
                                            int CharacterCount = IShell2.GetInt32(Ole32.PROPERTYKEY.System.Document.CharacterCount);

                                            if (CharacterCount > 0)
                                            {
                                                PropertiesDic.Add("CharacterCount", Convert.ToString(CharacterCount));
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("CharacterCount", string.Empty);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("CharacterCount", string.Empty);
                                        }

                                        try
                                        {
                                            int LineCount = IShell2.GetInt32(Ole32.PROPERTYKEY.System.Document.LineCount);

                                            if (LineCount > 0)
                                            {
                                                PropertiesDic.Add("LineCount", Convert.ToString(LineCount));
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("LineCount", string.Empty);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("LineCount", string.Empty);
                                        }

                                        try
                                        {
                                            ulong TotalEditingTime = IShell2.GetUInt64(Ole32.PROPERTYKEY.System.Document.TotalEditingTime);

                                            if (TotalEditingTime > 0)
                                            {
                                                PropertiesDic.Add("TotalEditingTime", Convert.ToString(TotalEditingTime));
                                            }
                                            else
                                            {
                                                PropertiesDic.Add("TotalEditingTime", string.Empty);
                                            }
                                        }
                                        catch
                                        {
                                            PropertiesDic.Add("TotalEditingTime", string.Empty);
                                        }
                                    }
                                }

                                Value.Add("Success", JsonSerializer.Serialize(PropertiesDic));
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetMIMEContentType":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet
                            {
                                { "Success", MIMEHelper.GetMIMEFromPath(ExecutePath)}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetHiddenItemInfo":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            using (ShellItem Item = ShellItem.Open(ExecutePath))
                            using (Image Thumbnail = Item.GetImage(new Size(128, 128), ShellItemGetImageOptions.BiggerSizeOk))
                            using (Bitmap OriginBitmap = new Bitmap(Thumbnail))
                            using (MemoryStream Stream = new MemoryStream())
                            {
                                OriginBitmap.MakeTransparent();
                                OriginBitmap.Save(Stream, ImageFormat.Png);

                                ValueSet Value = new ValueSet
                                {
                                    {"Success", JsonSerializer.Serialize(new HiddenDataPackage(Item.FileInfo.TypeName, Stream.ToArray()))}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }

                            break;
                        }
                    case "Execute_CheckIfEverythingAvailable":
                        {
                            await args.Request.SendResponseAsync(new ValueSet
                            {
                                {"Success", EverythingConnector.Current.IsAvailable }
                            });

                            break;
                        }
                    case "Execute_SearchByEverything":
                        {
                            string BaseLocation = Convert.ToString(args.Request.Message["BaseLocation"]);
                            string SearchWord = Convert.ToString(args.Request.Message["SearchWord"]);
                            bool SearchAsRegex = Convert.ToBoolean(args.Request.Message["SearchAsRegex"]);
                            bool IgnoreCase = Convert.ToBoolean(args.Request.Message["IgnoreCase"]);
                            uint MaxCount = Convert.ToUInt32(args.Request.Message["MaxCount"]);

                            ValueSet Value = new ValueSet();

                            if (EverythingConnector.Current.IsAvailable)
                            {
                                IEnumerable<string> SearchResult = EverythingConnector.Current.Search(BaseLocation, SearchWord, SearchAsRegex, IgnoreCase, MaxCount);

                                if (SearchResult.Any())
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                }
                                else
                                {
                                    EverythingConnector.StateCode Code = EverythingConnector.Current.GetLastErrorCode();

                                    if (Code == EverythingConnector.StateCode.OK)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                    }
                                    else
                                    {
                                        Value.Add("Error", $"Everything report an error, code: {Enum.GetName(typeof(EverythingConnector.StateCode), Code)}");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Everything is not available");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetContextMenuItems":
                        {
                            string[] ExecutePath = JsonSerializer.Deserialize<string[]>(Convert.ToString(args.Request.Message["ExecutePath"]));

                            ContextMenuPackage[] ContextMenuItems = await ContextMenu.FetchContextMenuItemsAsync(ExecutePath, Convert.ToBoolean(args.Request.Message["IncludeExtensionItem"]));

                            ValueSet Value = new ValueSet
                            {
                                {"Success", JsonSerializer.Serialize(ContextMenuItems) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_InvokeContextMenuItem":
                        {
                            string[] RelatedPath = JsonSerializer.Deserialize<string[]>(Convert.ToString(args.Request.Message["RelatedPath"]));
                            string Verb = Convert.ToString(args.Request.Message["Verb"]);
                            int Id = Convert.ToInt32(args.Request.Message["Id"]);
                            bool IncludeExtensionItem = Convert.ToBoolean(args.Request.Message["IncludeExtensionItem"]);

                            ValueSet Value = new ValueSet();

                            if (await ContextMenu.InvokeVerbAsync(RelatedPath, Verb, Id, IncludeExtensionItem))
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", $"Execute Id: \"{Id}\", Verb: \"{Verb}\" failed");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_CreateLink":
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            string Argument = string.Join(" ", Package.Argument.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para).ToArray());

                            using (ShellLink Link = ShellLink.Create(StorageController.GenerateUniquePath(Package.LinkPath), Package.LinkTargetPath, Package.Comment, Package.WorkDirectory, Argument))
                            {
                                Link.ShowState = (FormWindowState)Package.WindowState;
                                Link.RunAsAdministrator = Package.NeedRunAsAdmin;

                                if (Package.HotKey > 0)
                                {
                                    Link.HotKey = (((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                }
                            }

                            ValueSet Value = new ValueSet
                            {
                                { "Success", string.Empty }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetVariable_Path":
                        {
                            ValueSet Value = new ValueSet();

                            string Variable = Convert.ToString(args.Request.Message["Variable"]);

                            string Env = Environment.GetEnvironmentVariable(Variable);

                            if (string.IsNullOrEmpty(Env))
                            {
                                Value.Add("Error", "Could not found EnvironmentVariable");
                            }
                            else
                            {
                                Value.Add("Success", Env);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Rename":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string DesireName = Convert.ToString(args.Request.Message["DesireName"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                            {
                                if (StorageController.CheckCaptured(ExecutePath))
                                {
                                    Value.Add("Error_Capture", "FileLoadException");
                                }
                                else
                                {
                                    if (StorageController.CheckPermission(FileSystemRights.Modify, Path.GetDirectoryName(ExecutePath)))
                                    {
                                        if (!StorageController.Rename(ExecutePath, DesireName, (s, e) =>
                                        {
                                            Value.Add("Success", e.Name);
                                        }))
                                        {
                                            Value.Remove("Success");
                                            Value.Add("Error_Failure", "Error happened when rename");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "No Modify Permission");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "FileNotFoundException");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetInstalledApplication":
                        {
                            string PFN = Convert.ToString(args.Request.Message["PackageFamilyName"]);

                            InstalledApplicationPackage Pack = await Helper.GetInstalledApplicationAsync(PFN);

                            if (Pack != null)
                            {
                                ValueSet Value = new ValueSet
                                {
                                    {"Success", JsonSerializer.Serialize(Pack)}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }
                            else
                            {
                                ValueSet Value = new ValueSet
                                {
                                    {"Error",  "Could not found the package with PFN"}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }
                            break;
                        }
                    case "Execute_GetAllInstalledApplication":
                        {
                            ValueSet Value = new ValueSet
                            {
                                {"Success", JsonSerializer.Serialize(await Helper.GetInstalledApplicationAsync())}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_CheckPackageFamilyNameExist":
                        {
                            string PFN = Convert.ToString(args.Request.Message["PackageFamilyName"]);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", Helper.CheckIfPackageFamilyNameExist(PFN) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_UpdateUrl":
                        {
                            UrlDataPackage Package = JsonSerializer.Deserialize<UrlDataPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            ValueSet Value = new ValueSet();

                            if (File.Exists(Package.UrlPath))
                            {
                                List<string> SplitList;

                                using (StreamReader Reader = new StreamReader(Package.UrlPath, true))
                                {
                                    SplitList = Reader.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();

                                    string UrlLine = SplitList.FirstOrDefault((Line) => Line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));

                                    if (!string.IsNullOrEmpty(UrlLine))
                                    {
                                        SplitList.Remove(UrlLine);
                                        SplitList.Add($"URL={Package.UrlTargetPath}");
                                    }
                                }

                                using (StreamWriter Writer = new StreamWriter(Package.UrlPath, false))
                                {
                                    Writer.Write(string.Join(Environment.NewLine, SplitList));
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_UpdateLink":
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            string Argument = string.Join(" ", Package.Argument.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para).ToArray());

                            ValueSet Value = new ValueSet();

                            if (File.Exists(Package.LinkPath))
                            {
                                if (Path.IsPathRooted(Package.LinkTargetPath))
                                {
                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.TargetPath = Package.LinkTargetPath;
                                        Link.WorkingDirectory = Package.WorkDirectory;
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.RunAsAdministrator = Package.NeedRunAsAdmin;
                                        Link.Description = Package.Comment;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = (((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }
                                else if (Helper.CheckIfPackageFamilyNameExist(Package.LinkTargetPath))
                                {
                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.Description = Package.Comment;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path is not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_SetFileAttribute":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] AttributeGourp = JsonSerializer.Deserialize<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[]>(Convert.ToString(args.Request.Message["Attributes"]));

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                FileInfo File = new FileInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        File.Attributes |= AttributePair.Value;
                                    }
                                    else
                                    {
                                        File.Attributes &= ~AttributePair.Value;
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else if (Directory.Exists(ExecutePath))
                            {
                                DirectoryInfo Dir = new DirectoryInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (FileInfo SubFile in Helper.GetAllSubFiles(Dir))
                                            {
                                                SubFile.Attributes |= AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes |= AttributePair.Value;
                                        }
                                    }
                                    else
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (FileInfo SubFile in Helper.GetAllSubFiles(Dir))
                                            {
                                                SubFile.Attributes &= ~AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes &= ~AttributePair.Value;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetUrlData":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                using (ShellItem Item = ShellItem.Open(ExecutePath))
                                {
                                    string UrlPath = Item.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetUrl);

                                    using (Image IconImage = Item.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit))
                                    using (MemoryStream IconStream = new MemoryStream())
                                    using (Bitmap TempBitmap = new Bitmap(IconImage))
                                    {
                                        TempBitmap.MakeTransparent();
                                        TempBitmap.Save(IconStream, ImageFormat.Png);

                                        Value.Add("Success", JsonSerializer.Serialize(new UrlDataPackage(ExecutePath, UrlPath, IconStream.ToArray())));
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetLnkData":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                StringBuilder ProductCode = new StringBuilder(39);
                                StringBuilder ComponentCode = new StringBuilder(39);

                                if (Msi.MsiGetShortcutTarget(ExecutePath, ProductCode, szComponentCode: ComponentCode).Succeeded)
                                {
                                    uint Length = 0;

                                    StringBuilder ActualPathBuilder = new StringBuilder();

                                    Msi.INSTALLSTATE State = Msi.MsiGetComponentPath(ProductCode.ToString(), ComponentCode.ToString(), ActualPathBuilder, ref Length);

                                    if (State == Msi.INSTALLSTATE.INSTALLSTATE_LOCAL || State == Msi.INSTALLSTATE.INSTALLSTATE_SOURCE)
                                    {
                                        string ActualPath = ActualPathBuilder.ToString();

                                        foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                        {
                                            ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                        }

                                        using (ShellItem Item = new ShellItem(ActualPath))
                                        using (Image IconImage = Item.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit))
                                        using (MemoryStream IconStream = new MemoryStream())
                                        {
                                            Bitmap TempBitmap = new Bitmap(IconImage);
                                            TempBitmap.MakeTransparent();
                                            TempBitmap.Save(IconStream, ImageFormat.Png);

                                            Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, ActualPath, string.Empty, WindowState.Normal, 0, string.Empty, false, IconStream.ToArray())));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Lnk file could not be analysis by MsiGetShortcutTarget");
                                    }
                                }
                                else
                                {
                                    using (ShellLink Link = new ShellLink(ExecutePath))
                                    {
                                        if (string.IsNullOrEmpty(Link.TargetPath))
                                        {
                                            string PackageFamilyName = Helper.GetPackageFamilyNameFromUWPShellLink(ExecutePath);

                                            if (string.IsNullOrEmpty(PackageFamilyName))
                                            {
                                                Value.Add("Error", "TargetPath is invalid");
                                            }
                                            else
                                            {
                                                byte[] IconData = await Helper.GetIconDataFromPackageFamilyNameAsync(PackageFamilyName);

                                                Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, PackageFamilyName, string.Empty, (WindowState)Enum.Parse(typeof(WindowState), Enum.GetName(typeof(FormWindowState), Link.ShowState)), (int)Link.HotKey, Link.Description, false, IconData)));
                                            }
                                        }
                                        else
                                        {
                                            MatchCollection Collection = Regex.Matches(Link.Arguments, "[^ \"]+|\"[^\"]*\"");

                                            List<string> Arguments = new List<string>(Collection.Count);

                                            foreach (Match Mat in Collection)
                                            {
                                                Arguments.Add(Mat.Value);
                                            }

                                            string ActualPath = Link.TargetPath;

                                            foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                            {
                                                ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                            }

                                            using (Image IconImage = Link.GetImage(new Size(120, 120), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ScaleUp))
                                            using (MemoryStream IconStream = new MemoryStream())
                                            using (Bitmap TempBitmap = new Bitmap(IconImage))
                                            {
                                                TempBitmap.MakeTransparent();
                                                TempBitmap.Save(IconStream, ImageFormat.Png);

                                                Value.Add("Success", JsonSerializer.Serialize(new LinkDataPackage(ExecutePath, ActualPath, Link.WorkingDirectory, (WindowState)Enum.Parse(typeof(WindowState), Enum.GetName(typeof(FormWindowState), Link.ShowState)), (int)Link.HotKey, Link.Description, Link.RunAsAdministrator, IconStream.ToArray(), Arguments.ToArray())));
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File is not exist");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Intercept_Win_E":
                        {
                            ValueSet Value = new ValueSet();

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
                                        if (!Pro.WaitForExit(1000))
                                        {
                                            Pro.Kill();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not get alias location by Powershell");
                            }

                            if (string.IsNullOrEmpty(AliasLocation))
                            {
                                string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                                                            .Concat(Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)).Distinct().ToArray();

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

                            if (!string.IsNullOrEmpty(AliasLocation))
                            {
                                StorageFile InterceptFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Intercept_WIN_E.reg"));
                                StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("Intercept_WIN_E_Temp.reg", CreationCollisionOption.ReplaceExisting);

                                using (Stream FileStream = await InterceptFile.OpenStreamForReadAsync())
                                using (StreamReader Reader = new StreamReader(FileStream))
                                {
                                    string Content = await Reader.ReadToEndAsync();

                                    using (Stream TempStream = await TempFile.OpenStreamForWriteAsync())
                                    using (StreamWriter Writer = new StreamWriter(TempStream, Encoding.Unicode))
                                    {
                                        await Writer.WriteAsync(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                    }
                                }

                                using (Process RegisterProcess = new Process())
                                {
                                    RegisterProcess.StartInfo.FileName = TempFile.Path;
                                    RegisterProcess.StartInfo.UseShellExecute = true;
                                    RegisterProcess.Start();

                                    SetWindowsZPosition(RegisterProcess);
                                    RegisterProcess.WaitForExit();
                                }

                                try
                                {
                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false))
                                    {
                                        if (Key != null)
                                        {
                                            if (Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase) && Key.GetValue("DelegateExecute") == null)
                                            {
                                                Value.Add("Success", string.Empty);
                                            }
                                            else
                                            {
                                                Value.Add("Error", "Registry verification failed");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Success", string.Empty);
                                        }
                                    }
                                }
                                catch
                                {
                                    Value.Add("Success", string.Empty);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Alias file is not exists");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Restore_Win_E":
                        {
                            ValueSet Value = new ValueSet();

                            StorageFile RestoreFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_WIN_E.reg"));

                            using (Process Process = Process.Start(RestoreFile.Path))
                            {
                                SetWindowsZPosition(Process);
                                Process.WaitForExit();
                            }

                            try
                            {
                                RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false);

                                if (Key != null)
                                {
                                    try
                                    {
                                        if (Convert.ToString(Key.GetValue("DelegateExecute")) == "{11dbb47c-a525-400b-9e80-a54615a090c0}" && string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                        {
                                            Value.Add("Success", string.Empty);
                                        }
                                        else
                                        {
                                            Value.Add("Error", "Registry verification failed");
                                        }
                                    }
                                    finally
                                    {
                                        Key.Dispose();
                                    }
                                }
                                else
                                {
                                    Value.Add("Success", string.Empty);
                                }
                            }
                            catch
                            {
                                Value.Add("Success", string.Empty);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RequestCreateNewPipe":
                        {
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);

                            if (!PipeServers.ContainsKey(Guid))
                            {
                                NamedPipeServerStream NewPipeServer = new NamedPipeServerStream($@"Explorer_And_FullTrustProcess_NamedPipe-{Guid}", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 2048, 2048, null, HandleInheritability.None, PipeAccessRights.ChangePermissions);

                                PipeSecurity Security = NewPipeServer.GetAccessControl();
                                PipeAccessRule ClientRule = new PipeAccessRule(new SecurityIdentifier("S-1-15-2-1"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
                                PipeAccessRule OwnerRule = new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow);
                                Security.AddAccessRule(ClientRule);
                                Security.AddAccessRule(OwnerRule);
                                NewPipeServer.SetAccessControl(Security);

                                PipeServers.Add(Guid, NewPipeServer);

                                _ = NewPipeServer.WaitForConnectionAsync(new CancellationTokenSource(3000).Token).ContinueWith((task) =>
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipe))
                                    {
                                        Pipe.Dispose();
                                        PipeServers.Remove(Guid);
                                    }
                                }, TaskContinuationOptions.OnlyOnCanceled);
                            }

                            break;
                        }
                    case "Identity":
                        {
                            ValueSet Value = new ValueSet
                            {
                                { "Identity", "FullTrustProcess" }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Quicklook":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                QuicklookConnector.SendMessage(ExecutePath);
                            }

                            break;
                        }
                    case "Execute_Check_QuicklookIsAvaliable":
                        {
                            bool IsSuccess = QuicklookConnector.CheckQuicklookIsAvaliable();

                            ValueSet Result = new ValueSet
                            {
                                {"Check_QuicklookIsAvaliable_Result", IsSuccess }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_Association":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Associate_Result", JsonSerializer.Serialize(ExtensionAssociate.GetAllAssociation(Path)) }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Default_Association":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Success", ExtensionAssociate.GetDefaultProgramPathRelated(Path)}
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_RecycleBinItems":
                        {
                            ValueSet Result = new ValueSet();

                            string RecycleItemResult = RecycleBinController.GenerateRecycleItemsByJson();

                            if (string.IsNullOrEmpty(RecycleItemResult))
                            {
                                Result.Add("Error", "Could not get recycle items");
                            }
                            else
                            {
                                Result.Add("RecycleBinItems_Json_Result", RecycleItemResult);
                            }

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Empty_RecycleBin":
                        {
                            ValueSet Result = new ValueSet
                            {
                                { "RecycleBinItems_Clear_Result", RecycleBinController.EmptyRecycleBin() }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Restore_RecycleItem":
                        {
                            string[] PathList = JsonSerializer.Deserialize<string[]>(Convert.ToString(args.Request.Message["ExecutePath"]));

                            ValueSet Result = new ValueSet
                            {
                                {"Restore_Result", RecycleBinController.Restore(PathList) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_Delete_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Delete_Result", RecycleBinController.Delete(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_EjectUSB":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (string.IsNullOrEmpty(Path))
                            {
                                Value.Add("EjectResult", false);
                            }
                            else
                            {
                                Value.Add("EjectResult", USBController.EjectDevice(Path));
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Execute_Unlock_Occupy":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);
                            bool ForceClose = Convert.ToBoolean(args.Request.Message["ForceClose"]);

                            if (File.Exists(Path))
                            {
                                if (StorageController.CheckCaptured(Path))
                                {
                                    IReadOnlyList<Process> LockingProcesses = StorageController.GetLockingProcesses(Path);

                                    try
                                    {
                                        foreach (Process Pro in LockingProcesses)
                                        {
                                            if (ForceClose || !Pro.CloseMainWindow())
                                            {
                                                Pro.Kill();
                                            }
                                        }

                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"Kill process failed, reason: {ex.Message}");
                                        Value.Add("Error_Failure", "Unoccupied failed");
                                    }
                                    finally
                                    {
                                        foreach (Process Pro in LockingProcesses)
                                        {
                                            try
                                            {
                                                if (!ForceClose)
                                                {
                                                    Pro.WaitForExit();
                                                }

                                                Pro.Dispose();
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, "Process is no longer running");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotOccupy", "The file is not occupied");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFoundOrNotFile", "Path is not a file");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Copy":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            CollisionOptions Option = (CollisionOptions)Enum.Parse(typeof(CollisionOptions), Convert.ToString(args.Request.Message["CollisionOptions"]));

                            List<string> SourcePathList = JsonSerializer.Deserialize<List<string>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                            {
                                if (StorageController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                {
                                    if (StorageController.Copy(SourcePathList, DestinationPath, Option, (s, e) =>
                                    {
                                        lock (Locker)
                                        {
                                            try
                                            {
                                                Progress = e.ProgressPercentage;

                                                if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                {
                                                    using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                    {
                                                        Writer.WriteLine(e.ProgressPercentage);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, "Could not send progress data");
                                            }
                                        }
                                    },
                                    (se, arg) =>
                                    {
                                        if (arg.Result == HRESULT.S_OK)
                                        {
                                            if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                            }
                                            else
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                            }
                                        }
                                    }))
                                    {
                                        Value.Add("Success", string.Empty);

                                        if (OperationRecordList.Count > 0)
                                        {
                                            Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while copying the folder");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Move":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            CollisionOptions Option = (CollisionOptions)Enum.Parse(typeof(CollisionOptions), Convert.ToString(args.Request.Message["CollisionOptions"]));

                            List<string> SourcePathList = JsonSerializer.Deserialize<List<string>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                            {
                                if (SourcePathList.Any((Item) => StorageController.CheckCaptured(Item)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                    {
                                        if (StorageController.Move(SourcePathList, DestinationPath, Option, (s, e) =>
                                        {
                                            lock (Locker)
                                            {
                                                try
                                                {
                                                    Progress = e.ProgressPercentage;

                                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                    {
                                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                        {
                                                            Writer.WriteLine(e.ProgressPercentage);
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogTracer.Log(ex, "Could not send progress data");
                                                }
                                            }
                                        },
                                        (se, arg) =>
                                        {
                                            if (arg.Result == HRESULT.COPYENGINE_S_DONT_PROCESS_CHILDREN)
                                            {
                                                if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                }
                                                else
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                }
                                            }
                                        }))
                                        {
                                            if (SourcePathList.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                            {
                                                Value.Add("Success", string.Empty);

                                                if (OperationRecordList.Count > 0)
                                                {
                                                    Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                                }
                                            }
                                            else
                                            {
                                                Value.Add("Error_Capture", "An error occurred while moving the folder");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "An error occurred while moving the folder");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while moving the folder");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not send progress data");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Execute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExecutePathJson = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);

                            List<string> ExecutePathList = JsonSerializer.Deserialize<List<string>>(ExecutePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            try
                            {
                                if (ExecutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                {
                                    if (ExecutePathList.Any((Item) => StorageController.CheckCaptured(Item)))
                                    {
                                        Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                    }
                                    else
                                    {
                                        if (ExecutePathList.All((Path) => StorageController.CheckPermission(FileSystemRights.Modify, System.IO.Path.GetDirectoryName(Path))))
                                        {
                                            if (StorageController.Delete(ExecutePathList, PermanentDelete, (s, e) =>
                                            {
                                                lock (Locker)
                                                {
                                                    try
                                                    {
                                                        Progress = e.ProgressPercentage;

                                                        if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                        {
                                                            using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                            {
                                                                Writer.WriteLine(e.ProgressPercentage);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, "Could not send progress data");
                                                    }
                                                }
                                            },
                                            (se, arg) =>
                                            {
                                                if (!PermanentDelete)
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                }
                                            }))
                                            {
                                                if (ExecutePathList.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                {
                                                    Value.Add("Success", string.Empty);

                                                    if (OperationRecordList.Count > 0)
                                                    {
                                                        Value.Add("OperationRecord", JsonSerializer.Serialize(OperationRecordList));
                                                    }
                                                }
                                                else
                                                {
                                                    Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                                }
                                            }
                                            else
                                            {
                                                Value.Add("Error_Failure", "The specified file could not be deleted");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotFound", "ExecutePath is not a file or directory");
                                }
                            }
                            catch
                            {
                                Value.Add("Error_Failure", "The specified file or folder could not be deleted");
                            }

                            if (Progress < 100)
                            {
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RunExe":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string ExecuteParameter = Convert.ToString(args.Request.Message["ExecuteParameter"]);
                            string ExecuteAuthority = Convert.ToString(args.Request.Message["ExecuteAuthority"]);
                            string ExecuteWindowStyle = Convert.ToString(args.Request.Message["ExecuteWindowStyle"]);
                            string ExecuteWorkDirectory = Convert.ToString(args.Request.Message["ExecuteWorkDirectory"]);

                            bool ExecuteCreateNoWindow = Convert.ToBoolean(args.Request.Message["ExecuteCreateNoWindow"]);
                            bool ShouldWaitForExit = Convert.ToBoolean(args.Request.Message["ExecuteShouldWaitForExit"]);

                            ValueSet Value = new ValueSet();

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                if (StorageController.CheckPermission(FileSystemRights.ExecuteFile, ExecutePath))
                                {
                                    try
                                    {
                                        await Helper.ExecuteOnSTAThreadAsync(() =>
                                        {
                                            ShowWindowCommand WindowCommand;

                                            if (ExecuteCreateNoWindow)
                                            {
                                                WindowCommand = ShowWindowCommand.SW_HIDE;
                                            }
                                            else
                                            {
                                                switch ((ProcessWindowStyle)Enum.Parse(typeof(ProcessWindowStyle), ExecuteWindowStyle))
                                                {
                                                    case ProcessWindowStyle.Hidden:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_HIDE;
                                                            break;
                                                        }
                                                    case ProcessWindowStyle.Minimized:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_SHOWMINIMIZED;
                                                            break;
                                                        }
                                                    case ProcessWindowStyle.Maximized:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_SHOWMAXIMIZED;
                                                            break;
                                                        }
                                                    default:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_NORMAL;
                                                            break;
                                                        }
                                                }
                                            }

                                            Shell32.SHELLEXECUTEINFO ExecuteInfo = new Shell32.SHELLEXECUTEINFO
                                            {
                                                hwnd = HWND.NULL,
                                                lpVerb = ExecuteAuthority == "Administrator" ? "runas" : "open",
                                                cbSize = Marshal.SizeOf<Shell32.SHELLEXECUTEINFO>(),
                                                lpFile = ExecutePath,
                                                lpParameters = string.IsNullOrWhiteSpace(ExecuteParameter) ? null : ExecuteParameter,
                                                lpDirectory = string.IsNullOrWhiteSpace(ExecuteWorkDirectory) ? null : ExecuteWorkDirectory,
                                                fMask = Shell32.ShellExecuteMaskFlags.SEE_MASK_FLAG_NO_UI
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_UNICODE
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_DOENVSUBST
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOASYNC
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOCLOSEPROCESS,
                                                nShellExecuteShow = WindowCommand,
                                            };

                                            if (Shell32.ShellExecuteEx(ref ExecuteInfo))
                                            {
                                                if (ExecuteInfo.hProcess != HPROCESS.NULL)
                                                {
                                                    IntPtr Buffer = Marshal.AllocHGlobal(Marshal.SizeOf<NtDll.PROCESS_BASIC_INFORMATION>());

                                                    try
                                                    {
                                                        NtDll.PROCESS_BASIC_INFORMATION Info = new NtDll.PROCESS_BASIC_INFORMATION();

                                                        Marshal.StructureToPtr(Info, Buffer, false);

                                                        if (NtDll.NtQueryInformationProcess(ExecuteInfo.hProcess, NtDll.PROCESSINFOCLASS.ProcessBasicInformation, Buffer, Convert.ToUInt32(Marshal.SizeOf<NtDll.PROCESS_BASIC_INFORMATION>()), out _).Succeeded)
                                                        {
                                                            NtDll.PROCESS_BASIC_INFORMATION ResultInfo = Marshal.PtrToStructure<NtDll.PROCESS_BASIC_INFORMATION>(Buffer);

                                                            using (Process OpenedProcess = Process.GetProcessById(ResultInfo.UniqueProcessId.ToInt32()))
                                                            {
                                                                SetWindowsZPosition(OpenedProcess);
                                                            }
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        Marshal.FreeHGlobal(Buffer);
                                                        Kernel32.CloseHandle(ExecuteInfo.hProcess);
                                                    }
                                                }
                                            }
                                            else if (Path.GetExtension(ExecutePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Kernel32.CREATE_PROCESS CreationFlag = Kernel32.CREATE_PROCESS.NORMAL_PRIORITY_CLASS | Kernel32.CREATE_PROCESS.CREATE_UNICODE_ENVIRONMENT;

                                                if (ExecuteCreateNoWindow)
                                                {
                                                    CreationFlag |= Kernel32.CREATE_PROCESS.CREATE_NO_WINDOW;
                                                }

                                                Kernel32.STARTUPINFO SInfo = new Kernel32.STARTUPINFO
                                                {
                                                    cb = Convert.ToUInt32(Marshal.SizeOf<Kernel32.STARTUPINFO>()),
                                                    ShowWindowCommand = WindowCommand,
                                                    dwFlags = Kernel32.STARTF.STARTF_USESHOWWINDOW,
                                                };

                                                if (Kernel32.CreateProcess(lpCommandLine: new StringBuilder($"\"{ExecutePath}\" {(string.IsNullOrWhiteSpace(ExecuteParameter) ? string.Empty : ExecuteParameter)}"),
                                                                           bInheritHandles: false,
                                                                           dwCreationFlags: CreationFlag,
                                                                           lpCurrentDirectory: string.IsNullOrWhiteSpace(ExecuteWorkDirectory) ? null : ExecuteWorkDirectory,
                                                                           lpStartupInfo: SInfo,
                                                                           lpProcessInformation: out Kernel32.SafePROCESS_INFORMATION PInfo))

                                                {
                                                    try
                                                    {
                                                        using (Process OpenedProcess = Process.GetProcessById(Convert.ToInt32(PInfo.dwProcessId)))
                                                        {
                                                            SetWindowsZPosition(OpenedProcess);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        if (!PInfo.hProcess.IsInvalid && !PInfo.hProcess.IsNull)
                                                        {
                                                            PInfo.hProcess.Dispose();
                                                        }

                                                        if (!PInfo.hThread.IsInvalid && !PInfo.hThread.IsNull)
                                                        {
                                                            PInfo.hThread.Dispose();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                                }
                                            }
                                            else
                                            {
                                                throw new Win32Exception(Marshal.GetLastWin32Error());
                                            }
                                        });

                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        Value.Add("Error", $"Path: {ExecutePath}, Parameter: {ExecuteParameter}, Authority: {ExecuteAuthority}, ErrorMessage: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "The specified file could not be executed for lack of permission");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "ExecutePath could not be null or empty");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Test_Connection":
                        {
                            try
                            {
                                if (args.Request.Message.TryGetValue("ProcessId", out object ProcessId) && (ExplorerProcess?.Id).GetValueOrDefault() != Convert.ToInt32(ProcessId))
                                {
                                    ExplorerProcess = Process.GetProcessById(Convert.ToInt32(ProcessId));
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "GetProcess from id failed");
                            }

                            await args.Request.SendResponseAsync(new ValueSet { { "Execute_Test_Connection", string.Empty } });

                            break;
                        }
                    case "Paste_Remote_File":
                        {
                            string Path = Convert.ToString(args.Request.Message["Path"]);

                            ValueSet Value = new ValueSet();

                            if (await Helper.ExecuteOnSTAThreadAsync(() =>
                            {
                                try
                                {
                                    RemoteDataObject Rdo = new RemoteDataObject(Clipboard.GetDataObject());

                                    foreach (RemoteDataObject.DataPackage Package in Rdo.GetRemoteData())
                                    {
                                        try
                                        {
                                            if (Package.ItemType == RemoteDataObject.StorageType.File)
                                            {
                                                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                                if (!Directory.Exists(DirectoryPath))
                                                {
                                                    Directory.CreateDirectory(DirectoryPath);
                                                }

                                                string UniqueName = StorageController.GenerateUniquePath(System.IO.Path.Combine(Path, Package.Name));

                                                using (FileStream Stream = new FileStream(UniqueName, FileMode.CreateNew))
                                                {
                                                    Package.ContentStream.CopyTo(Stream);
                                                }
                                            }
                                            else
                                            {
                                                string DirectoryPath = System.IO.Path.Combine(Path, Package.Name);

                                                if (!Directory.Exists(DirectoryPath))
                                                {
                                                    Directory.CreateDirectory(DirectoryPath);
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            Package.Dispose();
                                        }
                                    }

                                    return true;
                                }
                                catch
                                {
                                    return false;
                                }
                            }))
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Clipboard is empty or could not get the content");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetThumbnailOverlay":
                        {
                            string Path = Convert.ToString(args.Request.Message["Path"]);

                            ValueSet Value = new ValueSet();

                            Value.Add("Success", JsonSerializer.Serialize(StorageController.GetThumbnailOverlay(Path)));

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_SetAsTopMostWindow":
                        {
                            string PackageFamilyName = Convert.ToString(args.Request.Message["PackageFamilyName"]);
                            uint? WithPID = args.Request.Message.ContainsKey("WithPID") ? Convert.ToUInt32(args.Request.Message["WithPID"]) : null;

                            ValueSet Value = new ValueSet();

                            if (Helper.GetUWPWindowInformation(PackageFamilyName, WithPID) is WindowInformation Info && !Info.Handle.IsNull)
                            {
                                User32.SetWindowPos(Info.Handle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Could not found the window handle");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RemoveTopMostWindow":
                        {
                            string PackageFamilyName = Convert.ToString(args.Request.Message["PackageFamilyName"]);
                            uint? WithPID = args.Request.Message.ContainsKey("WithPID") ? Convert.ToUInt32(args.Request.Message["WithPID"]) : null;

                            ValueSet Value = new ValueSet();

                            if (Helper.GetUWPWindowInformation(PackageFamilyName, WithPID) is WindowInformation Info && !Info.Handle.IsNull)
                            {
                                User32.SetWindowPos(Info.Handle, new IntPtr(-2), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Could not found the window handle");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Exit":
                        {
                            ExitLocker.Set();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Connection_RequestReceived)}");

                ValueSet Value = new ValueSet
                {
                    {"Error", ex.Message}
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                try
                {
                    Deferral.Complete();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Exception was threw when complete the deferral");
                }
            }
        }

        private static void SetWindowsZPosition(Process OtherProcess)
        {
            try
            {
                if (Helper.GetUWPWindowInformation(Package.Current.Id.FamilyName, (uint?)ExplorerProcess?.Id) is WindowInformation Info && !Info.Handle.IsNull)
                {
                    User32.SetWindowPos(Info.Handle, new IntPtr(1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOACTIVATE);
                }

                bool IsTimeout = false;

                try
                {
                    IsTimeout = OtherProcess.WaitForInputIdle(5000);
                }
                catch (InvalidOperationException ex)
                {
                    LogTracer.Log(ex, "Error: WaitForInputIdle threw an exception.");
                }

                if (IsTimeout)
                {
                    LogTracer.Log("Error: Could not switch to window because WaitForInputIdle is timeout after 5000ms");
                }
                else
                {
                    for (int i = 0; i < 10 && !OtherProcess.MainWindowHandle.CheckIfValidPtr(); i++)
                    {
                        Thread.Sleep(500);
                        OtherProcess.Refresh();
                    }

                    if (OtherProcess.MainWindowHandle.CheckIfValidPtr())
                    {
                        User32.ShowWindow(OtherProcess.MainWindowHandle, ShowWindowCommand.SW_SHOWNORMAL);
                        User32.SetWindowPos(OtherProcess.MainWindowHandle, new IntPtr(0), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                        User32.SetForegroundWindow(OtherProcess.MainWindowHandle);
                    }
                    else
                    {
                        LogTracer.Log("Error: Could not switch to window because MainWindowHandle is always invalid");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error: {nameof(SetWindowsZPosition)} threw an error, message: {ex.Message}");
            }
        }

        private static void AliveCheck(object state)
        {
            try
            {
                if ((ExplorerProcess?.HasExited).GetValueOrDefault())
                {
                    ExitLocker.Set();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(AliveCheck)} threw an exception, message: {ex.Message}");
            }
        }
    }
}
