using System;

namespace RX_Explorer.Class
{
    public class TerminalProfile : IEquatable<TerminalProfile>
    {
        public string Name { get; set; }

        public string Argument { get; set; }

        public string Path { get; set; }

        public bool RunAsAdmin { get; set; }

        public TerminalProfile(string Name, string Path, string Argument, bool RunAsAdmin)
        {
            this.Name = Name;
            this.Path = Path;
            this.Argument = Argument;
            this.RunAsAdmin = RunAsAdmin;
        }

        public bool Equals(TerminalProfile other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    return other.Name == Name;
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is TerminalProfile Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return $"ProfileName: {Name}, Path: {Path}, RunAsAdmin: {RunAsAdmin}";
        }

        public static bool operator ==(TerminalProfile left, TerminalProfile right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TerminalProfile left, TerminalProfile right)
        {
            return !left.Equals(right);
        }
    }
}
