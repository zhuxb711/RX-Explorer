using System;

namespace ShareClassLibrary
{
    public static class Extention
    {
        public static bool CheckIfValidPtr(this IntPtr Ptr)
        {
            return Ptr != IntPtr.Zero && Ptr.ToInt64() != -1;
        }
    }
}
