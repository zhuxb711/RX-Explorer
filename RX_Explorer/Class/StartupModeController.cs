using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class StartupModeController
    {
        private static readonly object Locker = new object();

        public static StartupMode GetStartupMode()
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["StartupMode"] is string Mode)
                {
                    return Enum.Parse<StartupMode>(Mode);
                }
                else
                {
                    return StartupMode.CreateNewTab;
                }
            }
        }

        public static void SetLaunchMode(StartupMode Mode)
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["StartupMode"] is string ExistMode)
                {
                    if (Enum.Parse<StartupMode>(ExistMode) != Mode)
                    {
                        ApplicationData.Current.LocalSettings.Values["StartupMode"] = Enum.GetName(typeof(StartupMode), Mode);
                        ApplicationData.Current.SignalDataChanged();
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["StartupMode"] = Enum.GetName(typeof(StartupMode), Mode);
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        public static async IAsyncEnumerable<string[]> GetAllPathAsync(StartupMode Mode)
        {
            if (Mode == StartupMode.CreateNewTab)
            {
                yield break;
            }

            switch (Mode)
            {
                case StartupMode.CreateNewTab:
                    {
                        yield break;
                    }
                case StartupMode.SpecificTab:
                    {
                        if (ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] is string RawData)
                        {
                            if (string.IsNullOrWhiteSpace(RawData))
                            {
                                yield break;
                            }

                            foreach (string Path in JsonSerializer.Deserialize<List<string>>(RawData))
                            {
                                if (await FileSystemStorageItemBase.CheckExistAsync(Path).ConfigureAwait(false))
                                {
                                    yield return new string[] { Path };
                                }
                            }
                        }
                        else
                        {
                            yield break;
                        }

                        break;
                    }
                case StartupMode.LastOpenedTab:
                    {
                        if (ApplicationData.Current.LocalSettings.Values["StartupWithLastOpenedPath"] is string RawData)
                        {
                            if (string.IsNullOrWhiteSpace(RawData))
                            {
                                yield break;
                            }

                            foreach (string[] PathList in JsonSerializer.Deserialize<List<string[]>>(RawData))
                            {
                                List<string> ValidPathList = new List<string>(PathList.Length);

                                foreach (string ValidPath in PathList)
                                {
                                    if (await FileSystemStorageItemBase.CheckExistAsync(ValidPath).ConfigureAwait(false))
                                    {
                                        ValidPathList.Add(ValidPath);
                                    }
                                }

                                yield return ValidPathList.ToArray();
                            }
                        }
                        else
                        {
                            yield break;
                        }

                        break;
                    }
            }
        }

        public static void AddSpecificPath(string InputPath)
        {
            if (string.IsNullOrWhiteSpace(InputPath))
            {
                return;
            }

            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] is string RawData && !string.IsNullOrWhiteSpace(RawData))
                {
                    List<string> PathList = JsonSerializer.Deserialize<List<string>>(RawData);

                    PathList.Add(InputPath);

                    ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                }
                else
                {
                    List<string> PathList = new List<string>(1)
                    {
                        InputPath
                    };

                    ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                }

                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static void RemoveSpecificPath(string Path)
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] is string RawData && !string.IsNullOrWhiteSpace(RawData))
                {
                    List<string> PathList = JsonSerializer.Deserialize<List<string>>(RawData);

                    if (PathList.Remove(Path))
                    {
                        if (PathList.Count > 0)
                        {
                            ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values.Remove("StartupWithSpecificPath");
                        }

                        ApplicationData.Current.SignalDataChanged();
                    }
                }
            }
        }

        public static void SetLastOpenedPath(List<string[]> InputPath)
        {
            if (InputPath.Count == 0)
            {
                return;
            }

            lock (Locker)
            {
                ApplicationData.Current.LocalSettings.Values["StartupWithLastOpenedPath"] = JsonSerializer.Serialize(InputPath);

                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static void Clear(StartupMode Mode)
        {
            if (Mode == StartupMode.CreateNewTab)
            {
                throw new ArgumentException($"Mode could not be {nameof(StartupMode.CreateNewTab)}", nameof(Mode));
            }

            lock (Locker)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(Mode == StartupMode.SpecificTab ? "StartupWithSpecificPath" : "StartupWithLastOpenedPath");
            }
        }
    }
}
