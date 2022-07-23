using FluentFTP;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class FTPFileSaveOnFlushStream : VirtualSaveOnFlushBaseStream
    {
        private readonly string Path;
        private readonly FTPClientController Controller;

        protected override async Task FlushCoreAsync(CancellationToken CancelToken)
        {
            BaseStream.Seek(0, SeekOrigin.Begin);

            FTPPathAnalysis Analysis = new FTPPathAnalysis(Path);

            if (await Controller.RunCommandAsync((Client) => Client.FileExistsAsync(Analysis.RelatedPath)))
            {
                await Controller.RunCommandAsync((Client) => Client.UploadStreamAsync(BaseStream, Analysis.RelatedPath, FtpRemoteExists.Overwrite));
            }
        }

        public FTPFileSaveOnFlushStream(string Path, FTPClientController Controller, Stream BaseStream) : base(BaseStream)
        {
            this.Controller = Controller ?? throw new ArgumentNullException(nameof(Controller), "Argument could not be null");
            this.Path = string.IsNullOrWhiteSpace(Path) ? throw new ArgumentNullException(nameof(Path), "Argument could not be null") : Path;
        }
    }
}
