using System;
using System.Collections.Generic;
using System.IO;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对文件路径的逐层解析
    /// </summary>
    public sealed class PathAnalysis
    {
        /// <summary>
        /// 完整路径
        /// </summary>
        public string FullPath { get; private set; }

        private Queue<string> PathQueue;

        private string CurrentPath;

        /// <summary>
        /// 指示是否还有下一级路径
        /// </summary>
        public bool HasNextLevel
        {
            get
            {
                return PathQueue.Count > 0;
            }
        }

        /// <summary>
        /// 初始化PathAnalysis对象
        /// </summary>
        /// <param name="FullPath">完整路径</param>
        /// <param name="CurrentPath">当前路径</param>
        public PathAnalysis(string FullPath, string CurrentPath)
        {
            if (string.IsNullOrEmpty(FullPath))
            {
                throw new ArgumentNullException(nameof(FullPath), "FullPath could not be null or empty");
            }

            this.FullPath = FullPath.TrimEnd('\\');
            this.CurrentPath = CurrentPath.TrimEnd('\\') ?? string.Empty;

            if (string.IsNullOrEmpty(this.CurrentPath))
            {
                string[] Split = this.FullPath.Split("\\", StringSplitOptions.RemoveEmptyEntries);

                if (this.FullPath.StartsWith(@"\\?\") && Split.Length > 0)
                {
                    Split[0] = $@"\\?\{Split[1]}\";
                }
                else if (this.FullPath.StartsWith(@"\\") && Split.Length > 0)
                {
                    Split[0] = $@"\\{Split[0]}\";
                }
                else
                {
                    Split[0] = $@"{Split[0]}\";
                }

                PathQueue = new Queue<string>(Split);
            }
            else
            {
                if (this.FullPath.TrimEnd('\\').Equals(this.CurrentPath.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    PathQueue = new Queue<string>(0);
                }
                else
                {
                    PathQueue = new Queue<string>(this.FullPath.Replace(this.CurrentPath, string.Empty).Split("\\", StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        public PathAnalysis(string FullPath) : this(FullPath, string.Empty)
        {

        }

        /// <summary>
        /// 获取下一级文件夹的完整路径
        /// </summary>
        /// <returns></returns>
        public string NextFullPath()
        {
            if (PathQueue.TryDequeue(out string RelativePath))
            {
                CurrentPath = Path.Combine(CurrentPath, RelativePath);
            }

            return CurrentPath;
        }

        /// <summary>
        /// 获取下一级文件夹的相对路径
        /// </summary>
        /// <returns></returns>
        public string NextRelativePath()
        {
            if (PathQueue.TryDequeue(out string RelativePath))
            {
                return RelativePath;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
