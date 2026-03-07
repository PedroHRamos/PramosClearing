using PramosClearing.OrderBook.Application.Models;

namespace PramosClearing.OrderBook.Application.Ports;

public interface IStockLoader
{
    Task<IReadOnlyList<StockInfo>> LoadAllActiveAsync(CancellationToken ct);
}
