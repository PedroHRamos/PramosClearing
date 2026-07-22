using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed record GetLatestPriceTickQuery(string Symbol, string Exchange)
    : IRequest<PriceTickResponse?>;