namespace MarsOffice.Tvg.TikTok.Entities
{
    public class TikTokUploadResponseData
    {
       public string share_id { get; set; }
    }

    public class TikTokUploadResponse
    {
        public TikTokUploadResponseData Data { get; set; }
    }
}