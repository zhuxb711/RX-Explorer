using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public abstract class OperationListBaseModel : INotifyPropertyChanged
    {
        public abstract string OperationKindText { get; }

        public abstract string FromDescription { get; }

        public abstract string ToDescription { get; }

        public int Progress { get; private set; }

        public string ProgressSpeed { get; private set; }

        public string RemainingTime { get; private set; }

        public string ActionButton1Content { get; private set; }

        public string ActionButton2Content { get; private set; }

        public string ActionButton3Content { get; private set; }

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
                    case OperationStatus.NeedAttention:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_NeedAttention")}: {Message}";
                        }
                    case OperationStatus.Error:
                        {
                            return $"{Globalization.GetString("TaskList_Task_Status_Error")}: {Message}";
                        }
                    case OperationStatus.Completed:
                        {
                            return Globalization.GetString("TaskList_Task_Status_Completed");
                        }
                    case OperationStatus.Cancelling:
                        {
                            return Globalization.GetString("TaskList_Task_Status_Cancelling");
                        }
                    case OperationStatus.Cancelled:
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

        public bool ProgressPause { get; private set; }

        public abstract bool CanBeCancelled { get; }

        public Visibility RemoveButtonVisibility { get; private set; }

        public Visibility ActionButtonAreaVisibility { get; private set; }

        public Visibility SpeedAndTimeVisibility { get; private set; }

        public Visibility CancelButtonVisibility { get; private set; }

        public Visibility ActionButton1Visibility { get; private set; }

        public Visibility ActionButton2Visibility { get; private set; }

        public Visibility ActionButton3Visibility { get; private set; }


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
                ProgressPause = false;
                ProgressError = false;
                ProgressIndeterminate = true;

                switch (status)
                {
                    case OperationStatus.Waiting:
                        {
                            RemoveButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Preparing:
                        {
                            CancelButtonVisibility = CanBeCancelled ? Visibility.Visible : Visibility.Collapsed;
                            RemoveButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.Processing:
                        {
                            CancelButtonVisibility = CanBeCancelled ? Visibility.Visible : Visibility.Collapsed;
                            RemoveButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Visible;
                            ActionButtonAreaVisibility = Visibility.Collapsed;
                            break;
                        }
                    case OperationStatus.NeedAttention:
                        {
                            ProgressPause = true;

                            ActionButton1Content = Globalization.GetString("NameCollision_Override");
                            ActionButton2Content = Globalization.GetString("NameCollision_Rename");
                            ActionButton3Content = Globalization.GetString("NameCollision_MoreOption");

                            RemoveButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Visible;
                            break;
                        }
                    case OperationStatus.Error:
                        {
                            ProgressError = true;

                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;

                            OnErrorThrow?.Invoke(this, null);
                            break;
                        }
                    case OperationStatus.Cancelling:
                        {
                            ProgressPause = true;

                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;

                            OnCancelRequested?.Invoke(this, null);
                            break;
                        }
                    case OperationStatus.Cancelled:
                        {
                            ProgressPause = true;

                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;

                            OnCancelled?.Invoke(this, null);
                            break;
                        }
                    case OperationStatus.Completed:
                        {
                            ProgressIndeterminate = false;

                            RemoveButtonVisibility = Visibility.Visible;
                            CancelButtonVisibility = Visibility.Collapsed;
                            SpeedAndTimeVisibility = Visibility.Collapsed;
                            ActionButtonAreaVisibility = Visibility.Collapsed;

                            UpdateProgress(100);

                            OnCompleted?.Invoke(this, null);
                            break;
                        }
                }

                OnPropertyChanged(nameof(ActionButton1Content));
                OnPropertyChanged(nameof(ActionButton2Content));
                OnPropertyChanged(nameof(ActionButton3Content));
                OnPropertyChanged(nameof(ActionButton1Visibility));
                OnPropertyChanged(nameof(ActionButton2Visibility));
                OnPropertyChanged(nameof(ActionButton3Visibility));
                OnPropertyChanged(nameof(ActionButtonAreaVisibility));
                OnPropertyChanged(nameof(CancelButtonVisibility));
                OnPropertyChanged(nameof(RemoveButtonVisibility));
                OnPropertyChanged(nameof(SpeedAndTimeVisibility));
                OnPropertyChanged(nameof(ProgressIndeterminate));
                OnPropertyChanged(nameof(ProgressError));
                OnPropertyChanged(nameof(ProgressPause));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private string Message;
        private short ActionButtonIndex = -1;

        private event EventHandler OnCompleted;
        private event EventHandler OnErrorThrow;
        private event EventHandler OnCancelled;
        public event EventHandler OnCancelRequested;

        protected ProgressCalculator Calculator;

        public abstract Task PrepareSizeDataAsync(CancellationToken Token);

        public void UpdateProgress(int NewProgress)
        {
            Progress = Math.Min(Math.Max(0, NewProgress), 100);

            if (Calculator != null)
            {
                Calculator.SetProgressValue(Progress);
                ProgressSpeed = Calculator.GetSpeed();
                RemainingTime = Calculator.GetRemainingTime().ConvertTimeSpanToString();
            }

            if (Progress > 0 && ProgressIndeterminate)
            {
                ProgressIndeterminate = false;
                OnPropertyChanged(nameof(ProgressIndeterminate));
            }

            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(ProgressSpeed));
            OnPropertyChanged(nameof(RemainingTime));
        }

        public void UpdateStatus(OperationStatus Status, string Message = null)
        {
            if (Status == OperationStatus.Cancelling && !CanBeCancelled)
            {
                throw new ArgumentException("This task could not be cancelled", nameof(Status));
            }

            this.Message = Message;
            this.Status = Status;
        }

        public void ActionButton1(object sender, RoutedEventArgs args)
        {
            ActionButtonIndex = 0;
        }

        public void ActionButton2(object sender, RoutedEventArgs args)
        {
            ActionButtonIndex = 1;
        }

        public void ActionButton3(object sender, RoutedEventArgs args)
        {
            ActionButtonIndex = 2;
        }

        public short WaitForButtonAction()
        {
            try
            {
                if (Status != OperationStatus.NeedAttention)
                {
                    throw new ArgumentException("Status is not correct", nameof(Status));
                }

                while (ActionButtonIndex < 0 && Status != OperationStatus.Cancelled)
                {
                    Thread.Sleep(500);
                }

                return ActionButtonIndex;
            }
            finally
            {
                ActionButtonIndex = -1;
            }
        }

        public OperationListBaseModel(EventHandler OnCompleted, EventHandler OnErrorThrow, EventHandler OnCancelled)
        {
            Status = OperationStatus.Waiting;
            RemoveButtonVisibility = Visibility.Collapsed;
            ActionButtonAreaVisibility = Visibility.Collapsed;
            SpeedAndTimeVisibility = Visibility.Collapsed;

            ProgressIndeterminate = true;

            this.OnCompleted = OnCompleted;
            this.OnErrorThrow = OnErrorThrow;
            this.OnCancelled = OnCancelled;
        }
    }
}
