using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public class EverythingConnector
    {
        private const int BufferSize = 256;

        [DllImport("Everything32.dll")]
        private static extern int Everything_SetSearch(string lpSearchString);

        [DllImport("Everything32.dll")]
        private static extern void Everything_SetOffset(int dwOffset);

        [DllImport("Everything32.dll")]
        private static extern string Everything_GetSearch();

        [DllImport("Everything32.dll")]
        private static extern void Everything_SetMax(int dwMax);

        [DllImport("Everything32.dll")]
        private static extern bool Everything_Query();

        [DllImport("Everything32.dll")]
        private static extern int Everything_GetNumResults();

        [DllImport("Everything32.dll")]
        private static extern StateCode Everything_GetLastError();

        [DllImport("Everything32.dll")]
        private static extern void Everything_GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport("Everything32.dll")]
        private static extern bool Everything_IsDBLoaded();

        [DllImport("Everything32.dll")]
        private static extern void Everything_SetRegex(bool isEnabled);

        [DllImport("Everything32.dll")]
        private static extern void Everything_SetMatchCase(bool isCaseSensitive);

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
                    Everything_SetSearch(BaseLocation.EndsWith("\\") ? $"\"{BaseLocation}\" {SearchWord}" : $"\"{BaseLocation}\\\" {SearchWord}");
                }

                if (Everything_Query())
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
