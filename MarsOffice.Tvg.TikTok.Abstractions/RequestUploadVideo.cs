using System.Collections.Generic;

namespace MarsOffice.Tvg.TikTok.Abstractions
{
    public class RequestUploadVideo
    {
        public string VideoPath { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string VideoId { get; set; }
        public string JobId { get; set; }
        public IEnumerable<string> OpenIds { get; set; }
    }
}