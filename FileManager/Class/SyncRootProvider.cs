namespace FileManager.Class
{
    /// <summary>
    /// 提供全局锁定根
    /// </summary>
    public static class SyncRootProvider
    {
        /// <summary>
        /// 锁定根对象
        /// </summary>
        public static object SyncRoot { get; } = new object();
    }
}
