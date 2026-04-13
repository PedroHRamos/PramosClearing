namespace PramosClearing.OrderBook.Domain.ValueObjects;

public sealed record Price
{
    public decimal Value { get; }

    public Price(decimal value)
    {
        if (value <= 0)
            throw new ArgumentException("Price must be positive.", nameof(value));

        Value = value;
    }

    public static implicit operator decimal(Price price) => price.Value;
    public static implicit operator Price(decimal value) => new(value);

    public override string ToString() => Value.ToString("F2");
}
