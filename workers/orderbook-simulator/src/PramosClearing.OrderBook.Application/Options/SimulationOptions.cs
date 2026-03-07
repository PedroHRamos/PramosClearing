namespace PramosClearing.OrderBook.Application.Options;

public sealed class SimulationOptions
{
    public int MinDelayMs { get; set; } = 1;
    public int MaxDelayMs { get; set; } = 10;
}
