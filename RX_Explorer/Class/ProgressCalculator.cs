using System;

namespace RX_Explorer.Class
{
    public sealed class ProgressCalculator
    {
        public ulong TotalSize { get; }

        private volatile int ProgressValue;

        private DateTimeOffset LastRecordTime = DateTimeOffset.Now;

        private readonly TimeSpan MinRefreshSpan = TimeSpan.FromMilliseconds(1000);

        private TimeSpan Span;

        private ulong DataOperatedInOneSpan;

        private readonly object Locker = new object();

        public void SetProgressValue(int ProgressValue)
        {
            lock (Locker)
            {
                if (ProgressValue < 100
                    && ProgressValue > this.ProgressValue
                    && DateTimeOffset.Now - LastRecordTime > MinRefreshSpan)
                {
                    Span = DateTimeOffset.Now - LastRecordTime;
                    DataOperatedInOneSpan = Convert.ToUInt64(ProgressValue - this.ProgressValue) * TotalSize / 100;
                    LastRecordTime = DateTimeOffset.Now;

                    this.ProgressValue = Math.Max(Math.Min(100, ProgressValue), 0);
                }
            }
        }

        public string GetSpeed()
        {
            lock (Locker)
            {
                if (Span.TotalSeconds > 0)
                {
                    return GetSpeedDescription(DataOperatedInOneSpan / Span.TotalSeconds);
                }
                else
                {
                    return "0 KB/s";
                }
            }
        }

        public TimeSpan GetRemainingTime()
        {
            lock (Locker)
            {
                if (DataOperatedInOneSpan == 0 || Span.TotalSeconds == 0)
                {
                    return TimeSpan.MaxValue;
                }
                else
                {
                    return TimeSpan.FromSeconds(Convert.ToUInt64(100 - ProgressValue) * TotalSize / 100 / (DataOperatedInOneSpan / Span.TotalSeconds));
                }
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
