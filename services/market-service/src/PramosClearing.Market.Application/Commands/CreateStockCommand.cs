using MediatR;

namespace PramosClearing.MarketService.Application.Commands;

public sealed record CreateStockCommand(
    string Name,
    string Currency,
    string Symbol,
    string Exchange,
    string Sector) : IRequest<Guid>;
