namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokUserResponseData
    {
        public string open_id { get; set; }
        public string avatar_url { get; set; }
        public string display_name { get; set; }
    }

    public class TikTokUserResponse
    {
        public string Message { get; set; }
        public TikTokUserResponseData Data { get; set; }
    }
}