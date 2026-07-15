namespace BabyToys.Models;

public sealed class SessionMarker
{
    public DateTimeOffset StartedAt { get; set; }
    public int ProcessId { get; set; }
}
