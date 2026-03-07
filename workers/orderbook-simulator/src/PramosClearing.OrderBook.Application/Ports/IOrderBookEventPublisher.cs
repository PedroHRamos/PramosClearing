using PramosClearing.OrderBook.Domain.Events;

namespace PramosClearing.OrderBook.Application.Ports;

public interface IOrderBookEventPublisher
{
    Task PublishAsync(OrderBookUpdate update, CancellationToken ct);
}
