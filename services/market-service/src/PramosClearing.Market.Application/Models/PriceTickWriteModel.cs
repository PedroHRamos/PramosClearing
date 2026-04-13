namespace PramosClearing.MarketService.Application.Models;

public sealed record PriceTickWriteModel(
    DateTime Time,
    Guid AssetId,
    string Symbol,
    string Exchange,
    decimal Bid,
    decimal Ask,
    decimal Last,
    decimal Volume,
    string Source);