using System;

namespace RX_Explorer.Class
{
    public sealed class UnlockDriveFailedException : Exception
    {
        public UnlockDriveFailedException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public UnlockDriveFailedException() : base()
        {
        }

        public UnlockDriveFailedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

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

    public sealed class LaunchProgramException : Exception
    {
        public LaunchProgramException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public LaunchProgramException() : base()
        {
        }

        public LaunchProgramException(string message, Exception innerException) : base(message, innerException)
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
    public sealed class SLEHeaderInvalidException : Exception
    {
        public SLEHeaderInvalidException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public SLEHeaderInvalidException() : base()
        {
        }

        public SLEHeaderInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class UnlockFileFailedException : Exception
    {
        public UnlockFileFailedException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public UnlockFileFailedException() : base()
        {
        }

        public UnlockFileFailedException(string message, Exception innerException) : base(message, innerException)
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
