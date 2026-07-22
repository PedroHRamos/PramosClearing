namespace PramosClearing.OrderBook.Application.Options;

public sealed class SimulationOptions
{
    public int MinDelayMs { get; set; } = 100;
    public int MaxDelayMs { get; set; } = 100;
    public int ConcurrencyLevel { get; set; } = 1;
}
