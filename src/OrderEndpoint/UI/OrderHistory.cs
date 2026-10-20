namespace OrderEndpoint.UI;

public record HistoryEntry(DateTimeOffset Time, string OrderId, string Event, string Detail);

// Memory-only record of what the saga handled, for the demo page; cleared on restart and not a durable audit
public class OrderHistory
{
    public const int Capacity = 100;

    readonly LinkedList<HistoryEntry> entries = new();

    public void Add(string orderId, string @event, string detail)
    {
        lock (entries)
        {
            entries.AddFirst(new HistoryEntry(DateTimeOffset.UtcNow, orderId, @event, detail));
            if (entries.Count > Capacity)
            {
                entries.RemoveLast();
            }
        }
    }

    // Newest first
    public HistoryEntry[] Snapshot()
    {
        lock (entries)
        {
            return [.. entries];
        }
    }
}
