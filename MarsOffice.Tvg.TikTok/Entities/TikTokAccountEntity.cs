using System;
using Microsoft.Azure.Cosmos.Table;

namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokAccountEntity : TableEntity
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string AccountId { get; set; }
        public string AuthCode { get; set; }
        public string AccessToken { get; set; }
        public DateTimeOffset? AccessTokenExpAt { get; set; }
        public string RefreshToken { get; set; }
        public DateTimeOffset? RefreshTokenExpAt { get; set; }
        public DateTimeOffset? LastRefreshDate { get; set; }
    }
}
