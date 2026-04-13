using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed record GetPriceTicksQuery(string Symbol, string Exchange, int Take)
    : IRequest<IReadOnlyList<PriceTickResponse>>;