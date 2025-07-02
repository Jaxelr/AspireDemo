using Grpc.Core;

namespace AspireDemo.ServiceDefaults.Models;

public class Message
{
    public Guid Id { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Status Status { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum Status
{
    InProgress,
    Failed,
    Success
}
