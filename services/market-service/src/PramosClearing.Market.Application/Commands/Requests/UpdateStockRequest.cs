namespace PramosClearing.MarketService.Application.Commands.Requests;

public sealed record UpdateStockRequest(
    string Name,
    string Currency,
    string Exchange,
    string Sector);
