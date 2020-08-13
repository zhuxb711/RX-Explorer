using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<HardDeviceInfo> HardDeviceList { get; private set; } = new ObservableCollection<HardDeviceInfo>();
        public static ObservableCollection<LibraryFolder> LibraryFolderList { get; private set; } = new ObservableCollection<LibraryFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; private set; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> HotWebList { get; private set; } = new ObservableCollection<QuickStartItem>();

        private static Dictionary<FileControl, FilePresenter> FFInstanceContainer = new Dictionary<FileControl, FilePresenter>();

        private static Dictionary<FileControl, SearchPage> FSInstanceContainer = new Dictionary<FileControl, SearchPage>();

        private static Dictionary<ThisPC, FileControl> TFInstanceContainer = new Dictionary<ThisPC, FileControl>();

        public static void Register(FileControl Input1, FilePresenter Input2)
        {
            if (FFInstanceContainer.ContainsKey(Input1))
            {
                FFInstanceContainer[Input1] = Input2;
            }
            else
            {
                FFInstanceContainer.Add(Input1, Input2);
            }
        }

        public static void Register(ThisPC Input1, FileControl Input2)
        {
            if (TFInstanceContainer.ContainsKey(Input1))
            {
                TFInstanceContainer[Input1] = Input2;
            }
            else
            {
                TFInstanceContainer.Add(Input1, Input2);
            }
        }

        public static void Register(FileControl Input1, SearchPage Input2)
        {
            if (FSInstanceContainer.ContainsKey(Input1))
            {
                FSInstanceContainer[Input1] = Input2;
            }
            else
            {
                FSInstanceContainer.Add(Input1, Input2);
            }
        }

        public static void UnRegister(FileControl Input)
        {
            if (GetThisPCInstance(Input) is ThisPC PCInstance)
            {
                TFInstanceContainer.Remove(PCInstance);
            }

            if (FFInstanceContainer.ContainsKey(Input))
            {
                FFInstanceContainer.Remove(Input);
            }

            if (FSInstanceContainer.ContainsKey(Input))
            {
                FSInstanceContainer.Remove(Input);
            }
        }

        public static void UnRegister(FilePresenter Input)
        {
            if (GetFileControlInstance(Input) is FileControl ControlInstance)
            {
                if(GetThisPCInstance(ControlInstance) is ThisPC PCInstance)
                {
                    TFInstanceContainer.Remove(PCInstance);
                }

                if (FFInstanceContainer.ContainsKey(ControlInstance))
                {
                    FFInstanceContainer.Remove(ControlInstance);
                }

                if (FSInstanceContainer.ContainsKey(ControlInstance))
                {
                    FSInstanceContainer.Remove(ControlInstance);
                }
            }
        }

        public static void UnRegister(ThisPC Input)
        {
            if (TFInstanceContainer.ContainsKey(Input))
            {
                FileControl Instance = TFInstanceContainer[Input];

                if (FFInstanceContainer.ContainsKey(Instance))
                {
                    FFInstanceContainer.Remove(Instance);
                }

                if (FSInstanceContainer.ContainsKey(Instance))
                {
                    FSInstanceContainer.Remove(Instance);
                }

                TFInstanceContainer.Remove(Input);
            }
        }

        public static ThisPC GetThisPCInstance(FileControl Input)
        {
            if (TFInstanceContainer.FirstOrDefault((KV) => KV.Value == Input) is KeyValuePair<ThisPC, FileControl> KV)
            {
                return KV.Key;
            }
            else
            {
                return null;
            }
        }

        public static FilePresenter GetFilePresenterInstance(FileControl Input)
        {
            if (FFInstanceContainer.ContainsKey(Input))
            {
                return FFInstanceContainer[Input];
            }
            else
            {
                return null;
            }
        }

        public static FilePresenter GetFilePresenterInstance(ThisPC Input)
        {
            if (TFInstanceContainer.ContainsKey(Input))
            {
                FileControl Instance = TFInstanceContainer[Input];

                if (FFInstanceContainer.ContainsKey(Instance))
                {
                    return FFInstanceContainer[Instance];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static SearchPage GetSearchPageInstance(FileControl Input)
        {
            if (FSInstanceContainer.ContainsKey(Input))
            {
                return FSInstanceContainer[Input];
            }
            else
            {
                return null;
            }
        }

        public static FileControl GetFileControlInstance(ThisPC Input)
        {
            if (TFInstanceContainer.ContainsKey(Input))
            {
                return TFInstanceContainer[Input];
            }
            else
            {
                return null;
            }
        }

        public static FileControl GetFileControlInstance(FilePresenter Input)
        {
            if (FFInstanceContainer.FirstOrDefault((KV) => KV.Value == Input) is KeyValuePair<FileControl, FilePresenter> KV)
            {
                return KV.Key;
            }
            else
            {
                return null;
            }
        }
    }
}
