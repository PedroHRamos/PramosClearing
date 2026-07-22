namespace PramosClearing.MarketService.Application.Models;

public sealed record OrderBookUpdateMessage(
    string Symbol,
    string Exchange,
    string Side,
    decimal Price,
    int Size,
    string Action,
    DateTime Timestamp);