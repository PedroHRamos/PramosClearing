using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;
using PramosClearing.MarketService.Domain.Repositories;

namespace PramosClearing.MarketService.Application.Queries;

public sealed class GetStockBySymbolQueryHandler
    : IRequestHandler<GetStockBySymbolQuery, StockResponse?>
{
    private readonly IStockRepository _repository;

    public GetStockBySymbolQueryHandler(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task<StockResponse?> Handle(GetStockBySymbolQuery query, CancellationToken ct)
    {
        var stock = await _repository
            .GetBySymbolAsync(query.Symbol.ToUpperInvariant(), ct)
            .ConfigureAwait(false);

        if (stock is null)
            return null;

        return new StockResponse(
            stock.Id,
            stock.Name,
            stock.Currency,
            stock.Symbol,
            stock.Exchange,
            stock.Sector,
            stock.MarketIdentifier,
            stock.IsActive,
            stock.CreatedAt);
    }
}
