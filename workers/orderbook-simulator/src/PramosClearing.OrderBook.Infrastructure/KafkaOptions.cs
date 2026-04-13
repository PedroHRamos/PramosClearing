namespace PramosClearing.OrderBook.Infrastructure;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic            { get; set; } = "orderbook-updates";
    public int    RetryCount       { get; set; } = 3;
    public int    RetryBackoffMs   { get; set; } = 500;
}
