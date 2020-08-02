using System.Runtime.InteropServices;
using System.Text;

namespace FullTrustProcess
{
    public static class ExtensionAssociate
    {
        [DllImport("shell32.dll", EntryPoint = "FindExecutable")]
        private static extern long FindExecutable(string lpFile, string lpDirectory, StringBuilder lpResult);

        public static string GetAssociate(string Path)
        {
            StringBuilder executable = new StringBuilder();
            
            if (FindExecutable(Path, string.Empty, executable) >= 32)
            {
                return executable.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
