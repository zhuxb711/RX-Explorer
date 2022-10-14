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

        public static SLEHeader GetHeader(Stream BaseFileStream)
        {
            long OriginPosition = BaseFileStream.Position;

            try
            {
                using (StreamReader Reader = new StreamReader(BaseFileStream, Encoding.UTF8, true, 512, true))
                {
                    char[] Buffer = new char[512];

                    if (Reader.ReadBlock(Buffer, 0, 512) > 0)
                    {
                        // Check the file whether version is lower than SLE200
                        if (Buffer[0] == '$')
                        {
                            int EndSignalIndex = Array.LastIndexOf(Buffer, '$');

                            if (EndSignalIndex > 1)
                            {
                                string RawInfoData = new string(Buffer.Take(EndSignalIndex + 1).ToArray());

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

                                        return new SLEHeader(new SLEHeaderCore(Version, FieldArray[1], Convert.ToInt32(FieldArray[0])), Encoding.UTF8.GetBytes(RawInfoData).Length);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Check the file whether version is higher than SLE200
                            try
                            {
                                string BufferString = new string(Buffer);
                                SLEHeaderCore Core = JsonSerializer.Deserialize<SLEHeaderCore>(BufferString.TrimEnd('\0'));
                                return new SLEHeader(Core, Encoding.UTF8.GetBytes(BufferString).Length);
                            }
                            catch (Exception)
                            {
                                // No need to handle this exception
                            }
                        }
                    }
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

            using (StreamWriter Writer = new StreamWriter(BaseFileStream, Encoding.UTF8, 512, true))
            {
                if (Core.Version >= SLEVersion.SLE200)
                {
                    Writer.Write(JsonSerializer.Serialize(Core).PadRight(512, '\0'));
                }
                else
                {
                    Writer.Write($"${Core.KeySize}|{Core.FileName.Replace('$', '_')}|{(int)Core.Version}$");
                }

                Writer.Flush();
            }
        }

        private SLEHeader(SLEHeaderCore Core, int HeaderSize)
        {
            this.Core = Core;
            this.HeaderSize = HeaderSize;
        }

        public SLEHeader(SLEVersion Version, string FileName, int KeySize)
        {
            Core = new SLEHeaderCore(Version, FileName, KeySize);
            HeaderSize = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Core)).Length;
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
