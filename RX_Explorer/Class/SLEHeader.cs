using System;
using System.IO;
using System.Linq;
using System.Text;

namespace RX_Explorer.Class
{
    public sealed class SLEHeader
    {
        public SLEVersion Version { get; }

        public int KeySize { get; }

        public string FileName { get; }

        public int HeaderLength { get; set; }

        public static SLEHeader GetHeader(Stream BaseFileStream)
        {
            long OriginPosition = BaseFileStream.Position;

            try
            {
                StringBuilder Builder = new StringBuilder();

                using (StreamReader Reader = new StreamReader(BaseFileStream, Encoding.UTF8, true, 256, true))
                {
                    for (int Count = 0; Reader.Peek() >= 0; Count++)
                    {
                        if (Count > 256)
                        {
                            throw new FileDamagedException("File damaged, could not be decrypted");
                        }

                        char NextChar = (char)Reader.Read();

                        if (Builder.Length > 0 && NextChar == '$')
                        {
                            Builder.Append(NextChar);
                            break;
                        }
                        else
                        {
                            Builder.Append(NextChar);
                        }
                    }
                }

                string RawInfoData = Builder.ToString();

                if (string.IsNullOrWhiteSpace(RawInfoData))
                {
                    throw new FileDamagedException("File damaged, could not be decrypted");
                }

                int HeaderLength = Encoding.UTF8.GetBytes(RawInfoData).Length;

                BaseFileStream.Seek(HeaderLength, SeekOrigin.Begin);

                if (RawInfoData.Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string InfoData)
                {
                    string[] FieldArray = InfoData.Split('|', StringSplitOptions.RemoveEmptyEntries);

                    SLEVersion Version = FieldArray.Length switch
                    {
                        2 => SLEVersion.Version_1_0_0,
                        3 => (SLEVersion)Convert.ToInt32(FieldArray[2]),
                        _ => throw new FileDamagedException("File damaged, could not be decrypted")
                    };

                    return new SLEHeader(Version, FieldArray[1], Convert.ToInt32(FieldArray[0]), HeaderLength);
                }
                else
                {
                    throw new FileDamagedException("File damaged, could not be decrypted");
                }
            }
            finally
            {
                BaseFileStream.Seek(OriginPosition, SeekOrigin.Begin);
            }
        }

        private SLEHeader(SLEVersion Version, string FileName, int KeySize, int HeaderLength)
        {
            if (string.IsNullOrWhiteSpace(FileName))
            {
                throw new ArgumentException("FileName could not be empty", nameof(FileName));
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidDataException("KeySize could only be set with 128 or 256");
            }

            this.Version = Version;
            this.FileName = FileName;
            this.KeySize = KeySize;
            this.HeaderLength = HeaderLength;
        }

        public SLEHeader(SLEVersion Version, string FileName, int KeySize) : this(Version, FileName, KeySize, 0)
        {

        }
    }
}
