using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace RX_Explorer.Class
{
    public sealed class FTPPathAnalysis
    {
        public string Host { get; }

        public int Port { get; }

        public string Path { get; }

        public string RelatedPath { get; }

        public bool IsRootDirectory => RelatedPath == "\\";

        public string UserName { get; }

        public string Password { get; }

        public FTPPathAnalysis(string Path)
        {
            if (Regex.IsMatch(Path, @"^ftp(s)?:\\.+", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(Path, @"^ftp(s)?:\\[^\\].+", RegexOptions.IgnoreCase))
                {
                    if (Path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase))
                    {
                        Path = Path.Insert(5, @"\");
                    }
                    else if (Path.StartsWith("ftps:", StringComparison.OrdinalIgnoreCase))
                    {
                        Path = Path.Insert(6, @"\");
                    }
                }

                this.Path = Path;

                string[] SplitString = Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries);

                Match SimpleMat = Regex.Match(SplitString[1], @"^(?<Host>([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:(?<Port>[0-9]+))?$");

                if (SimpleMat.Success)
                {
                    Host = SimpleMat.Groups["Host"].Value;
                    Port = int.Parse(SimpleMat.Groups["Port"].Success ? SimpleMat.Groups["Port"].Value : (Path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase) ? "21" : "990"));
                    RelatedPath = @$"\{string.Join(@"\", SplitString.Skip(2))}";
                    return;
                }
                else
                {
                    Match ComplexMat = Regex.Match(SplitString[1], @"(?<UserName>(.+)):(?<Password>(.+))@(?<Host>([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}))(:(?<Port>[0-9]+))?$");

                    if (ComplexMat.Success)
                    {
                        Host = ComplexMat.Groups["Host"].Value;
                        Port = int.Parse(ComplexMat.Groups["Port"].Success ? ComplexMat.Groups["Port"].Value : (Path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase) ? "21" : "990"));
                        UserName = ComplexMat.Groups["UserName"].Value;
                        Password = ComplexMat.Groups["Password"].Value;
                        RelatedPath = @$"\{string.Join(@"\", SplitString.Skip(2))}";
                        return;
                    }
                }
            }

            throw new NotSupportedException(Path);
        }
    }
}
