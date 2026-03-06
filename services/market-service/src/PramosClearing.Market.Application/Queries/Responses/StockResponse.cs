namespace PramosClearing.MarketService.Application.Queries.Responses;

public sealed record StockResponse(
    Guid Id,
    string Name,
    string Currency,
    string Symbol,
    string Exchange,
    string Sector,
    string MarketIdentifier,
    bool IsActive,
    DateTime CreatedAt);
