using PramosClearing.OrderBook.Domain.Enums;

namespace PramosClearing.OrderBook.Domain.Events;

public sealed record OrderBookUpdate(
    string Symbol,
    string Exchange,
    OrderSide Side,
    decimal Price,
    int Size,
    OrderAction Action,
    DateTime Timestamp);
