namespace PramosClearing.MarketService.Infrastructure;

public sealed class KafkaConsumerOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "orderbook-updates";
    public string GroupId { get; set; } = "market-price-tick-consumer";
    public int BatchSize { get; set; } = 5000;
    public int FlushIntervalMs { get; set; } = 100;
}