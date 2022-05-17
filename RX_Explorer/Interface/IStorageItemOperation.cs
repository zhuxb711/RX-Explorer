using ShareClassLibrary;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Interface
{
    public interface IStorageItemOperation
    {
        public Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null);
        public Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.Skip, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null);
        public Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null);
        public Task<string> RenameAsync(string DesireName, CancellationToken CancelToken = default);
    }
}
