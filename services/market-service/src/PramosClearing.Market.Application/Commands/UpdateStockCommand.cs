using MediatR;

namespace PramosClearing.MarketService.Application.Commands;

public sealed record UpdateStockCommand(
    Guid Id,
    string Name,
    string Currency,
    string Exchange,
    string Sector) : IRequest;
