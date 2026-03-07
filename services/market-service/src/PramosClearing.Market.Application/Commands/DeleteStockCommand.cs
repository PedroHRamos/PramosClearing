using MediatR;

namespace PramosClearing.MarketService.Application.Commands;

public sealed record DeleteStockCommand(Guid Id) : IRequest;
