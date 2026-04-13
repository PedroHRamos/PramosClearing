using Microsoft.Extensions.Logging;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Domain.Events;

namespace PramosClearing.OrderBook.Infrastructure.Publishers;

public sealed class LoggingOrderBookEventPublisher : IOrderBookEventPublisher
{
    private readonly ILogger<LoggingOrderBookEventPublisher> _logger;

    public LoggingOrderBookEventPublisher(ILogger<LoggingOrderBookEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(OrderBookUpdate update, CancellationToken ct)
    {
        _logger.LogInformation(
            "OrderBook {Action} | {Symbol}@{Exchange} | {Side} {Price:F2} x {Size} | {Timestamp:O}",
            update.Action,
            update.Symbol,
            update.Exchange,
            update.Side,
            update.Price,
            update.Size,
            update.Timestamp);

        return Task.CompletedTask;
    }
}
