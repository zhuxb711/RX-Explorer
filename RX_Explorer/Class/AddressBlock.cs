using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RX_Explorer.Class
{
    public sealed class AddressBlock : INotifyPropertyChanged
    {
        public string Path { get; }

        private AddressBlockType blockType;
        public AddressBlockType BlockType
        {
            get
            {
                return blockType;
            }
            set
            {
                blockType = value;
                OnPropertyChanged();
            }
        }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(InnerDisplayName))
                {
                    string FileName = System.IO.Path.GetFileName(Path);

                    if (string.IsNullOrEmpty(FileName))
                    {
                        return Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    }
                    else
                    {
                        return FileName;
                    }
                }
                else
                {
                    return InnerDisplayName;
                }
            }
        }

        private readonly string InnerDisplayName;

        public event PropertyChangedEventHandler PropertyChanged;

        public AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;
            InnerDisplayName = DisplayName;
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
