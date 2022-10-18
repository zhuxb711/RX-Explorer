using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RX_Explorer.Class
{
    public sealed class SLEHeader
    {
        public SLEHeaderCore Core { get; }

        public int HeaderSize { get; private set; }

        public Encoding HeaderEncoding { get; }

        public static SLEHeader GetHeader(Stream BaseFileStream, Encoding HeaderEncoding = null)
        {
            long OriginPosition = BaseFileStream.Position;

            try
            {
                if (HeaderEncoding == null)
                {
                    HeaderEncoding = new UTF8Encoding(false);
                }

                try
                {
                    using (BinaryReader Reader = new BinaryReader(BaseFileStream, HeaderEncoding, true))
                    {
                        // Check the file whether version is lower than SLE200
                        if (Reader.PeekChar() == '$')
                        {
                            char[] Chars = Reader.ReadChars(512);

                            int EndSignalIndex = Array.FindIndex(Chars, 1, (Char) => Char == '$');

                            if (EndSignalIndex > 1)
                            {
                                string RawInfoData = new string(Chars.Take(EndSignalIndex + 1).ToArray());

                                if (!string.IsNullOrWhiteSpace(RawInfoData))
                                {
                                    if (RawInfoData.Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string InfoData)
                                    {
                                        string[] FieldArray = InfoData.Split('|', StringSplitOptions.RemoveEmptyEntries);

                                        SLEVersion Version = FieldArray.Length switch
                                        {
                                            2 => SLEVersion.SLE100,
                                            3 => (SLEVersion)Convert.ToInt32(FieldArray[2]),
                                            _ => throw new FileDamagedException("Encrypted file structure invalid, could not be decrypted")
                                        };

                                        return new SLEHeader(Version, HeaderEncoding, FieldArray[1], Convert.ToInt32(FieldArray[0]), HeaderEncoding.GetByteCount(RawInfoData));
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Check the file whether version is higher than SLE200
                            int HeaderContentSize = Reader.ReadInt32();

                            if (HeaderContentSize > 0)
                            {
                                return new SLEHeader(JsonSerializer.Deserialize<SLEHeaderCore>(HeaderEncoding.GetString(Reader.ReadBytes(HeaderContentSize))), HeaderEncoding, HeaderContentSize + BitConverter.GetBytes(int.MaxValue).Length);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not analysis the header of SLE file");
                }

                throw new FileDamagedException("Encrypted file structure invalid, could not be decrypted");
            }
            finally
            {
                BaseFileStream.Seek(OriginPosition, SeekOrigin.Begin);
            }
        }

        public void WriteHeader(Stream BaseFileStream)
        {
            BaseFileStream.Seek(0, SeekOrigin.Begin);

            if (Core.Version >= SLEVersion.SLE200)
            {
                using (BinaryWriter Writer = new BinaryWriter(BaseFileStream, HeaderEncoding, true))
                {
                    string HeaderContent = JsonSerializer.Serialize(Core);
                    Writer.Write(HeaderEncoding.GetByteCount(HeaderContent));
                    Writer.Write(HeaderEncoding.GetBytes(JsonSerializer.Serialize(Core)));
                    Writer.Flush();
                }
            }
            else
            {
                using (StreamWriter Writer = new StreamWriter(BaseFileStream, HeaderEncoding, 512, true))
                {
                    Writer.Write($"${string.Join('|', Core.KeySize, Core.FileName.Replace('$', '_'), (int)Core.Version)}$");
                    Writer.Flush();
                }
            }

            HeaderSize = (int)BaseFileStream.Length;
        }

        private SLEHeader(SLEHeaderCore Core, Encoding HeaderEncoding, int HeaderSize)
        {
            if (Core.Version <= SLEVersion.SLE150 && !new UTF8Encoding(false).Equals(HeaderEncoding))
            {
                throw new ArgumentException($"Header encoding must be {nameof(UTF8Encoding)} without BOM if the version is lower or equals than {SLEVersion.SLE150}");
            }

            this.Core = Core;
            this.HeaderEncoding = HeaderEncoding;
            this.HeaderSize = HeaderSize;
        }

        private SLEHeader(SLEVersion Version, Encoding HeaderEncoding, string FileName, int KeySize, int HeaderSize) : this(new SLEHeaderCore(Version, FileName, KeySize), HeaderEncoding, HeaderSize)
        {

        }

        public SLEHeader(SLEVersion Version, Encoding HeaderEncoding, string FileName, int KeySize) : this(new SLEHeaderCore(Version, FileName, KeySize), HeaderEncoding, 0)
        {

        }

        public sealed class SLEHeaderCore
        {
            public int KeySize { get; }

            public string FileName { get; }

            public SLEVersion Version { get; }

            public SLEHeaderCore(SLEVersion Version, string FileName, int KeySize)
            {
                if (string.IsNullOrWhiteSpace(FileName))
                {
                    throw new ArgumentException("FileName could not be empty", nameof(FileName));
                }

                if (KeySize != 256 && KeySize != 128)
                {
                    throw new InvalidDataException("KeySize could only be set with 128 or 256");
                }

                this.KeySize = KeySize;
                this.FileName = FileName;
                this.Version = Version;
            }
        }
    }
}
