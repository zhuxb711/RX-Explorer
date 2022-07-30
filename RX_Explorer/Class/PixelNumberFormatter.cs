using System;
using Windows.Globalization.NumberFormatting;

namespace RX_Explorer.Class
{
    public sealed class PixelNumberFormatter : INumberFormatter, INumberFormatter2, INumberParser
    {
        public string FormatInt(long value)
        {
            return Format(value);
        }

        public string FormatUInt(ulong value)
        {
            return Format(value);
        }

        public string FormatDouble(double value)
        {
            return Format(value);
        }

        public long? ParseInt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Argument could not be empty or null", nameof(text));
            }

            if (text.EndsWith("px"))
            {
                string[] SplitArray = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (SplitArray.Length == 2 && long.TryParse(SplitArray[0], out long Result))
                {
                    return Result;
                }
            }

            throw new Exception($"Could not parse the value \"{text}\"");
        }

        public ulong? ParseUInt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Argument could not be empty or null", nameof(text));
            }

            if (text.EndsWith("px"))
            {
                string[] SplitArray = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (SplitArray.Length == 2 && ulong.TryParse(SplitArray[0], out ulong Result))
                {
                    return Result;
                }
            }

            throw new Exception($"Could not parse the value \"{text}\"");
        }

        public double? ParseDouble(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Argument could not be empty or null", nameof(text));
            }

            if (text.EndsWith("px"))
            {
                string[] SplitArray = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (SplitArray.Length == 2 && double.TryParse(SplitArray[0], out double Result))
                {
                    return Result;
                }
            }

            throw new Exception($"Could not parse the value \"{text}\"");
        }

        public string Format(long value)
        {
            return $"{value} px";
        }

        public string Format(ulong value)
        {
            return $"{value} px";
        }

        public string Format(double value)
        {
            return $"{value} px";
        }
    }
}
