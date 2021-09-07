using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class StartupModeController
    {
        private static readonly object Locker = new object();

        public static StartupMode Mode
        {
            get
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
            set
            {
                lock (Locker)
                {
                    if (ApplicationData.Current.LocalSettings.Values["StartupMode"] is string ExistMode)
                    {
                        if (Enum.Parse<StartupMode>(ExistMode) != value)
                        {
                            ApplicationData.Current.LocalSettings.Values["StartupMode"] = Enum.GetName(typeof(StartupMode), value);
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["StartupMode"] = Enum.GetName(typeof(StartupMode), value);
                    }
                }
            }
        }

        public static async IAsyncEnumerable<string[]> GetAllPathAsync()
        {
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

                            foreach (string Path in JsonSerializer.Deserialize<IEnumerable<string>>(RawData).Where((Path) => !string.IsNullOrWhiteSpace(Path)))
                            {
                                if (await FileSystemStorageItemBase.CheckExistAsync(Path))
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

                            foreach (string[] PathList in JsonSerializer.Deserialize<IEnumerable<string[]>>(RawData).Where((Path) => Path.Length > 0))
                            {
                                List<string> ValidPathList = new List<string>(PathList.Length);

                                foreach (string ValidPath in PathList.Where((Path) => !string.IsNullOrWhiteSpace(Path)))
                                {
                                    if (await FileSystemStorageItemBase.CheckExistAsync(ValidPath))
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

        public static void SetSpecificPath(IEnumerable<string> InputPath)
        {
            if (Mode != StartupMode.SpecificTab)
            {
                throw new ArgumentException($"Mode must be {nameof(StartupMode.SpecificTab)}", nameof(Mode));
            }

            lock (Locker)
            {
                ApplicationData.Current.LocalSettings.Values["StartupWithSpecificPath"] = JsonSerializer.Serialize(InputPath);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static void SetLastOpenedPath(IEnumerable<string[]> InputPath)
        {
            if (Mode != StartupMode.LastOpenedTab)
            {
                throw new ArgumentException($"Mode must be {nameof(StartupMode.LastOpenedTab)}", nameof(Mode));
            }

            lock (Locker)
            {
                ApplicationData.Current.LocalSettings.Values["StartupWithLastOpenedPath"] = JsonSerializer.Serialize(InputPath);
                ApplicationData.Current.SignalDataChanged();
            }
        }
    }
}
