namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokUserResponseUserData
    {
        public string open_id { get; set; }
        public string avatar_url { get; set; }
        public string display_name { get; set; }
        public string union_id { get; set; }
    }
    public class TikTokUserResponseData
    {
        public TikTokUserResponseUserData User { get; set; }
    }

    public class TikTokUserResponse
    {
        public string Message { get; set; }
        public TikTokUserResponseData Data { get; set; }
    }
}