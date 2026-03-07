using PramosClearing.OrderBook.Domain.Enums;
using PramosClearing.OrderBook.Domain.Events;
using PramosClearing.OrderBook.Domain.ValueObjects;

namespace PramosClearing.OrderBook.Domain.Entities;

public sealed class OrderBook
{
    private const decimal TickSize   = 0.01m;
    private const int     MaxLevels  = 10;
    private const int     SeedLevels = 5;

    private decimal _midPrice;

    private readonly SortedDictionary<decimal, OrderBookEntry> _bids;
    private readonly SortedDictionary<decimal, OrderBookEntry> _asks;

    public string Symbol   { get; }
    public string Exchange { get; }

    public IReadOnlyDictionary<decimal, OrderBookEntry> Bids => _bids;
    public IReadOnlyDictionary<decimal, OrderBookEntry> Asks => _asks;

    public OrderBook(string symbol, string exchange, decimal initialMidPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol,   nameof(symbol));
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange, nameof(exchange));

        if (initialMidPrice <= 0)
            throw new ArgumentException("Initial mid price must be positive.", nameof(initialMidPrice));

        Symbol   = symbol;
        Exchange = exchange;
        _midPrice = Round(initialMidPrice);

        _bids = new SortedDictionary<decimal, OrderBookEntry>(
            Comparer<decimal>.Create((a, b) => b.CompareTo(a)));
        _asks = new SortedDictionary<decimal, OrderBookEntry>();

        Seed();
    }

    public OrderBookUpdate GenerateUpdate(Random random)
    {
        MaybeDriftMidPrice(random);
        PruneCrossedLevels();
        EnsureMinimumLevels();

        var side   = random.Next(2) == 0 ? OrderSide.Bid : OrderSide.Ask;
        var levels = side == OrderSide.Bid ? _bids : _asks;

        var roll = random.NextDouble();

        if (levels.Count >= MaxLevels)
            return ApplyRemove(side, levels, random);

        if (levels.Count > 2 && roll < 0.20)
            return ApplyRemove(side, levels, random);

        if (roll < 0.60)
            return ApplyAdd(side, levels, random);

        return ApplyModify(side, levels, random);
    }

    private void Seed()
    {
        for (var i = 1; i <= SeedLevels; i++)
        {
            var bidPrice = Round(_midPrice - i * TickSize);
            var askPrice = Round(_midPrice + i * TickSize);
            _bids[bidPrice] = new OrderBookEntry(new Price(bidPrice), new Quantity(Random.Shared.Next(100, 2001)));
            _asks[askPrice] = new OrderBookEntry(new Price(askPrice), new Quantity(Random.Shared.Next(100, 2001)));
        }
    }

    private void MaybeDriftMidPrice(Random random)
    {
        if (random.NextDouble() > 0.20) return;

        var direction = random.Next(2) == 0 ? 1m : -1m;
        _midPrice = Round(Math.Max(TickSize, _midPrice + direction * TickSize));
    }

    private void PruneCrossedLevels()
    {
        foreach (var price in _bids.Keys.Where(p => p >= _midPrice).ToList())
            _bids.Remove(price);

        foreach (var price in _asks.Keys.Where(p => p <= _midPrice).ToList())
            _asks.Remove(price);
    }

    private void EnsureMinimumLevels()
    {
        if (_bids.Count == 0)
        {
            var p = Round(_midPrice - TickSize);
            _bids[p] = new OrderBookEntry(new Price(p), new Quantity(Random.Shared.Next(100, 2001)));
        }

        if (_asks.Count == 0)
        {
            var p = Round(_midPrice + TickSize);
            _asks[p] = new OrderBookEntry(new Price(p), new Quantity(Random.Shared.Next(100, 2001)));
        }
    }

    private OrderBookUpdate ApplyAdd(
        OrderSide side,
        SortedDictionary<decimal, OrderBookEntry> levels,
        Random random)
    {
        var price = FindFreshPrice(side, levels, random);
        var size  = random.Next(100, 2001);
        levels[price] = new OrderBookEntry(new Price(price), new Quantity(size));
        return new OrderBookUpdate(Symbol, Exchange, side, price, size, OrderAction.Add, DateTime.UtcNow);
    }

    private OrderBookUpdate ApplyModify(
        OrderSide side,
        SortedDictionary<decimal, OrderBookEntry> levels,
        Random random)
    {
        var price = PickRandomKey(levels, random);
        var size  = random.Next(100, 2001);
        levels[price].Update(new Quantity(size));
        return new OrderBookUpdate(Symbol, Exchange, side, price, size, OrderAction.Update, DateTime.UtcNow);
    }

    private OrderBookUpdate ApplyRemove(
        OrderSide side,
        SortedDictionary<decimal, OrderBookEntry> levels,
        Random random)
    {
        var price = PickRandomKey(levels, random);
        levels.Remove(price);
        return new OrderBookUpdate(Symbol, Exchange, side, price, 0, OrderAction.Remove, DateTime.UtcNow);
    }

    private decimal FindFreshPrice(
        OrderSide side,
        SortedDictionary<decimal, OrderBookEntry> levels,
        Random random)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var offset = random.Next(1, MaxLevels + 1) * TickSize;
            var price  = side == OrderSide.Bid
                ? Round(_midPrice - offset)
                : Round(_midPrice + offset);

            if (!levels.ContainsKey(price))
                return Math.Max(TickSize, price);
        }

        var fallback = side == OrderSide.Bid
            ? Round(_midPrice - TickSize)
            : Round(_midPrice + TickSize);

        return Math.Max(TickSize, fallback);
    }

    private static decimal PickRandomKey(SortedDictionary<decimal, OrderBookEntry> levels, Random random) =>
        levels.Keys.ElementAt(random.Next(levels.Count));

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
