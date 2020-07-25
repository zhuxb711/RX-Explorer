using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RX_Explorer.Class
{
    public sealed class FileSystemItemNameChecker
    {
        private static readonly HashSet<string> NotAllowNames = new HashSet<string>
        {
            "CON","PRN", "AUX", "CLOCK$", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static bool IsValid(string Input)
        {
            char[] InvalidChar = Path.GetInvalidFileNameChars();

            if (Input.Any((Char) => InvalidChar.Contains(Char)) || NotAllowNames.Any((Name) => Input.Contains(Name)))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
