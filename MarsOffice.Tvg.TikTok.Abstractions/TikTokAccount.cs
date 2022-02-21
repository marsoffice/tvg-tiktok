namespace MarsOffice.Tvg.TikTok.Abstractions
{
    public class TikTokAccount
    {
        public string UserId { get; set; }
        public string TikTokUsername { get; set; }
        public string Email { get; set; }
        public string AuthCode { get; set; }
        public string AccessToken { get; set; }
        public long? AccessTokenExp { get; set; }
        public string RefreshToken { get; set; }
        public long? RefreshTokenExp { get; set; }
    }
}