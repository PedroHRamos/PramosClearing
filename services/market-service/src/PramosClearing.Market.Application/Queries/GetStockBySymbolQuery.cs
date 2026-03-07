using MediatR;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed record GetStockBySymbolQuery(string Symbol) : IRequest<StockResponse?>;
