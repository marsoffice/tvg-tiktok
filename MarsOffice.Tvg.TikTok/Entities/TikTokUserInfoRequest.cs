using System.Collections.Generic;

namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokUserInfoRequest
    {
        public string open_id { get; set; }
        public string access_token { get; set; }
        public IEnumerable<string> fields { get; set; }
    }
}