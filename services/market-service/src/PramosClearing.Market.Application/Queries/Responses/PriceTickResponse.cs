namespace PramosClearing.MarketService.Application.Queries.Responses;

public sealed record PriceTickResponse(
    DateTime Time,
    Guid AssetId,
    string Symbol,
    string Exchange,
    decimal Bid,
    decimal Ask,
    decimal Last,
    decimal Volume,
    string Source);