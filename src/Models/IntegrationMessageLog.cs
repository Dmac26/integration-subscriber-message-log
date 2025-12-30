namespace Wachter.IntegrationSubscriberMessageLog.Models
{
    public class IntegrationMessageLog
    {
        public Guid MessageId { get; set; }
        public DateTime ReceivedDateTime { get; set; }
        public string SourceSystem { get; set; } = null!;
        public string EventType { get; set; } = null!;
        public string SerializedContent { get; set; } = null!;
        public DateTime? ProcessedDateTime { get; set; }
        public string? ErrorMessage { get; set; }
    }
}