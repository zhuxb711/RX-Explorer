using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public class EverythingConnector
    {
        private static EverythingConnector Instance;

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

		public static EverythingConnector Current
        {
            get
            {
                return Instance ??= new EverythingConnector();
            }
        }

		public IEnumerable<string> Search(string SearchKeyWord, bool SearchAsRegex = false, int Offset = 0, int MaxCount = 100)
		{
			if (string.IsNullOrWhiteSpace(SearchKeyWord) || !CheckIfAvailable)
			{
				yield break;
			}
            else
            {
                if (Offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Offset));
                }

                if (MaxCount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxCount));
                }

                Everything_SetRegex(SearchAsRegex);
                Everything_SetSearch(SearchKeyWord);
                Everything_SetOffset(Offset);
                Everything_SetMax(MaxCount);

                if (Everything_Query())
                {
                    StringBuilder Builder = new StringBuilder(BufferSize);

                    for (int index = 0; index < Everything_GetNumResults(); ++index)
                    {
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

		private EverythingConnector()
        {

        }
    }
}
