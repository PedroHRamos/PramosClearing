namespace PramosClearing.OrderBook.Domain.ValueObjects;

public sealed record Quantity
{
    public int Value { get; }

    public Quantity(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(value));

        Value = value;
    }

    public static implicit operator int(Quantity qty) => qty.Value;
    public static implicit operator Quantity(int value) => new(value);

    public override string ToString() => Value.ToString();
}
