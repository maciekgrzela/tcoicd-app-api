using System;
using System.Collections.Generic;

namespace CloudMediaCenterAPI.Dtos.Out
{
    public class ShareContent
    {
        public string ShareName { get; set; }
        public List<ShareContentListItem> ShareContentListItems { get; set; }
    }

    public class ShareContentListItem
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }
}