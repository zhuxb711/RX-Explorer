using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对反馈内容的UI支持
    /// </summary>
    public sealed class FeedBackItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// 建议或反馈内容
        /// </summary>
        public string Suggestion { get; private set; }

        /// <summary>
        /// 支持的人数
        /// </summary>
        public string LikeNum { get; private set; }

        /// <summary>
        /// 踩的人数
        /// </summary>
        public string DislikeNum { get; private set; }

        /// <summary>
        /// 文字描述
        /// </summary>
        public string SupportDescription { get; private set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserID { get; private set; }

        /// <summary>
        /// 此反馈的GUID
        /// </summary>
        public string GUID { get; private set; }

        public bool IsTranslated { get; set; } = false;

        /// <summary>
        /// 记录当前用户的操作
        /// </summary>
        public string UserVoteAction
        {
            get
            {
                return userVoteAction;
            }
            set
            {
                switch (value)
                {
                    case "=":
                        {
                            isLike = false;
                            isDislike = false;
                            break;
                        }
                    case "+":
                        {
                            isLike = true;
                            isDislike = false;
                            break;
                        }
                    case "-":
                        {
                            isLike = false;
                            isDislike = true;
                            break;
                        }
                }
                userVoteAction = value;
            }
        }

        public bool? IsLike
        {
            get
            {
                return isLike;
            }
            set
            {
                if (value != isLike)
                {
                    if (value.GetValueOrDefault())
                    {
                        isLike = true;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.AddLike);
                    }
                    else
                    {
                        isLike = false;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.DelLike);
                    }
                }
            }
        }

        public bool? IsDislike
        {
            get
            {
                return isDislike;
            }
            set
            {
                if (value != isDislike)
                {
                    if (value.GetValueOrDefault())
                    {
                        isLike = false;
                        isDislike = true;
                        UpdateSupportInfo(FeedBackUpdateType.AddDislike);
                    }
                    else
                    {
                        isLike = false;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.DelDislike);
                    }
                }
            }
        }

        private string userVoteAction;
        private bool? isLike;
        private bool? isDislike;
        private static AutoResetEvent Locker = new AutoResetEvent(true);

        /// <summary>
        /// 初始化FeedBackItem
        /// </summary>
        /// <param name="UserName">用户名</param>
        /// <param name="Title">标题</param>
        /// <param name="Suggestion">建议或反馈内容</param>
        /// <param name="LikeNum">支持的人数</param>
        /// <param name="DislikeNum">反对的人数</param>
        /// <param name="UserID">用户ID</param>
        /// <param name="GUID">反馈的GUID</param>
        /// <param name="UserVoteAction">指示支持或反对</param>
        public FeedBackItem(string UserName, string Title, string Suggestion, string LikeNum, string DislikeNum, string UserID, string GUID, string UserVoteAction = "=")
        {
            this.UserName = UserName;
            this.Title = Title;
            this.Suggestion = Suggestion;
            this.LikeNum = LikeNum;
            this.DislikeNum = DislikeNum;
            this.UserID = UserID;
            this.GUID = GUID;
            this.UserVoteAction = UserVoteAction;
            SupportDescription = $"({LikeNum} {Globalization.GetString("FeedBackItem_SupportDescription_Positive")} , {DislikeNum} {Globalization.GetString("FeedBackItem_SupportDescription_Negative")})";
        }

        /// <summary>
        /// 更新支持或反对的信息
        /// </summary>
        /// <param name="Type">更新类型</param>
        private async void UpdateSupportInfo(FeedBackUpdateType Type)
        {
            switch (Type)
            {
                case FeedBackUpdateType.AddLike:
                    {
                        if (UserVoteAction == "-")
                        {
                            DislikeNum = (Convert.ToInt16(DislikeNum) - 1).ToString();
                        }

                        LikeNum = (Convert.ToInt16(LikeNum) + 1).ToString();
                        UserVoteAction = "+";
                        break;
                    }
                case FeedBackUpdateType.DelLike:
                    {
                        LikeNum = (Convert.ToInt16(LikeNum) - 1).ToString();
                        UserVoteAction = "=";
                        break;
                    }
                case FeedBackUpdateType.AddDislike:
                    {
                        if (UserVoteAction == "+")
                        {
                            LikeNum = (Convert.ToInt16(LikeNum) - 1).ToString();
                        }

                        DislikeNum = (Convert.ToInt16(DislikeNum) + 1).ToString();
                        UserVoteAction = "-";
                        break;
                    }
                case FeedBackUpdateType.DelDislike:
                    {
                        DislikeNum = (Convert.ToInt16(DislikeNum) - 1).ToString();
                        UserVoteAction = "=";
                        break;
                    }
            }

            SupportDescription = $"({LikeNum} {Globalization.GetString("FeedBackItem_SupportDescription_Positive")} , {DislikeNum} {Globalization.GetString("FeedBackItem_SupportDescription_Negative")})";

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLike)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDislike)));

            await Task.Run(() =>
            {
                Locker.WaitOne();
            }).ConfigureAwait(true);

            try
            {
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(this).ConfigureAwait(true))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("Network_Error_Dialog_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            finally
            {
                Locker.Set();
            }
        }

        /// <summary>
        /// 更新反馈内容
        /// </summary>
        /// <param name="Title">标题</param>
        /// <param name="Suggestion">建议</param>
        public void UpdateTitleAndSuggestion(string Title, string Suggestion)
        {
            this.Title = Title;
            this.Suggestion = Suggestion;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Title)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Suggestion)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
