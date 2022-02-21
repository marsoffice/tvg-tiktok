namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokAuthResponse
    {
        public string open_id { get; set; }
        public string scope { get; set; }
        public string access_token { get; set; }
        public long expires_in { get; set; }
        public string refresh_token { get; set; }
        public long refresh_expires_in { get; set; }
    }
}