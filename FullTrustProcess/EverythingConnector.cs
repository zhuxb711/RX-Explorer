using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public class EverythingConnector : IDisposable
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
        private static extern void Everything_CleanUp();

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

        public bool CheckIfAvailable
        {
            get
            {
                return Everything_IsDBLoaded();
            }
        }

        public StateCode GetLastErrorCode()
        {
            return Everything_GetLastError();
        }

		public IEnumerable<string> Search(string SearchKeyWord, bool SearchAsRegex = false, int MaxCount = 500)
		{
			if (string.IsNullOrWhiteSpace(SearchKeyWord) || !CheckIfAvailable)
			{
				yield break;
			}
            else
            {
                if (MaxCount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxCount));
                }

                Everything_SetRegex(SearchAsRegex);
                Everything_SetSearch(SearchKeyWord);
                Everything_SetOffset(0);
                Everything_SetMax(MaxCount);

                if (Everything_Query())
                {
                    StringBuilder Builder = new StringBuilder(BufferSize);

                    for (int index = 0; index < Everything_GetNumResults(); ++index)
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

        public void Dispose()
        {
            Everything_CleanUp();
        }
    }
}
