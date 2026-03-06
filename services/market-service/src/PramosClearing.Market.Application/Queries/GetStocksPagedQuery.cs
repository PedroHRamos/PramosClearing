using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed record GetStocksPagedQuery(
    string SymbolFilter,
    int Page,
    int PageSize) : IRequest<PagedResult<StockResponse>>;
