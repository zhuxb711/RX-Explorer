using System;
using System.Text.RegularExpressions;

namespace RX_Explorer.Class
{
    public sealed class FtpPathAnalysis
    {
        public string Host { get; }

        public int Port { get; }

        public string Path { get; }

        public string RelatedPath { get; }

        public bool IsRootDirectory => RelatedPath == "\\";

        public string UserName { get; }

        public string Password { get; }

        public FtpPathAnalysis(string Path)
        {
            this.Path = Regex.Replace(Path, @"\\+", @"\");

            Match FtpRegexMat = Regex.Match(this.Path, @"^ftps?:\\((?<UserName>\S+):(?<Password>\S+)@)?((?<Host>[\w.-]+(?:\.[\w\.-]+)+)(:(?<Port>[1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5]))?(?<RelatedPath>\\.*)?)$", RegexOptions.IgnoreCase);

            if (FtpRegexMat.Success)
            {
                Host = FtpRegexMat.Groups["Host"].Value;

                if (FtpRegexMat.Groups["UserName"].Success)
                {
                    UserName = FtpRegexMat.Groups["UserName"].Value;
                }

                if (FtpRegexMat.Groups["Password"].Success)
                {
                    Password = FtpRegexMat.Groups["Password"].Value;
                }

                if (FtpRegexMat.Groups["Port"].Success)
                {
                    Port = int.Parse(FtpRegexMat.Groups["Port"].Value);
                }
                else
                {
                    Port = int.Parse(this.Path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase) ? "21" : "990");
                }

                if (FtpRegexMat.Groups["RelatedPath"].Success)
                {
                    RelatedPath = FtpRegexMat.Groups["RelatedPath"].Value;
                }
                else
                {
                    RelatedPath = @"\";
                }
            }
            else
            {
                throw new NotSupportedException(Path);
            }
        }
    }
}
