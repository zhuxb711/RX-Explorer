using System;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class ProgressCalculator
    {
        private readonly ulong TotalSize;
        private readonly TimeSpan MinRefreshSpan = TimeSpan.FromMilliseconds(1000);

        private int ProgressValue;

        private DateTimeOffset LastRecordTime = DateTimeOffset.Now;

        private TimeSpan Span;

        private ulong DataOperatedInOneSpan;

        public void SetProgressValue(int NewValue)
        {
            if (NewValue is >= 0 and <= 100)
            {
                TimeSpan LocalSpan = DateTimeOffset.Now - LastRecordTime;

                if (LocalSpan >= MinRefreshSpan && NewValue > Volatile.Read(ref ProgressValue))
                {
                    Span = LocalSpan;
                    LastRecordTime = DateTimeOffset.Now;
                    DataOperatedInOneSpan = Convert.ToUInt64(NewValue - Interlocked.Exchange(ref ProgressValue, NewValue)) * TotalSize / 100;
                }
            }
        }

        public string GetSpeed()
        {
            double TotalSeconds = Span.TotalSeconds;
            ulong DataOperated = DataOperatedInOneSpan;

            if (TotalSeconds > 0)
            {
                return GetSpeedDescription(DataOperated / TotalSeconds);
            }
            else
            {
                return "0 KB/s";
            }
        }

        public TimeSpan GetRemainingTime()
        {
            double TotalSeconds = Span.TotalSeconds;
            ulong DataOperated = DataOperatedInOneSpan;

            if (TotalSeconds == 0 || DataOperated == 0)
            {
                return TimeSpan.MaxValue;
            }
            else
            {
                return TimeSpan.FromSeconds(Convert.ToUInt64(100 - ProgressValue) * TotalSize / 100 / (DataOperated / TotalSeconds));
            }
        }

        private string GetSpeedDescription(double SizeRaw)
        {
            if (SizeRaw > 0)
            {
                switch ((short)Math.Log(SizeRaw, 1024))
                {
                    case 0:
                        {
                            return $"{SizeRaw} B/s";
                        }
                    case 1:
                        {
                            return $"{SizeRaw / 1024d:##.##} KB/s";
                        }
                    case 2:
                        {
                            return $"{SizeRaw / 1048576d:##.##} MB/s";
                        }
                    case 3:
                        {
                            return $"{SizeRaw / 1073741824d:##.##} GB/s";
                        }
                    case 4:
                        {
                            return $"{SizeRaw / 1099511627776d:##.##} TB/s";
                        }
                    case 5:
                        {
                            return $"{SizeRaw / 1125899906842624d:##.##} PB/s";
                        }
                    default:
                        {
                            throw new ArgumentOutOfRangeException(nameof(SizeRaw), "Argument is too large");
                        }
                }
            }
            else
            {
                return "0 KB/s";
            }
        }

        public ProgressCalculator(ulong TotalSize)
        {
            this.TotalSize = TotalSize;
        }
    }
}
