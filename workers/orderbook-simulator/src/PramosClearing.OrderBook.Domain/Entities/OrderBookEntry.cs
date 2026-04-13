using PramosClearing.OrderBook.Domain.ValueObjects;

namespace PramosClearing.OrderBook.Domain.Entities;

public sealed class OrderBookEntry
{
    public Price Price { get; }
    public Quantity Quantity { get; private set; }

    public OrderBookEntry(Price price, Quantity quantity)
    {
        Price    = price;
        Quantity = quantity;
    }

    public void Update(Quantity quantity) => Quantity = quantity;
}
