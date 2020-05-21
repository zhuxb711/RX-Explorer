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

        private string CurrentLevel;

        /// <summary>
        /// 指示是否还有下一级路径
        /// </summary>
        public bool HasNextLevel { get; private set; }

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

            this.FullPath = FullPath;

            CurrentLevel = CurrentPath;

            if (string.IsNullOrEmpty(CurrentPath))
            {
                string[] Split = FullPath.Split("\\", StringSplitOptions.RemoveEmptyEntries);
                Split[0] = Split[0] + "\\";
                PathQueue = new Queue<string>(Split);
                HasNextLevel = true;
            }
            else
            {
                if (FullPath != CurrentPath)
                {
                    HasNextLevel = true;
                    string[] Split = Path.GetRelativePath(CurrentPath, FullPath).Split("\\", StringSplitOptions.RemoveEmptyEntries);
                    PathQueue = new Queue<string>(Split);
                }
                else
                {
                    HasNextLevel = false;
                    PathQueue = new Queue<string>(0);
                }
            }
        }

        /// <summary>
        /// 获取下一级文件夹的完整路径
        /// </summary>
        /// <returns></returns>
        public string NextFullPath()
        {
            if (PathQueue.Count != 0)
            {
                CurrentLevel = Path.Combine(CurrentLevel, PathQueue.Dequeue());
                if (PathQueue.Count == 0)
                {
                    HasNextLevel = false;
                }
                return CurrentLevel;
            }
            else
            {
                return CurrentLevel;
            }
        }

        /// <summary>
        /// 获取下一级文件夹的相对路径
        /// </summary>
        /// <returns></returns>
        public string NextRelativePath()
        {
            if (PathQueue.Count != 0)
            {
                string RelativePath = PathQueue.Dequeue();
                if (PathQueue.Count == 0)
                {
                    HasNextLevel = false;
                }
                return RelativePath;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
