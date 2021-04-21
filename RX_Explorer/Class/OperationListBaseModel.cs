using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public abstract class OperationListBaseModel : INotifyPropertyChanged
    {
        public abstract string OperationKindText { get; }

        public string[] FromPath { get; }

        public string ToPath { get; }

        public virtual string FromPathText
        {
            get
            {
                if (FromPath.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, FromPath.Take(5))}{Environment.NewLine}({FromPath.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, FromPath)}";
                }
            }
        }

        public virtual string ToPathText
        {
            get
            {
                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{ToPath}";
            }
        }

        public int Progress { get; private set; }

        public string ProgressSpeed { get; private set; }

        public string RemainingTime { get; private set; }

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case OperationStatus.Waiting:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_Waiting")}...";
                        }
                    case OperationStatus.Preparing:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_Preparing")}...";
                        }
                    case OperationStatus.Processing:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_Processing")}...";
                        }
                    case OperationStatus.Error:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_Error")}: {ErrorMessage}";
                        }
                    case OperationStatus.Complete:
                        {
                            return Globalization.GetString("TaskList_Task_Status_Completed");
                        }
                    case OperationStatus.Cancel:
                        {
                            return Globalization.GetString("TaskList_Task_Status_Cancelled");
                        }
                    default:
                        {
                            return string.Empty;
                        }
                }
            }
        }

        public bool ProgressIndeterminate { get; private set; }

        public bool ProgressError { get; private set; }

        public bool ProgressCancel { get; private set; }

        public Visibility RemoveButtonVisibility { get; private set; } = Visibility.Collapsed;

        public Visibility CancelButtonVisibility { get; private set; } = Visibility.Collapsed;

        public Visibility SpeedAndTimeVisibility { get; private set; } = Visibility.Collapsed;

        private OperationStatus status;
        public OperationStatus Status
        {
            get
            {
                return status;
            }
            private set
            {
                status = value;

                switch (status)
                {
                    case OperationStatus.Waiting:
                        {
                            ProgressIndeterminate = true;
                            RemoveButtonVisibility = Visibility.Collapsed;
                            CancelButtonVisibility = Visibility.Visible;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Preparing:
                        {
                            ProgressIndeterminate = true;
                            RemoveButtonVisibility = Visibility.Collapsed;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Processing:
                        {
                            ProgressIndeterminate = false;
                            RemoveButtonVisibility = Visibility.Collapsed;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Visible;
                            break;
                        }
                    case OperationStatus.Error:
                        {
                            ProgressIndeterminate = true;
                            ProgressError = true;
                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Cancel:
                        {
                            ProgressIndeterminate = true;
                            ProgressCancel = true;
                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Complete:
                        {
                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ProgressIndeterminate = false;
                            OnCompleted?.Invoke(this, null);
                            break;
                        }
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CancelButtonVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemoveButtonVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeedAndTimeVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressIndeterminate)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressError)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressCancel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private string ErrorMessage;

        private event EventHandler OnCompleted;

        private ProgressCalculator Calculator;

        public async Task PrepareSizeDataAsync()
        {
            ulong TotalSize = 0;

            foreach (string Path in FromPath)
            {
                switch (await FileSystemStorageItemBase.OpenAsync(Path))
                {
                    case FileSystemStorageFolder Folder:
                        {
                            TotalSize += await Folder.GetFolderSizeAsync();
                            break;
                        }
                    case FileSystemStorageFile File:
                        {
                            TotalSize += File.SizeRaw;
                            break;
                        }
                }
            }

            Calculator = new ProgressCalculator(TotalSize);
        }

        public void UpdateProgress(int NewProgress)
        {
            Progress = Math.Min(Math.Max(0, NewProgress), 100);

            if (Calculator != null)
            {
                Calculator.SetProgressValue(NewProgress);
                ProgressSpeed = Calculator.GetSpeed();
                RemainingTime = Calculator.GetRemainingTime().ConvertTimsSpanToString();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressSpeed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingTime)));
        }

        public void UpdateStatus(OperationStatus Status, string ErrorMessage = null)
        {
            this.ErrorMessage = ErrorMessage;
            this.Status = Status;
        }

        public OperationListBaseModel(string[] FromPath, string ToPath, EventHandler OnCompleted)
        {
            Status = OperationStatus.Waiting;
            ProgressIndeterminate = true;

            this.FromPath = FromPath;
            this.ToPath = ToPath;
            this.OnCompleted = OnCompleted;
        }
    }
}
