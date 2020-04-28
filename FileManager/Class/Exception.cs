using System;

namespace FileManager.Class
{
    /// <summary>
    /// 密码错误异常
    /// </summary>
    public sealed class PasswordErrorException : Exception
    {
        public PasswordErrorException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public PasswordErrorException() : base()
        {
        }

        public PasswordErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 文件损坏异常
    /// </summary>
    public sealed class FileDamagedException : Exception
    {
        public FileDamagedException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public FileDamagedException() : base()
        {
        }

        public FileDamagedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 网络错误
    /// </summary>
    public sealed class NetworkException : Exception
    {
        public NetworkException(string ErrorMessage) : base(ErrorMessage)
        {

        }

        public NetworkException() : base()
        {

        }

        public NetworkException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
