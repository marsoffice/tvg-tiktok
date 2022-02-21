namespace MarsOffice.Tvg.TikTok.Abstractions
{
    public class VideoUploadResult
    {
        public string UserId { get; set; }
        public string VideoId { get; set; }
        public string JobId { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}