using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RX_Explorer.Class
{
    public sealed class SLEHeader
    {
        public int HeaderSize { get; private set; }

        public int ContentOffset
        {
            get => HeaderSize + HeaderEncoding.GetByteCount("PASSWORD_CORRECT");
        }

        public SLEHeaderCore Core { get; }

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

                // Check the file whether version is lower than SLE200
                try
                {
                    using (StreamReader Reader = new StreamReader(BaseFileStream, HeaderEncoding, true, 512, true))
                    {
                        if (Convert.ToChar(Reader.Peek()) == '$')
                        {
                            StringBuilder Builder = new StringBuilder(256);

                            for (int Index = 0; Index < 256; Index++)
                            {
                                int CharInt = Reader.Read();

                                if (CharInt < 0)
                                {
                                    break;
                                }

                                char Char = Convert.ToChar(CharInt);

                                Builder.Append(Char);

                                if (Builder.Length > 1 && Char == '$')
                                {
                                    break;
                                }
                            }

                            Match RegexMatch = Regex.Match(Builder.ToString(), @"^\$(?<HeaderFields>(.+))\$$");

                            if (RegexMatch.Success)
                            {
                                string[] HeaderFields = RegexMatch.Groups["HeaderFields"].Value.Split('|', StringSplitOptions.RemoveEmptyEntries);

                                if (HeaderFields.Length == 3)
                                {
                                    SLEVersion Version = HeaderFields.Length switch
                                    {
                                        2 => SLEVersion.SLE100,
                                        3 => (SLEVersion)Convert.ToInt32(HeaderFields[2]),
                                        _ => throw new SLEHeaderInvalidException("SLE header structure invalid, version parsing failed")
                                    };

                                    SLEKeySize KeySize = Convert.ToInt32(HeaderFields[0]) switch
                                    {
                                        128 => SLEKeySize.AES128,
                                        256 => SLEKeySize.AES256,
                                        _ => throw new SLEHeaderInvalidException("SLE header structure invalid, key size parsing failed")
                                    };

                                    return new SLEHeader(Version, SLEOriginType.File, KeySize, HeaderEncoding, HeaderFields[1], HeaderEncoding.GetByteCount(Builder.ToString()));
                                }
                                else
                                {
                                    throw new SLEHeaderInvalidException("SLE header structure invalid, header fields number checking failed");
                                }
                            }
                            else
                            {
                                throw new SLEHeaderInvalidException("SLE header structure invalid, regex checking on header failed");
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not SLEHeaderInvalidException)
                {
                    LogTracer.Log(ex, $"Could not analyze the header as {SLEVersion.SLE150} or lower format");
                }

                BaseFileStream.Seek(OriginPosition, SeekOrigin.Begin);

                // Check the file whether version is higher than SLE200
                try
                {
                    using (BinaryReader Reader = new BinaryReader(BaseFileStream, HeaderEncoding, true))
                    {
                        int HeaderSize = Reader.ReadInt32();

                        if (HeaderSize > 0)
                        {
                            byte[] HeaderBytes = Reader.ReadBytes(HeaderSize);

                            if (HeaderBytes.Length == HeaderSize)
                            {
                                return new SLEHeader(JsonConvert.DeserializeObject<SLEHeaderCore>(HeaderEncoding.GetString(HeaderBytes)), HeaderEncoding, HeaderSize + BitConverter.GetBytes(int.MaxValue).Length);
                            }
                            else
                            {
                                throw new SLEHeaderInvalidException("SLE header structure invalid, header is not completed and not match the header size");
                            }
                        }
                        else
                        {
                            throw new SLEHeaderInvalidException("SLE header structure invalid, header length checking failed");
                        }
                    }
                }
                catch (Exception ex) when (ex is not SLEHeaderInvalidException)
                {
                    LogTracer.Log(ex, $"Could not analyze the header as {SLEVersion.SLE200} or higher format");
                }

                throw new SLEHeaderInvalidException($"SLE header structure invalid, the header is not match with any {nameof(SLEVersion)}");
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
                    string HeaderContent = JsonConvert.SerializeObject(Core);
                    Writer.Write(HeaderEncoding.GetByteCount(HeaderContent));
                    Writer.Write(HeaderEncoding.GetBytes(JsonConvert.SerializeObject(Core)));
                    Writer.Flush();
                }
            }
            else
            {
                using (StreamWriter Writer = new StreamWriter(BaseFileStream, HeaderEncoding, 512, true))
                {
                    Writer.Write($"${string.Join('|', (int)Core.KeySize, Core.FileName.Replace('$', '_'), (int)Core.Version)}$");
                    Writer.Flush();
                }
            }

            HeaderSize = (int)BaseFileStream.Length;
        }

        private SLEHeader(SLEHeaderCore Core, Encoding HeaderEncoding, int HeaderSize)
        {
            if (HeaderEncoding == null)
            {
                throw new ArgumentNullException(nameof(HeaderEncoding));
            }

            if (Core.Version <= SLEVersion.SLE150 && !(HeaderEncoding.Equals(new UTF8Encoding(false)) || HeaderEncoding.Equals(Encoding.UTF8)))
            {
                throw new ArgumentException($"Header encoding must be {nameof(UTF8Encoding)} without BOM if the version is lower or equals than {SLEVersion.SLE150}");
            }

            this.Core = Core;
            this.HeaderEncoding = HeaderEncoding;
            this.HeaderSize = HeaderSize;
        }

        private SLEHeader(SLEVersion Version, SLEOriginType OriginType, SLEKeySize KeySize, Encoding HeaderEncoding, string FileName, int HeaderSize) : this(new SLEHeaderCore(Version, OriginType, KeySize, FileName), HeaderEncoding, HeaderSize)
        {

        }

        public SLEHeader(SLEVersion Version, SLEOriginType OriginType, SLEKeySize KeySize, Encoding HeaderEncoding, string FileName) : this(new SLEHeaderCore(Version, OriginType, KeySize, FileName), HeaderEncoding, 0)
        {

        }

        public sealed class SLEHeaderCore
        {
            public string FileName { get; }

            public SLEVersion Version { get; }

            public SLEKeySize KeySize { get; }

            public SLEOriginType OriginType { get; }

            public SLEHeaderCore(SLEVersion Version, SLEOriginType OriginType, SLEKeySize KeySize, string FileName)
            {
                if (string.IsNullOrWhiteSpace(FileName))
                {
                    throw new ArgumentException("FileName could not be empty", nameof(FileName));
                }

                if (KeySize is not SLEKeySize.AES128 and not SLEKeySize.AES256)
                {
                    throw new ArgumentException("KeySize could only be set with 128 or 256", nameof(KeySize));
                }

                if (Version <= SLEVersion.SLE150 && OriginType == SLEOriginType.Folder)
                {
                    throw new NotSupportedException($"Version under {SLEVersion.SLE200} is not support for ecrypt folder");
                }

                this.KeySize = KeySize;
                this.FileName = FileName;
                this.Version = Version;
                this.OriginType = OriginType;
            }
        }
    }
}
