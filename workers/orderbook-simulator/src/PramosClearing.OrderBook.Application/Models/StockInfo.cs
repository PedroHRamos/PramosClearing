namespace PramosClearing.OrderBook.Application.Models;

public sealed record StockInfo(
    string Symbol,
    string Exchange,
    string Currency,
    string Name);
