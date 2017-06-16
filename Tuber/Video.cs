using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tuber
{
    class Video
    {
        public string VideoId { get; set; }
        public string Title { get; set; }
        public DateTime PublishedAt { get; set; }

        private DateTime publishedAt = DateTime.MaxValue;
        
        public Video(string videoID, string title, DateTime publishedAt)
        {
            VideoId = videoID;
            Title = title;
            PublishedAt = publishedAt;
        }

        public override string ToString()
        {
            return PublishedAt + " " + Title;
        }
    }
}
