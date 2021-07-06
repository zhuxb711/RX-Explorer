using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public class EverythingConnector
    {
        private const int BufferSize = 512;

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearch(string lpSearchString);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetOffset(int dwOffset);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetMax(int dwMax);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern bool Everything_Query(bool bWait);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern int Everything_GetNumResults();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern StateCode Everything_GetLastError();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern bool Everything_IsDBLoaded();

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
        private static extern void Everything_SetRegex(bool isEnabled);

        [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
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

        public static bool IsAvailable
        {
            get
            {
                return Everything_IsDBLoaded();
            }
        }

        public static StateCode GetLastErrorCode()
        {
            return Everything_GetLastError();
        }

        public static IEnumerable<string> Search(string BaseLocation, string SearchWord, bool SearchAsRegex = false, bool IgnoreCase = true, uint MaxCount = 500)
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

                if (SearchWord.Contains(" ") && !SearchWord.StartsWith("\"") && !SearchWord.EndsWith("\""))
                {
                    SearchWord = $"\"{SearchWord}\"";
                }

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
                    for (int index = 0; index < Everything_GetNumResults(); index++)
                    {
                        StringBuilder Builder = new StringBuilder(BufferSize);
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
