using System;
using System.ComponentModel;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class AnnouncementItem
    {
        public string Title { get; private set; }

        public string Content { get; private set; }

        public bool IsTranslated { get; set; }

        public AnnouncementItem(string Title, string Content)
        {
            this.Title = Title;
            this.Content = Content;
        }
    }
}
