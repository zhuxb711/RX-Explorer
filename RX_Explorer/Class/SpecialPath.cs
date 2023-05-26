using Newtonsoft.Json;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class SpecialPath
    {
        private static IReadOnlyList<string> OneDrivePathCollection { get; } = new List<string>(3)
        {
            Environment.GetEnvironmentVariable("OneDriveConsumer"),
            Environment.GetEnvironmentVariable("OneDriveCommercial"),
            Environment.GetEnvironmentVariable("OneDrive")
        };

        private static IReadOnlyList<string> DropboxPathCollection { get; set; } = new List<string>(0);

        public static async Task InitializeAsync()
        {
            static async Task<IReadOnlyList<string>> LocalLoadJsonAsync(string JsonPath)
            {
                List<string> DropboxPathResult = new List<string>(2);

                try
                {
                    if (await FileSystemStorageItemBase.OpenAsync(JsonPath) is FileSystemStorageFile JsonFile)
                    {
                        using (Stream Stream = await JsonFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                        using (StreamReader Reader = new StreamReader(Stream, true))
                        {
                            var JsonObject = JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, object>>>(Reader.ReadToEnd());

                            if (JsonObject.TryGetValue("personal", out IDictionary<string, object> PersonalSubDic))
                            {
                                DropboxPathResult.Add(Convert.ToString(PersonalSubDic["path"]));
                            }

                            if (JsonObject.TryGetValue("business", out IDictionary<string, object> BusinessSubDic))
                            {
                                DropboxPathResult.Add(Convert.ToString(BusinessSubDic["path"]));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the configuration from Dropbox info.json");
                }

                return DropboxPathResult;
            }

            string JsonPath1 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%APPDATA%\Dropbox\info.json");
            string JsonPath2 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%LOCALAPPDATA%\Dropbox\info.json");

            if (await FileSystemStorageItemBase.CheckExistsAsync(JsonPath1))
            {
                DropboxPathCollection = await LocalLoadJsonAsync(JsonPath1);
            }
            else if (await FileSystemStorageItemBase.CheckExistsAsync(JsonPath2))
            {
                DropboxPathCollection = await LocalLoadJsonAsync(JsonPath2);
            }

            if (DropboxPathCollection.Count == 0)
            {
                DropboxPathCollection = new List<string>(1)
                    {
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Dropbox")
                    };
            }
        }

        public static bool IsPathIncluded(string Path, SpecialPathEnum Enum)
        {
            switch (Enum)
            {
                case SpecialPathEnum.OneDrive:
                    {
                        return OneDrivePathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((OneDrivePath) => Path.StartsWith(OneDrivePath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(OneDrivePath, StringComparison.OrdinalIgnoreCase));
                    }
                case SpecialPathEnum.Dropbox:
                    {
                        return DropboxPathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((DropboxPath) => Path.StartsWith(DropboxPath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(DropboxPath, StringComparison.OrdinalIgnoreCase));
                    }
                default:
                    {
                        return false;
                    }
            }
        }
    }

}
