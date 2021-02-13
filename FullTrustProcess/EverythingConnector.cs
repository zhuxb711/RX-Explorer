using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public class EverythingConnector
    {
        private const int BufferSize = 512;

        [DllImport("Everything32.dll", EntryPoint = "Everything_SetSearch", CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearch32(string lpSearchString);

        [DllImport("Everything64.dll", EntryPoint = "Everything_SetSearch", CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearch64(string lpSearchString);


        [DllImport("Everything32.dll", EntryPoint = "Everything_SetOffset", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetOffset32(int dwOffset);

        [DllImport("Everything64.dll", EntryPoint = "Everything_SetOffset", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetOffset64(int dwOffset);


        [DllImport("Everything32.dll", EntryPoint = "Everything_SetMax", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetMax32(int dwMax);

        [DllImport("Everything64.dll", EntryPoint = "Everything_SetMax", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetMax64(int dwMax);


        [DllImport("Everything32.dll", EntryPoint = "Everything_Query", CharSet = CharSet.Unicode)]
        private static extern bool Everything_Query32(bool bWait);

        [DllImport("Everything64.dll", EntryPoint = "Everything_Query", CharSet = CharSet.Unicode)]
        private static extern bool Everything_Query64(bool bWait);


        [DllImport("Everything32.dll", EntryPoint = "Everything_GetNumResults", CharSet = CharSet.Unicode)]
        private static extern int Everything_GetNumResults32();

        [DllImport("Everything64.dll", EntryPoint = "Everything_GetNumResults", CharSet = CharSet.Unicode)]
        private static extern int Everything_GetNumResults64();


        [DllImport("Everything32.dll", EntryPoint = "Everything_GetLastError", CharSet = CharSet.Unicode)]
        private static extern StateCode Everything_GetLastError32();

        [DllImport("Everything64.dll", EntryPoint = "Everything_GetLastError", CharSet = CharSet.Unicode)]
        private static extern StateCode Everything_GetLastError64();


        [DllImport("Everything32.dll", EntryPoint = "Everything_GetResultFullPathName", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathName32(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport("Everything64.dll", EntryPoint = "Everything_GetResultFullPathName", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathName64(int nIndex, StringBuilder lpString, int nMaxCount);


        [DllImport("Everything32.dll", EntryPoint = "Everything_IsDBLoaded", CharSet = CharSet.Unicode)]
        private static extern bool Everything_IsDBLoaded32();

        [DllImport("Everything64.dll", EntryPoint = "Everything_IsDBLoaded", CharSet = CharSet.Unicode)]
        private static extern bool Everything_IsDBLoaded64();


        [DllImport("Everything32.dll", EntryPoint = "Everything_SetRegex", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetRegex32(bool isEnabled);

        [DllImport("Everything64.dll", EntryPoint = "Everything_SetRegex", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetRegex64(bool isEnabled);


        [DllImport("Everything32.dll", EntryPoint = "Everything_SetMatchCase", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetMatchCase32(bool isCaseSensitive);

        [DllImport("Everything64.dll", EntryPoint = "Everything_SetMatchCase", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetMatchCase64(bool isCaseSensitive);


        private static bool Everything_IsDBLoaded()
        {
            if (Environment.Is64BitProcess)
            {
                return Everything_IsDBLoaded64();
            }
            else
            {
                return Everything_IsDBLoaded32();
            }
        }

        private static StateCode Everything_GetLastError()
        {
            if (Environment.Is64BitProcess)
            {
                return Everything_GetLastError64();
            }
            else
            {
                return Everything_GetLastError32();
            }
        }

        private static void Everything_SetRegex(bool IsEnabled)
        {
            if (Environment.Is64BitProcess)
            {
                Everything_SetRegex64(IsEnabled);
            }
            else
            {
                Everything_SetRegex32(IsEnabled);
            }
        }

        private static void Everything_SetMatchCase(bool IsCaseSensitive)
        {
            if (Environment.Is64BitProcess)
            {
                Everything_SetMatchCase64(IsCaseSensitive);
            }
            else
            {
                Everything_SetMatchCase32(IsCaseSensitive);
            }
        }

        private static void Everything_SetOffset(int Offset)
        {
            if (Environment.Is64BitProcess)
            {
                Everything_SetOffset64(Offset);
            }
            else
            {
                Everything_SetOffset32(Offset);
            }
        }

        private static void Everything_SetMax(int Max)
        {
            if (Environment.Is64BitProcess)
            {
                Everything_SetMax64(Max);
            }
            else
            {
                Everything_SetMax32(Max);
            }
        }

        private static int Everything_SetSearch(string SearchString)
        {
            if (Environment.Is64BitProcess)
            {
                return Everything_SetSearch64(SearchString);
            }
            else
            {
                return Everything_SetSearch32(SearchString);
            }
        }

        private static bool Everything_Query(bool Wait)
        {
            if (Environment.Is64BitProcess)
            {
                return Everything_Query64(Wait);
            }
            else
            {
                return Everything_Query32(Wait);
            }
        }

        private static int Everything_GetNumResults()
        {
            if (Environment.Is64BitProcess)
            {
                return Everything_GetNumResults64();
            }
            else
            {
                return Everything_GetNumResults32();
            }
        }

        private static void Everything_GetResultFullPathName(int Index, StringBuilder Builder, int MaxCount)
        {
            if (Environment.Is64BitProcess)
            {
                Everything_GetResultFullPathName64(Index, Builder, MaxCount);
            }
            else
            {
                Everything_GetResultFullPathName32(Index, Builder, MaxCount);
            }
        }

        public enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError
        }

        public bool IsAvailable
        {
            get
            {
                return Everything_IsDBLoaded();
            }
        }

        private static EverythingConnector Instance;
        public static EverythingConnector Current
        {
            get
            {
                return Instance ??= new EverythingConnector();
            }
        }

        private EverythingConnector()
        {

        }

        public StateCode GetLastErrorCode()
        {
            return Everything_GetLastError();
        }

        public IEnumerable<string> Search(string BaseLocation, string SearchWord, bool SearchAsRegex = false, bool IgnoreCase = true, uint MaxCount = 500)
        {
            if (string.IsNullOrWhiteSpace(SearchWord) || !IsAvailable)
            {
                yield break;
            }
            else
            {
                Everything_SetRegex(SearchAsRegex);
                Everything_SetMatchCase(!IgnoreCase);
                Everything_SetOffset(0);
                Everything_SetMax(Convert.ToInt32(MaxCount));

                if (string.IsNullOrEmpty(BaseLocation))
                {
                    Everything_SetSearch(SearchWord);
                }
                else
                {
                    Everything_SetSearch($"{(BaseLocation.EndsWith("\\") ? BaseLocation : BaseLocation + "\\")} {SearchWord}");
                }

                if (Everything_Query(true))
                {
                    StringBuilder Builder = new StringBuilder(BufferSize);

                    for (int index = 0; index < Everything_GetNumResults(); index++)
                    {
                        Builder.Clear();
                        Everything_GetResultFullPathName(index, Builder, BufferSize);
                        yield return Builder.ToString();
                    }
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}
