using RX_Explorer.Class;
using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureFilePropertyDialog : QueueContentDialog
    {
        public static async Task<SecureFilePropertyDialog> CreateAsync(FileSystemStorageFile SFile)
        {
            if (SFile == null)
            {
                throw new ArgumentNullException(nameof(SFile), "Parameter could not be null");
            }

            if (!SFile.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("File must be end with .sle", nameof(SFile));
            }

            using (Stream FileStream = await SFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
            {
                SLEHeader Header = SLEHeader.GetHeader(FileStream);

                return new SecureFilePropertyDialog(SFile.DisplayName,
                                                    SFile.DisplayType, SFile.Size.GetFileSizeDescription(),
                                                    string.Join('.', Convert.ToString((int)Header.Core.Version).ToCharArray()),
                                                    Header.Core.KeySize switch
                                                    {
                                                        128 => "AES-128bit",
                                                        256 => "AES-256bit",
                                                        _ => throw new NotSupportedException()
                                                    });
            }
        }

        private SecureFilePropertyDialog(string DisplayName, string DisplayType, string SizeDescription, string Version, string Level)
        {
            InitializeComponent();
            FileNameLabel.Text = DisplayName;
            FileTypeLabel.Text = DisplayType;
            FileSizeLabel.Text = SizeDescription;
            VersionLabel.Text = Version;
            LevelLabel.Text = Level;
        }
    }
}
