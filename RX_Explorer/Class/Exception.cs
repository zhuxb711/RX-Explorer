using System;

namespace RX_Explorer.Class
{
    public sealed class LocationNotAvailableException : Exception
    {
        public LocationNotAvailableException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public LocationNotAvailableException() : base()
        {
        }

        public LocationNotAvailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }



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

    public sealed class NoResponseException : Exception
    {
        public NoResponseException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public NoResponseException() : base()
        {
        }

        public NoResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class UnlockException : Exception
    {
        public UnlockException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public UnlockException() : base()
        {
        }

        public UnlockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class FileCaputureException : Exception
    {
        public FileCaputureException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public FileCaputureException() : base()
        {
        }

        public FileCaputureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
