using System;
using System.Collections.Generic;
using System.Text.Json;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class LaunchModeController
    {
        private static readonly object Locker = new object();

        public static LaunchWithTabMode GetLaunchMode()
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["LaunchWithTabMode"] is string Mode)
                {
                    return Enum.Parse<LaunchWithTabMode>(Mode);
                }
                else
                {
                    return LaunchWithTabMode.CreateNewTab;
                }
            }
        }

        public static void SetLaunchMode(LaunchWithTabMode Mode)
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["LaunchWithTabMode"] is string ExistMode)
                {
                    if (Enum.Parse<LaunchWithTabMode>(ExistMode) != Mode)
                    {
                        ApplicationData.Current.LocalSettings.Values["LaunchWithTabMode"] = Enum.GetName(typeof(LaunchWithTabMode), Mode);
                        ApplicationData.Current.SignalDataChanged();
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LaunchWithTabMode"] = Enum.GetName(typeof(LaunchWithTabMode), Mode);
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        public static async IAsyncEnumerable<string[]> GetAllPathAsync(LaunchWithTabMode Mode)
        {
            if (Mode == LaunchWithTabMode.CreateNewTab)
            {
                yield break;
            }

            switch (Mode)
            {
                case LaunchWithTabMode.CreateNewTab:
                    {
                        yield break;
                    }
                case LaunchWithTabMode.SpecificTab:
                    {
                        if (ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] is string RawData)
                        {
                            if (string.IsNullOrWhiteSpace(RawData))
                            {
                                yield break;
                            }

                            foreach (string Path in JsonSerializer.Deserialize<List<string>>(RawData))
                            {
                                if (await FileSystemStorageItemBase.CheckExist(Path).ConfigureAwait(false))
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
                case LaunchWithTabMode.LastOpenedTab:
                    {
                        if (ApplicationData.Current.LocalSettings.Values["LaunchWithLastOpenedPath"] is string RawData)
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
                                    if (await FileSystemStorageItemBase.CheckExist(ValidPath).ConfigureAwait(false))
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
                if (ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] is string RawData && !string.IsNullOrWhiteSpace(RawData))
                {
                    List<string> PathList = JsonSerializer.Deserialize<List<string>>(RawData);

                    PathList.Add(InputPath);

                    ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                }
                else
                {
                    List<string> PathList = new List<string>(1)
                    {
                        InputPath
                    };

                    ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                }

                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static void RemoveSpecificPath(string Path)
        {
            lock (Locker)
            {
                if (ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] is string RawData && !string.IsNullOrWhiteSpace(RawData))
                {
                    List<string> PathList = JsonSerializer.Deserialize<List<string>>(RawData);

                    if (PathList.Remove(Path))
                    {
                        if (PathList.Count > 0)
                        {
                            ApplicationData.Current.LocalSettings.Values["LaunchWithSpecificPath"] = JsonSerializer.Serialize(PathList);
                        }
                        else
                        {
                            ApplicationData.Current.LocalSettings.Values.Remove("LaunchWithSpecificPath");
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
                ApplicationData.Current.LocalSettings.Values["LaunchWithLastOpenedPath"] = JsonSerializer.Serialize(InputPath);

                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static void Clear(LaunchWithTabMode Mode)
        {
            if (Mode == LaunchWithTabMode.CreateNewTab)
            {
                throw new ArgumentException($"Mode could not be {nameof(LaunchWithTabMode.CreateNewTab)}", nameof(Mode));
            }

            lock (Locker)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(Mode == LaunchWithTabMode.SpecificTab ? "LaunchWithSpecificPath" : "LaunchWithLastOpenedPath");
            }
        }
    }
}
