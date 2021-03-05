using RX_Explorer.Class;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface ICryptable
    {
        public Task<FileSystemStorageFile> EncryptAsync(string OutputDirectory, string Key, int KeySize, CancellationToken CancelToken = default);

        public Task<FileSystemStorageFile> DecryptAsync(string OutputDirectory, string Key, CancellationToken CancelToken = default);

        public Task<string> GetEncryptionLevelAsync();
    }
}
