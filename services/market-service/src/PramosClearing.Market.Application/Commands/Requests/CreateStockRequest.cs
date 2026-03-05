namespace PramosClearing.MarketService.Application.Commands.Requests;

public sealed record CreateStockRequest(
    string Name,
    string Currency,
    string Symbol,
    string Exchange,
    string Sector);
