namespace AspireDemo.ServiceDefaults.Models;

public class Result
{
    public Guid Id { get; set; }
    public Status Status { get; set; }
    public string Value { get; set; } = string.Empty;
}
