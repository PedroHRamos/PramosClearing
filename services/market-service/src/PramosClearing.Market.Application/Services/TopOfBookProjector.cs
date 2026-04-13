using PramosClearing.MarketService.Application.Models;

namespace PramosClearing.MarketService.Application.Services;

public sealed class TopOfBookProjector
{
    private readonly Dictionary<string, OrderBookState> _books = new(StringComparer.Ordinal);

    public bool TryProject(OrderBookUpdateMessage update, Guid assetId, out PriceTickWriteModel? priceTick)
    {
        ArgumentNullException.ThrowIfNull(update);

        var symbol = update.Symbol.ToUpperInvariant();
        var exchange = update.Exchange.ToUpperInvariant();
        var key = string.Concat(symbol, "@", exchange);

        if (!_books.TryGetValue(key, out var book))
        {
            book = new OrderBookState();
            _books[key] = book;
        }

        if (!book.Apply(update))
        {
            priceTick = null;
            return false;
        }

        if (!book.TryGetBestBid(out var bid) || !book.TryGetBestAsk(out var ask))
        {
            priceTick = null;
            return false;
        }

        priceTick = new PriceTickWriteModel(
            update.Timestamp,
            assetId,
            symbol,
            exchange,
            bid,
            ask,
            Math.Round((bid + ask) / 2m, 8),
            0m,
            "orderbook-simulator");

        return true;
    }

    private sealed class OrderBookState
    {
        private readonly SortedDictionary<decimal, int> _bids = new(Comparer<decimal>.Create((left, right) => right.CompareTo(left)));
        private readonly SortedDictionary<decimal, int> _asks = new();

        public bool Apply(OrderBookUpdateMessage update)
        {
            var side = ResolveSide(update.Side);
            var action = ResolveAction(update.Action);

            if (side is null || action is null)
                return false;

            var levels = side == "bid" ? _bids : _asks;

            if (action == "remove" || update.Size <= 0)
            {
                levels.Remove(update.Price);
                return true;
            }

            levels[update.Price] = update.Size;
            return true;
        }

        public bool TryGetBestBid(out decimal bid)
        {
            foreach (var level in _bids)
            {
                bid = level.Key;
                return true;
            }

            bid = 0m;
            return false;
        }

        public bool TryGetBestAsk(out decimal ask)
        {
            foreach (var level in _asks)
            {
                ask = level.Key;
                return true;
            }

            ask = 0m;
            return false;
        }

        private static string? ResolveSide(string side)
        {
            if (string.Equals(side, "bid", StringComparison.OrdinalIgnoreCase))
                return "bid";

            if (string.Equals(side, "ask", StringComparison.OrdinalIgnoreCase))
                return "ask";

            return null;
        }

        private static string? ResolveAction(string action)
        {
            if (string.Equals(action, "add", StringComparison.OrdinalIgnoreCase))
                return "add";

            if (string.Equals(action, "update", StringComparison.OrdinalIgnoreCase))
                return "update";

            if (string.Equals(action, "remove", StringComparison.OrdinalIgnoreCase))
                return "remove";

            return null;
        }
    }
}