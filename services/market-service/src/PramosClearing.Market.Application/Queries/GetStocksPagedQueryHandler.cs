using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;
using PramosClearing.MarketService.Domain.Repositories;

namespace PramosClearing.MarketService.Application.Queries;

public sealed class GetStocksPagedQueryHandler
    : IRequestHandler<GetStocksPagedQuery, PagedResult<StockResponse>>
{
    private readonly IStockRepository _repository;

    public GetStocksPagedQueryHandler(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<StockResponse>> Handle(
        GetStocksPagedQuery query, CancellationToken ct)
    {
        var (items, totalCount) = await _repository
            .GetPagedBySymbolAsync(query.SymbolFilter, query.Page, query.PageSize, ct)
            .ConfigureAwait(false);

        var responses = items.Select(s => new StockResponse(
            s.Id,
            s.Name,
            s.Currency,
            s.Symbol,
            s.Exchange,
            s.Sector,
            s.MarketIdentifier,
            s.IsActive,
            s.CreatedAt)).ToList();

        return new PagedResult<StockResponse>(responses, totalCount, query.Page, query.PageSize);
    }
}
