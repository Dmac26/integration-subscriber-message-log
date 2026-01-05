using System.ComponentModel.DataAnnotations;

namespace Wachter.IntegrationSubscriberMessageLog.Models
{
    public class MessageLog
    {
        [Key] // Optional but safe since name isn't convention
        public Guid MessageLogId { get; set; } // Changed back to MessageId to match PK column

        public string Exchange { get; set; } = null!;

        public string MessageStatus { get; set; } = null!;

        public string Payload { get; set; } = null!;

        public bool FailureAddressed { get; set; } // assuming bit/bool; change to int if it's not
    }
}